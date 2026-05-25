using ELKH.Controllers.Base;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.Services;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers;

// TABLE OF CONTENTS
// - Profile details
// - Profile update
// - Avatar upload
// - Activity sections

/// <summary>
/// Controller responsible for user profile management operations including
/// dashboard, profile editing, avatar upload, and user activity sections.
/// </summary>
public class UserProfileController : UserControllerBase
{
    private readonly IRegisteredUserProfileRepo _profileRepository;
    private readonly IRegisteredUserLogRepo _logRepository;

    // CA1861: Constant arrays to avoid repeated allocations
    private static readonly string[] AllowedImageTypes = { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
    private readonly ILogger<UserProfileController> _logger;

    public UserProfileController(
        IRegisteredUserProfileRepo profileRepository,
        IRegisteredUserLogRepo logRepository,
        IUserService userService,
        ILogger<UserProfileController> logger,
        ELKH.Data.ApplicationDbContext db)
        : base(db, userService)
    {
        _profileRepository = profileRepository;
        _logRepository = logRepository;
        _logger = logger;
    }

    #region Dashboard & Profile Management

    /// <summary>
    /// GET: UserProfile/Index - Main user dashboard with activity summaries
    /// </summary>
    /// <returns>Dashboard view with user profile and activity sections</returns>
    public async Task<IActionResult> Index()
    {
        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return authResult;

        var profileEntity = _profileRepository?.GetById(email);

        var vm = new UserDashboardVM
        {
            Profile = profileEntity is null ? null : new UserProfileVM
            {
                PkEmail = profileEntity.PkEmail,
                FirstName = profileEntity.FirstName,
                LastName = profileEntity.LastName
            }
        };

        var registered = await GetCurrentUserAsync();

        if (registered != null)
        {
            var userId = registered.PkRegisteredUserId;

            var dashboard = await UserService.GetDashboardDataAsync(userId);
            vm.WishlistCount = dashboard.WishlistCount;
            vm.WishlistSection = dashboard.Wishlist;
            vm.ActiveOrdersSection = dashboard.ActiveOrders;
            vm.OrderHistorySection = dashboard.OrderHistory;
        }

        return View(vm);
    }

    /// <summary>
    /// GET: UserProfile/Edit - Display profile editing form
    /// </summary>
    /// <returns>Profile edit view with current user data</returns>
    public async Task<IActionResult> Edit()
    {
        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return authResult;

        var profile = _profileRepository.GetById(email);

        var vm = new UserProfilePageVM
        {
            Profile = profile is null
                ? new UserProfileVM { PkEmail = email }
                : new UserProfileVM
                {
                    PkEmail = profile.PkEmail,
                    FirstName = profile.FirstName,
                    LastName = profile.LastName,
                    HasAvatar = profile.AvatarData is not null
                }
        };

        return View(vm);
    }

    /// <summary>
    /// POST: UserProfile/Edit - Update user profile information
    /// </summary>
    /// <param name="vm">Profile data to update</param>
    /// <returns>Redirect to edit form with success message or validation errors</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserProfilePageVM vm)
    {
        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return authResult;

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var existing = _profileRepository.GetById(email);

        if (existing is null)
        {
            var newProfile = new UserProfileModel
            {
                PkEmail = email,
                FirstName = vm.Profile.FirstName,
                LastName = vm.Profile.LastName
            };
            await _profileRepository.AddAndSaveAsync(newProfile);
        }
        else
        {
            existing.FirstName = vm.Profile.FirstName;
            existing.LastName = vm.Profile.LastName;
            await _profileRepository.UpdateAndSaveAsync(existing);
        }

        SetSuccessMessage("Profile updated successfully");
        await _logRepository.LogActivityAsync(email, "ProfileUpdated", $"Name updated to {vm.Profile.FirstName} {vm.Profile.LastName}");

        return RedirectToAction(nameof(Edit));
    }

    #endregion

    #region Avatar Management

    /// <summary>
    /// POST: UserProfile/UploadAvatar - Upload and save user avatar image
    /// </summary>
    /// <param name="file">The image file to upload</param>
    /// <returns>Redirect to edit profile with success/error message</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return authResult;

        // Validation
        var maxBytes = 10 * 1024 * 1024; // 10 MB

        if (file is null || file.Length == 0)
        {
            SetErrorMessage("Please select an image file to upload.");
            return RedirectToAction(nameof(Edit));
        }

        if (file.Length > maxBytes)
        {
            SetErrorMessage("The image must not exceed 10 MB.");
            return RedirectToAction(nameof(Edit));
        }

        if (!AllowedImageTypes.Contains(file.ContentType))
        {
            SetErrorMessage("Only JPEG, PNG, GIF, and WebP images are supported.");
            return RedirectToAction(nameof(Edit));
        }

        using var ms = new System.IO.MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var existing = _profileRepository.GetById(email);
        if (existing is null)
        {
            var newProfile = new UserProfileModel
            {
                PkEmail = email,
                FirstName = string.Empty,
                LastName = string.Empty,
                AvatarData = bytes,
                AvatarMimeType = file.ContentType
            };
            await _profileRepository.AddAndSaveAsync(newProfile);
        }
        else
        {
            existing.AvatarData = bytes;
            existing.AvatarMimeType = file.ContentType;
            await _profileRepository.UpdateAndSaveAsync(existing);
        }

        await _logRepository.LogActivityAsync(email, "AvatarUploaded", $"Uploaded profile picture ({file.ContentType}, {file.Length} bytes)");
        SetSuccessMessage("Profile picture updated successfully.");
        return RedirectToAction(nameof(Edit));
    }

