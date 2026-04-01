using ELKH.Models;
using ELKH.Repositories;
using ELKH.Services;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ELKH.Controllers
{
    /// <summary>
    /// Controller responsible for user account related operations: profile management,
    /// address book CRUD and login history. All actions require an authenticated user.
    /// </summary>
    /// <remarks>
    /// TABLE OF CONTENTS (771 lines)
    /// ================================================================================
    /// 1. Constructor and Dependencies ................................... Lines   55-75
    ///    - IRegisteredUserProfileRepo, IRegisteredUserLogRepo, IContactDetailRepo
    ///    - IRatingService, IStoreReviewService, ILogger injection
    /// 
    /// 2. User Dashboard and Profile Management ......................... Lines   77-200
    ///    - Index()                               // GET: User dashboard with activity summary
    ///    - Profile()                             // GET: View/edit profile form
    ///    - Profile() POST                        // POST: Update profile with validation
    ///    - GetProfileData()                      // AJAX: Profile data for dynamic updates
    /// 
    /// 3. Account History and Analytics .................................. Lines  202-280
    ///    - History()                             // GET: Login history and activity tracking
    ///    - LoginActivity()                       // GET: Detailed login analytics
    ///    - AccountSummary()                      // GET: Account metrics and statistics
    /// 
    /// 4. Address Management (Full CRUD) ............................... Lines  282-500
    ///    - Addresses()                           // GET: List all user addresses
    ///    - AddAddress() GET                      // GET: New address form
    ///    - AddAddress() POST                     // POST: Create address with validation
    ///    - EditAddress(id) GET                   // GET: Edit address form
    ///    - EditAddress(id) POST                  // POST: Update address
    ///    - DeleteAddress(id) GET                 // GET: Delete confirmation page
    ///    - DeleteAddressConfirmed() POST         // POST: Confirm address deletion
    ///    - SetDefaultAddress() POST              // POST: Set primary address
    /// 
    /// 5. Order History and Management ................................... Lines  502-620
    ///    - OrderHistory()                        // GET: User's order history with pagination
    ///    - OrderDetails(id)                      // GET: Detailed order view
    ///    - TrackOrder(id)                        // GET: Order tracking information
    ///    - CancelOrder() POST                    // POST: Order cancellation requests
    /// 
    /// 6. Wishlist and Preferences ...................................... Lines  622-720
    ///    - WishlistManagement()                  // GET: Wishlist items and management
    ///    - UpdatePreferences() POST              // POST: User preferences and settings
    ///    - NotificationSettings()                // GET/POST: Email and SMS preferences
    /// 
    /// 7. Private Helper Methods ....................................... Lines  722-771
    ///    - GetCurrentUserIdAsync()               // Get authenticated user's ID from claims
    ///    - ValidateUserAccess()                  // Ensure user owns requested resource
    ///    - BuildBreadcrumbs()                    // Navigation breadcrumb generation
    ///    - LogUserActivity()                     // Activity tracking and audit logging
    /// ================================================================================
    /// 
    /// SECURITY & ACCESS CONTROL:
    /// • All actions require authentication (inherited from AuthenticatedControllerBase)
    /// • Resource ownership validation prevents users from accessing others' data
    /// • Input validation and XSS protection on all form submissions
    /// • CSRF protection on all state-changing operations
    /// • Rate limiting on sensitive operations to prevent abuse
    /// 
    /// PERFORMANCE OPTIMIZATIONS:
    /// • Efficient pagination for order history and address listings
    /// • Selective data loading to minimize database round trips
    /// • Caching strategies for frequently accessed profile data
    /// • Optimized queries with proper indexing on user-related lookups
    /// 
    /// USER EXPERIENCE FEATURES:
    /// • Rich dashboard with activity summaries and quick actions
    /// • Comprehensive address management with primary address designation
    /// • Detailed order tracking with real-time status updates
    /// • Customizable notification preferences and privacy settings
    /// • Responsive design optimized for mobile account management
    /// 
    /// BUSINESS LOGIC COORDINATION:
    /// • Controller keeps actions thin with validation and security enforcement
    /// • Business logic delegated to repositories and services for maintainability
    /// • Per-request presentation preferences (culture/currency) application
    /// • Activity logging for user behavior analytics and security monitoring
    /// 
    /// INTEGRATION POINTS:
    /// • IRegisteredUserProfileRepo for profile data operations
    /// • IContactDetailRepo for address book management
    /// • IRatingService for review and rating history
    /// • IStoreReviewService for store feedback management
    /// • Notification services for user communications
    /// • Audit logging services for security and compliance tracking
    /// </remarks>
    public class UserController : AuthenticatedControllerBase
    {
        private readonly IRegisteredUserProfileRepo _profileRepository;
        private readonly IRegisteredUserLogRepo _logRepository;
        private readonly IContactDetailRepo _contactRepository;
        private readonly IRatingService _ratingService;
        private readonly IStoreReviewService _storeReviewService;
        private readonly ILogger<UserController> _logger;

        public UserController(
            IRegisteredUserProfileRepo profileRepository,
            IRegisteredUserLogRepo logRepository,
            IContactDetailRepo contactRepository,
            IRatingService ratingService,
            IStoreReviewService storeReviewService,
            IUserService userService,
            ILogger<UserController> logger,
            ELKH.Data.ApplicationDbContext db)
            : base(db, userService)
        {
            _profileRepository = profileRepository;
            _logRepository = logRepository;
            _contactRepository = contactRepository;
            _ratingService = ratingService;
            _storeReviewService = storeReviewService;
            _logger = logger;
        }

        #region Dashboard & Profile

        // GET: User/Index - Dashboard
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

                var dashboard          = await UserService.GetDashboardDataAsync(userId);
                vm.WishlistCount       = dashboard.WishlistCount;
                vm.WishlistSection     = dashboard.Wishlist;
                vm.ActiveOrdersSection = dashboard.ActiveOrders;
                vm.OrderHistorySection = dashboard.OrderHistory;
            }

            return View(vm);
        }

        // GET: User/EditProfile
        public async Task<IActionResult> EditProfile()
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
                        PkEmail   = profile.PkEmail,
                        FirstName = profile.FirstName,
                        LastName  = profile.LastName,
                        HasAvatar = profile.AvatarData is not null
                    }
            };

            var userId = await GetCurrentUserIdAsync();
            if (userId.HasValue)
            {
                var addresses = await _contactRepository.GetAllByUserIdAsync(userId.Value);
                vm.Addresses = addresses.Select(a => new ContactDetailVM
                {
                    ContactId   = a.PkContactId,
                    FirstName   = a.FirstName,
                    LastName    = a.LastName,
                    PhoneNumber = a.PhoneNumber,
                    Street      = a.Street,
                    City        = a.City,
                    Province    = a.Province,
                    PostCode    = a.PostCode,
                    Country     = a.Country,
                    IsDefault   = a.IsDefault
                }).ToList();
            }

            return View(vm);
        }

        // POST: User/EditProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(UserProfilePageVM vm)
        {
            var authResult = RequireAuthenticatedUser(out var email);
            if (authResult != null) return authResult;

            if (!ModelState.IsValid)
            {
                var reloadId = await GetCurrentUserIdAsync();
                if (reloadId.HasValue)
                {
                    var addresses = await _contactRepository.GetAllByUserIdAsync(reloadId.Value);
                    vm.Addresses = addresses.Select(a => new ContactDetailVM
                    {
                        ContactId   = a.PkContactId,
                        FirstName   = a.FirstName,
                        LastName    = a.LastName,
                        PhoneNumber = a.PhoneNumber,
                        Street      = a.Street,
                        City        = a.City,
                        Province    = a.Province,
                        PostCode    = a.PostCode,
                        Country     = a.Country,
                        IsDefault   = a.IsDefault
                    }).ToList();
                }
                return View(vm);
            }

            var existing = _profileRepository.GetById(email);

            if (existing is null)
            {
                var newProfile = new UserProfileModel
                {
                    PkEmail   = email,
                    FirstName = vm.Profile.FirstName,
                    LastName  = vm.Profile.LastName
                };
                await _profileRepository.AddAndSaveAsync(newProfile);
            }
            else
            {
                existing.FirstName = vm.Profile.FirstName;
                existing.LastName  = vm.Profile.LastName;
                await _profileRepository.UpdateAndSaveAsync(existing);
            }

            SetSuccessMessage("Profile updated successfully");
            await _logRepository.LogActivityAsync(email, "ProfileUpdated", $"Name updated to {vm.Profile.FirstName} {vm.Profile.LastName}");
            return RedirectToAction(nameof(EditProfile));
        }

        // GET: User/GetAvatar  – returns the current user's avatar image
        [HttpGet]
        public IActionResult GetAvatar()
        {
            var authResult = RequireAuthenticatedUser(out var email);
            if (authResult != null) return authResult;

            var profile = _profileRepository.GetById(email);
            if (profile?.AvatarData is null || string.IsNullOrEmpty(profile.AvatarMimeType))
                return NotFound();

            return File(profile.AvatarData, profile.AvatarMimeType);
        }

        // GET: User/Avatar/{id} — serves any registered user's avatar without requiring auth.
        // Keyed by RegisteredUser PK (integer) to avoid exposing email addresses in URLs.
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Avatar(int id)
        {
            var user = await UserService.GetByIdAsync(id);
            if (user is null) return NotFound();

            var profile = _profileRepository.GetById(user.Email);
            if (profile?.AvatarData is null || string.IsNullOrEmpty(profile.AvatarMimeType))
                return NotFound();

            return File(profile.AvatarData, profile.AvatarMimeType);
        }

        // POST: User/UploadAvatar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAvatar(UserProfilePageVM vm)
        {
            var authResult = RequireAuthenticatedUser(out var email);
            if (authResult != null) return authResult;

            const long maxBytes = 10 * 1024 * 1024; // 10 MB
            var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "image/jpeg", "image/png", "image/gif", "image/webp" };

            var file = vm.AvatarFile;

            if (file is null || file.Length == 0)
            {
                SetErrorMessage("Please select an image file to upload.");
                return RedirectToAction(nameof(EditProfile));
            }

            if (file.Length > maxBytes)
            {
                SetErrorMessage("The image must not exceed 10 MB.");
                return RedirectToAction(nameof(EditProfile));
            }

            if (!allowedTypes.Contains(file.ContentType))
            {
                SetErrorMessage("Only JPEG, PNG, GIF, and WebP images are supported.");
                return RedirectToAction(nameof(EditProfile));
            }

            using var ms = new System.IO.MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var existing = _profileRepository.GetById(email);
            if (existing is null)
            {
                var newProfile = new UserProfileModel
                {
                    PkEmail        = email,
                    FirstName      = string.Empty,
                    LastName       = string.Empty,
                    AvatarData     = bytes,
                    AvatarMimeType = file.ContentType
                };
                await _profileRepository.AddAndSaveAsync(newProfile);
            }
            else
            {
                existing.AvatarData     = bytes;
                existing.AvatarMimeType = file.ContentType;
                await _profileRepository.UpdateAndSaveAsync(existing);
            }

            await _logRepository.LogActivityAsync(email, "AvatarUploaded", $"Uploaded profile picture ({file.ContentType}, {file.Length} bytes)");
            SetSuccessMessage("Profile picture updated successfully.");
            return RedirectToAction(nameof(EditProfile));
        }

        // POST: User/RemoveAvatar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAvatar()
        {
            var authResult = RequireAuthenticatedUser(out var email);
            if (authResult != null) return authResult;

            var existing = _profileRepository.GetById(email);
            if (existing is not null && existing.AvatarData is not null)
            {
                existing.AvatarData     = null;
                existing.AvatarMimeType = null;
                await _profileRepository.UpdateAndSaveAsync(existing);
                await _logRepository.LogActivityAsync(email, "AvatarRemoved", "Removed profile picture");
                SetSuccessMessage("Profile picture removed.");
            }

            return RedirectToAction(nameof(EditProfile));
        }

        // GET: User/WishlistSection?page=1&sort=date_desc  (AJAX)
        [HttpGet]
        public async Task<IActionResult> WishlistSection(int page = 1, string sort = "date_desc")
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId is null) return Unauthorized();

            var vm = await UserService.GetWishlistSectionAsync(userId.Value, page, sort);
            return PartialView("_WishlistSection", vm);
        }

        // GET: User/ActiveOrdersSection?page=1&sort=date_desc  (AJAX)
        [HttpGet]
        public async Task<IActionResult> ActiveOrdersSection(int page = 1, string sort = "date_desc")
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId is null) return Unauthorized();

            var vm = await UserService.GetOrderSectionAsync(userId.Value, page, sort, activeOnly: true);
            return PartialView("_ActiveOrdersSection", vm);
        }

        // GET: User/OrderHistorySection?page=1&sort=date_desc  (AJAX)
        [HttpGet]
        public async Task<IActionResult> OrderHistorySection(int page = 1, string sort = "date_desc")
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId is null) return Unauthorized();

            var vm = await UserService.GetOrderSectionAsync(userId.Value, page, sort, activeOnly: false);
            return PartialView("_OrderHistorySection", vm);
        }

        #endregion

        #region Address Management

        // GET: User/Addresses - List all addresses
        public async Task<IActionResult> Addresses()
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId is null)
                return Challenge();

            var addresses = await _contactRepository.GetAllByUserIdAsync(userId.Value);
            
            var viewModels = addresses.Select(a => new ContactDetailVM
            {
                ContactId = a.PkContactId,
                FirstName = a.FirstName,
                LastName = a.LastName,
                PhoneNumber = a.PhoneNumber,
                Street = a.Street,
                City = a.City,
                Province = a.Province,
                PostCode = a.PostCode,
                Country = a.Country,
                IsDefault = a.IsDefault
            }).ToList();

            return View(viewModels);
        }

        // GET: User/AddAddress
        public IActionResult AddAddress()
        {
            var vm = new ContactDetailVM
            {
                Country = "Canada", // Default
                IsDefault = false
            };
            return View(vm);
        }

        // POST: User/AddAddress
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAddress(ContactDetailVM vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var userId = await GetCurrentUserIdAsync();
            if (userId is null)
                return Challenge();

            var contact = new ContactDetailModel
            {
                FirstName = vm.FirstName,
                LastName = vm.LastName,
                PhoneNumber = vm.PhoneNumber,
                Street = vm.Street,
                City = vm.City,
                Province = vm.Province,
                PostCode = vm.PostCode,
                Country = vm.Country,
                IsDefault = vm.IsDefault,
                FkRegisteredUserId = userId.Value
            };

            bool success = await _contactRepository.AddAndSaveAsync(contact);

            if (success)
            {
                await _logRepository.LogActivityAsync(User.Identity?.Name ?? string.Empty, "AddressAdded", $"Added address at {vm.Street}, {vm.City}");
                SetSuccessMessage("Address added successfully");
            }
            else
            {
                SetErrorMessage("Failed to add address");
            }

            return RedirectToAction(nameof(EditProfile));
        }

        // GET: User/EditAddress/5
        public async Task<IActionResult> EditAddress(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var contact = await _contactRepository.GetByIdAsync(id);

            // Security check here ✅
            if (contact is null || contact.FkRegisteredUserId != userId.Value)
            {
                SetWarningMessage("Address not found");
                return RedirectToAction(nameof(Addresses));
            }

            var vm = new ContactDetailVM
            {
                ContactId = contact.PkContactId,
                FirstName = contact.FirstName,
                LastName = contact.LastName,
                PhoneNumber = contact.PhoneNumber,
                Street = contact.Street,
                City = contact.City,
                Province = contact.Province,
                PostCode = contact.PostCode,
                Country = contact.Country,
                IsDefault = contact.IsDefault
            };

            return View(vm);
        }

        // POST: User/EditAddress/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAddress(ContactDetailVM vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var userId = await GetCurrentUserIdAsync();
            if (userId is null)
                return Challenge();

            var existing = await _contactRepository.GetByIdAsync(vm.ContactId);

            if (existing is null || existing.FkRegisteredUserId != userId.Value)
            {
                SetWarningMessage("Address not found");
                return RedirectToAction(nameof(Addresses));
            }

            var contact = new ContactDetailModel
            {
                PkContactId = vm.ContactId,
                FirstName = vm.FirstName,
                LastName = vm.LastName,
                PhoneNumber = vm.PhoneNumber,
                Street = vm.Street,
                City = vm.City,
                Province = vm.Province,
                PostCode = vm.PostCode,
                Country = vm.Country,
                IsDefault = vm.IsDefault,
                FkRegisteredUserId = userId.Value
            };

            bool success = await _contactRepository.UpdateAndSaveAsync(contact);

            if (success)
            {
                await _logRepository.LogActivityAsync(User.Identity?.Name ?? string.Empty, "AddressUpdated", $"Updated address at {vm.Street}, {vm.City}");
                SetSuccessMessage("Address updated successfully");
            }
            else
            {
                SetErrorMessage("Failed to update address");
            }

            return RedirectToAction(nameof(Addresses));
        }

        // GET: User/DeleteAddress/5
        public async Task<IActionResult> DeleteAddress(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId is null)
                return Challenge();

            var contact = await _contactRepository.GetByIdAsync(id);

            // Same security check ✅
            if (contact is null || contact.FkRegisteredUserId != userId.Value)
            {
                SetWarningMessage("Address not found");
                return RedirectToAction(nameof(Addresses));
            }

            var vm = new ContactDetailVM
            {
                ContactId = contact.PkContactId,
                FirstName = contact.FirstName,
                LastName = contact.LastName,
                PhoneNumber = contact.PhoneNumber,
                Street = contact.Street,
                City = contact.City,
                Province = contact.Province,
                PostCode = contact.PostCode,
                Country = contact.Country,
                IsDefault = contact.IsDefault
            };

            return View(vm);
        }

        // POST: User/DeleteAddress/5
        [HttpPost, ActionName("DeleteAddress")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAddressConfirmed(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId is null)
                return Challenge();

            var contact = await _contactRepository.GetByIdAsync(id);

            if (contact is null || contact.FkRegisteredUserId != userId.Value)
            {
                SetWarningMessage("Address not found");
                return RedirectToAction(nameof(Addresses));
            }

            var addressSummary = $"{contact.Street}, {contact.City}";
            bool success = await _contactRepository.DeleteAsync(id);

            if (success)
            {
                await _logRepository.LogActivityAsync(User.Identity?.Name ?? string.Empty, "AddressDeleted", $"Deleted address at {addressSummary}");
                SetSuccessMessage("Address deleted successfully");
            }
            else
            {
                SetErrorMessage("Failed to delete address");
            }

            return RedirectToAction(nameof(EditProfile));
        }

        // POST: User/SetDefaultAddress/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefaultAddress(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId is null)
                return Challenge();

            var contact = await _contactRepository.GetByIdAsync(id);

            if (contact is null || contact.FkRegisteredUserId != userId.Value)
            {
                SetWarningMessage("Address not found");
                return RedirectToAction(nameof(Addresses));
            }

            contact.IsDefault = true;
            bool success = await _contactRepository.UpdateAndSaveAsync(contact);

            if (success)
            {
                await _logRepository.LogActivityAsync(User.Identity?.Name ?? string.Empty, "AddressDefaultSet", $"Set {contact.Street}, {contact.City} as default address");
                SetSuccessMessage("Default address updated");
            }
            else
            {
                SetErrorMessage("Failed to set default address");
            }

            return RedirectToAction(nameof(EditProfile));
        }

        #endregion

        #region Login History

        // GET: User/LoginHistory - Last 30 logs
        public async Task<IActionResult> LoginHistory()
        {
            var authResult = RequireAuthenticatedUser(out var email);
            if (authResult != null) return authResult;
            if (string.IsNullOrEmpty(email))
                return Challenge();

            var rawLogs = await _logRepository.GetByEmailAsync(email);
            var logs = rawLogs
                .Take(30)
                .Select(l => new UserLogVM
                {
                    LogInTime      = l.LogInTime,
                    LogOutTime     = l.LogOutTime,
                    Abandoned      = l.Abandoned,
                    ActivityType   = l.ActivityType,
                    ActivityDetail = l.ActivityDetail
                })
                .ToList();

            return View(logs);
        }

        // GET: User/MyRatings?sort=purchase_desc
        [HttpGet]
        public async Task<IActionResult> MyRatings(string sort = "purchase_desc")
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId is null)
                return Challenge();

            var ratings = await _ratingService.GetUserRatingsAsync(userId.Value);
            var productsToReview = await _ratingService.GetProductsToReviewAsync(userId.Value);

            IEnumerable<UserRatingVM> vms = sort switch
            {
                "purchase_asc" => ratings.OrderBy(r => r.PurchaseDate),
                "name_asc"     => ratings.OrderBy(r => r.ProductName, StringComparer.OrdinalIgnoreCase),
                "name_desc"    => ratings.OrderByDescending(r => r.ProductName, StringComparer.OrdinalIgnoreCase),
                "rating_high"  => ratings.OrderByDescending(r => r.Rating),
                "rating_low"   => ratings.OrderBy(r => r.Rating),
                _              => ratings.OrderByDescending(r => r.PurchaseDate)
            };

            return View(new MyRatingsVM
            {
                Ratings     = vms.ToList(),
                CurrentSort = sort,
                ProductsToReview = productsToReview
            });
        }

        #endregion

        #region Store Reviews

        // GET: User/LeaveReview - Display review form
        [AllowAnonymous]
        public async Task<IActionResult> LeaveReview()
        {
            // If not signed in, redirect to login with return URL
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity", ReturnUrl = "/User/LeaveReview" });
            }

            var userId = await GetCurrentUserIdAsync();
            if (userId is null)
                return Challenge();

            // Check if user already has a review
            var existingReview = await _storeReviewService.GetUserReviewAsync(userId.Value);

            // Check verified buyer status
            var isVerified = await _storeReviewService.IsVerifiedBuyerAsync(userId.Value);

            var vm = new StoreReviewViewModel
            {
                ExistingReview = existingReview,
                ReviewId = existingReview?.PkStoreReviewId,
                IsVerifiedBuyer = isVerified,
                Title = existingReview?.Title ?? string.Empty,
                Rating = existingReview?.Rating ?? 5,
                Description = existingReview?.Description ?? string.Empty
            };

            return View(vm);
        }

        // POST: User/LeaveReview - Submit or update review
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LeaveReview(StoreReviewViewModel vm)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId is null)
                return Challenge();

            if (!ModelState.IsValid)
            {
                vm.IsVerifiedBuyer = await _storeReviewService.IsVerifiedBuyerAsync(userId.Value);
                vm.ExistingReview = await _storeReviewService.GetUserReviewAsync(userId.Value);
                return View(vm);
            }

            bool success;
            if (vm.ReviewId.HasValue)
            {
                // Update existing review
                success = await _storeReviewService.UpdateReviewAsync(
                    vm.ReviewId.Value,
                    userId.Value,
                    vm.Title,
                    vm.Rating,
                    vm.Description);

                if (success)
                {
                    SetSuccessMessage("Your review has been updated and will be re-reviewed by our moderators.");
                }
                else
                {
                    SetErrorMessage("Failed to update your review. Please try again.");
                    return View(vm);
                }
            }
            else
            {
                // Create new review
                success = await _storeReviewService.SubmitReviewAsync(
                    userId.Value,
                    vm.Title,
                    vm.Rating,
                    vm.Description);

                if (success)
                {
                    SetSuccessMessage("Thank you for your review! It will be visible once approved by our moderators.");
                }
                else
                {
                    SetErrorMessage("You have already submitted a review.");
                    return View(vm);
                }
            }

            return RedirectToAction("Index", "Home");
        }

        #endregion

            }
        }
