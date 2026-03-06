using ELKH.Models;
using ELKH.Repositories;
using ELKH.Services;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    /// <summary>
    /// Controller responsible for user account related operations: profile management,
    /// address book CRUD and login history. All actions require an authenticated user.
    /// </summary>
    /// <remarks>
    /// TABLE OF CONTENTS
    /// ================================================================================
    /// 1. Constructor & Dependencies
    /// 2. Dashboard & Profile
    ///    - Index()                               // GET: User dashboard
    ///    - Profile()                             // GET: View/edit profile
    ///    - Profile() POST                        // POST: Update profile
    ///    - History()                             // GET: Login history
    /// 3. Address Management (CRUD)
    ///    - Addresses()                           // GET: List all addresses
    ///    - AddAddress() GET                      // GET: New address form
    ///    - AddAddress() POST                     // POST: Create address
    ///    - EditAddress(id) GET                   // GET: Edit address form
    ///    - EditAddress(id) POST                  // POST: Update address
    ///    - DeleteAddress(id) GET                 // GET: Delete confirmation
    ///    - DeleteAddressConfirmed() POST         // POST: Confirm deletion
    /// 4. Private Helpers
    ///    - GetCurrentUserIdAsync()               // Get authenticated user's ID
    /// ================================================================================
    /// 
    /// Responsibilities:
    /// - Keep controller actions thin: validate inputs, enforce security checks and
    ///   delegate persistence and business logic to repositories or services.
    /// - Apply per-request presentation preferences (culture/currency) when available.
    /// - All actions require authentication (inherited from AuthenticatedControllerBase)
    /// </remarks>
    public class UserController : AuthenticatedControllerBase
    {
        private readonly IRegisteredUserProfileRepo _profileRepository;
        private readonly IRegisteredUserLogRepo _logRepository;
        private readonly IContactDetailRepo _contactRepository;
        private readonly IRatingService _ratingService;

        public UserController(
            IRegisteredUserProfileRepo profileRepository,
            IRegisteredUserLogRepo logRepository,
            IContactDetailRepo contactRepository,
            IRatingService ratingService,
            IUserService userService) 
            : base(userService)
        {
            _profileRepository = profileRepository;
            _logRepository = logRepository;
            _contactRepository = contactRepository;
            _ratingService = ratingService;
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
                vm.WishListCount       = dashboard.WishlistCount;
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
                _profileRepository.Add(newProfile);
            }
            else
            {
                existing.FirstName = vm.Profile.FirstName;
                existing.LastName  = vm.Profile.LastName;
                _profileRepository.UpdateAndSave(existing);
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
                _profileRepository.Add(newProfile);
            }
            else
            {
                existing.AvatarData     = bytes;
                existing.AvatarMimeType = file.ContentType;
                _profileRepository.UpdateAndSave(existing);
            }

            _logRepository.LogActivityAsync(email, "AvatarUploaded", $"Uploaded profile picture ({file.ContentType}, {file.Length} bytes)");
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
                _profileRepository.UpdateAndSave(existing);
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

            bool success = await _contactRepository.AddAsync(contact);

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

            bool success = await _contactRepository.UpdateAsync(contact);

            if (success)
            {
                await _logRepository.LogActivityAsync(User.Identity?.Name ?? string.Empty, "AddressUpdated", $"Updated address at {vm.Street}, {vm.City}");
                SetSuccessMessage("Address updated successfully");
            }
            else
            {
                SetErrorMessage("Failed to update address");
            }

            return RedirectToAction(nameof(EditProfile));
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
            bool success = await _contactRepository.UpdateAsync(contact);

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
                CurrentSort = sort
            });
        }

        #endregion

            }
        }