    /// <summary>
    /// POST: UserProfile/RemoveAvatar - Remove user's avatar image
    /// </summary>
    /// <returns>Redirect to edit profile with success message</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAvatar()
    {
        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return authResult;

        var existing = _profileRepository.GetById(email);
        if (existing is not null && existing.AvatarData is not null)
        {
            existing.AvatarData = null;
            existing.AvatarMimeType = null;
            await _profileRepository.UpdateAndSaveAsync(existing);
            await _logRepository.LogActivityAsync(email, "AvatarRemoved", "Removed profile picture");
            SetSuccessMessage("Profile picture removed.");
        }

        return RedirectToAction(nameof(Edit));
    }

    #endregion

    #region Dashboard Sections (AJAX)

    /// <summary>
    /// GET: UserProfile/WishlistSection - Load wishlist section for dashboard (AJAX)
    /// </summary>
    /// <param name="page">Page number for pagination</param>
    /// <param name="sort">Sort order for wishlist items</param>
    /// <returns>Partial view with wishlist data</returns>
    [HttpGet]
    public async Task<IActionResult> WishlistSection(int page = 1, string sort = "date_desc")
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null) return Unauthorized();

        var vm = await UserService.GetWishlistSectionAsync(userId.Value, page, sort);
        return PartialView("_WishlistSection", vm);
    }

    /// <summary>
    /// GET: UserProfile/ActiveOrdersSection - Load active orders section for dashboard (AJAX)
    /// </summary>
    /// <param name="page">Page number for pagination</param>
    /// <param name="sort">Sort order for orders</param>
    /// <returns>Partial view with active orders data</returns>
    [HttpGet]
    public async Task<IActionResult> ActiveOrdersSection(int page = 1, string sort = "date_desc")
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null) return Unauthorized();

        var vm = await UserService.GetOrderSectionAsync(userId.Value, page, sort, activeOnly: true);
        return PartialView("_ActiveOrdersSection", vm);
    }

    /// <summary>
    /// GET: UserProfile/OrderHistorySection - Load order history section for dashboard (AJAX)
    /// </summary>
    /// <param name="page">Page number for pagination</param>
    /// <param name="sort">Sort order for orders</param>
    /// <returns>Partial view with order history data</returns>
    [HttpGet]
    public async Task<IActionResult> OrderHistorySection(int page = 1, string sort = "date_desc")
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null) return Unauthorized();

        var vm = await UserService.GetOrderSectionAsync(userId.Value, page, sort, activeOnly: false);
        return PartialView("_OrderHistorySection", vm);
    }

    #endregion

    #region Activity History

    /// <summary>
    /// GET: UserProfile/History - Display user activity history and login logs
    /// </summary>
    /// <param name="page">Page number for pagination</param>
    /// <returns>Activity history view with paginated logs</returns>
    public async Task<IActionResult> History(int page = 1)
    {
        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return authResult;

        // For now, return a simple view until UserHistoryVM is implemented
        ViewData["Email"] = email;
        ViewData["Page"] = page;

        return View();
    }

    #endregion
}
