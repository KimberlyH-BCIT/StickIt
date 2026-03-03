using ELKH.Data;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Controllers
{
    [Authorize]
    public class UserController : Controller
    {
        private readonly RegisteredUserProfileRepo _profileRepository;
        private readonly RegisteredUserLogRepo _logRepository;
        private readonly ContactDetailRepo _contactRepository;
        private readonly ApplicationDbContext _db;

        public UserController(
            RegisteredUserProfileRepo profileRepository,
            RegisteredUserLogRepo logRepository,
            ContactDetailRepo contactRepository,
            ApplicationDbContext db)
        {
            _profileRepository = profileRepository;
            _logRepository = logRepository;
            _contactRepository = contactRepository;
            _db = db;
        }

        #region Dashboard & Profile

        // GET: User/Index - Dashboard showing profile summary
        public IActionResult Index()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email))
                return Challenge();

            var profileEntity = _profileRepository.GetById(email);

            var vm = new UserDashboardVM
            {
                Profile = profileEntity is null ? null : new UserProfileVM
                {
                    PkEmail = profileEntity.PkEmail,
                    FirstName = profileEntity.FirstName,
                    LastName = profileEntity.LastName
                }
            };

            return View(vm);
        }

        // GET: User/EditProfile
        public IActionResult EditProfile()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email))
                return Challenge();

            var profile = _profileRepository.GetById(email);

            if (profile is null)
            {
                // Create new profile if doesn't exist
                return View(new UserProfileVM { PkEmail = email });
            }

            var vm = new UserProfileVM
            {
                PkEmail = profile.PkEmail,
                FirstName = profile.FirstName,
                LastName = profile.LastName
            };

            return View(vm);
        }

        // POST: User/EditProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditProfile(UserProfileVM vm)
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email))
                return Challenge();

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var existing = _profileRepository.GetById(email);

            if (existing is null)
            {
                // Create new profile
                var newProfile = new UserProfileModel
                {
                    PkEmail = email,
                    FirstName = vm.FirstName,
                    LastName = vm.LastName
                };
                _profileRepository.Add(newProfile);
            }
            else
            {
                // Update existing profile
                existing.FirstName = vm.FirstName;
                existing.LastName = vm.LastName;
                _db.SaveChanges();
            }

            TempData["Message"] = "success, Profile updated successfully";
            return RedirectToAction(nameof(Index));
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
                TempData["Message"] = "success, Address added successfully";
            }
            else
            {
                TempData["Message"] = "danger, Failed to add address";
            }

            return RedirectToAction(nameof(Addresses));
        }

        // GET: User/EditAddress/5
        public async Task<IActionResult> EditAddress(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var contact = await _contactRepository.GetByIdAsync(id);
            
            // Security check here ✅
            if (contact is null || contact.FkRegisteredUserId != userId.Value)
            {
                TempData["Message"] = "warning, Address not found";
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
                TempData["Message"] = "warning, Address not found";
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
                TempData["Message"] = "success, Address updated successfully";
            }
            else
            {
                TempData["Message"] = "danger, Failed to update address";
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
                TempData["Message"] = "warning, Address not found";
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
                TempData["Message"] = "warning, Address not found";
                return RedirectToAction(nameof(Addresses));
            }

            bool success = await _contactRepository.DeleteAsync(id);

            if (success)
            {
                TempData["Message"] = "success, Address deleted successfully";
            }
            else
            {
                TempData["Message"] = "danger, Failed to delete address";
            }

            return RedirectToAction(nameof(Addresses));
        }

        // POST: User/SetDefaultAddress/5
        [HttpPost]
        public async Task<IActionResult> SetDefaultAddress(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId is null)
                return Challenge();

            var contact = await _contactRepository.GetByIdAsync(id);
            
            if (contact is null || contact.FkRegisteredUserId != userId.Value)
            {
                TempData["Message"] = "warning, Address not found";
                return RedirectToAction(nameof(Addresses));
            }

            contact.IsDefault = true;
            bool success = await _contactRepository.UpdateAsync(contact);

            if (success)
            {
                TempData["Message"] = "success, Default address updated";
            }
            else
            {
                TempData["Message"] = "danger, Failed to set default address";
            }

            return RedirectToAction(nameof(Addresses));
        }

        #endregion

        #region Login History

        // GET: User/LoginHistory - Last 20 logs
        public IActionResult LoginHistory()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email))
                return Challenge();

            var logs = _logRepository.GetByEmail(email)
                .OrderByDescending(l => l.LogInTime)
                .Take(20)
                .Select(l => new UserLogVM
                {
                    LogInTime = l.LogInTime,
                    LogOutTime = l.LogOutTime,
                    Abandoned = l.Abandoned
                })
                .ToList();

            return View(logs);
        }

        #endregion

        #region Helpers

        private async Task<int?> GetCurrentUserIdAsync()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email))
                return null;

            var registeredUser = await _db.RegisteredUsers
                .FirstOrDefaultAsync(u => u.Email == email);

            return registeredUser?.PkRegisteredUserId;
        }

        #endregion
    }
}

