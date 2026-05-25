using System.Security.Claims;
using ELKH.Controllers.Base;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.Services;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers;

// TABLE OF CONTENTS
// - Address listing
// - Create address
// - Edit address
// - Delete address

/// <summary>
/// Controller responsible for user address book and contact detail management.
/// Handles CRUD operations for shipping and billing addresses.
/// </summary>
public class UserAddressController : UserControllerBase
{
    private readonly IContactDetailRepo _contactRepository;
    private readonly IRegisteredUserLogRepo _logRepository;
    private readonly ILogger<UserAddressController> _logger;

    public UserAddressController(
        IContactDetailRepo contactRepository,
        IRegisteredUserLogRepo logRepository,
        IUserService userService,
        ILogger<UserAddressController> logger,
        ELKH.Data.ApplicationDbContext db)
        : base(db, userService)
    {
        _contactRepository = contactRepository;
        _logRepository = logRepository;
        _logger = logger;
    }

    #region Address Listing

    /// <summary>
    /// GET: UserAddress/Index - List all user addresses
    /// </summary>
    /// <returns>Address list view with all user's saved addresses</returns>
    public async Task<IActionResult> Index()
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

    #endregion

    #region Address Creation

    /// <summary>
    /// GET: UserAddress/Create - Display new address form
    /// </summary>
    /// <returns>Create address view with empty form</returns>
    public IActionResult Create()
    {
        var vm = new ContactDetailVM
        {
            Country = "Canada", // Default to Canada
            IsDefault = false
        };
        return View(vm);
    }

    /// <summary>
    /// POST: UserAddress/Create - Create a new address
    /// </summary>
    /// <param name="vm">Address data to create</param>
    /// <returns>Redirect to address list or form with validation errors</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ContactDetailVM vm)
    {
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
            return Challenge();

        // Get the Identity UserId from claims
        var identityUserId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(identityUserId))
        {
            SetErrorMessage("Unable to identify user");
            return RedirectToAction(nameof(Index));
        }

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

        return RedirectToAction(nameof(Index));
    }

    #endregion

    #region Address Editing

    /// <summary>
    /// GET: UserAddress/Edit/5 - Display edit form for existing address
    /// </summary>
    /// <param name="id">Address ID to edit</param>
    /// <returns>Edit address view with current data or redirect if not found</returns>
    public async Task<IActionResult> Edit(int id)
    {
        var userId = await GetCurrentUserIdAsync();
        var contact = await _contactRepository.GetByIdAsync(id);

        // Security check: ensure user owns this address
        if (contact is null || contact.FkRegisteredUserId != userId.Value)
        {
            SetWarningMessage("Address not found");
            return RedirectToAction(nameof(Index));
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

    /// <summary>
    /// POST: UserAddress/Edit/5 - Update existing address
    /// </summary>
    /// <param name="vm">Updated address data</param>
    /// <returns>Redirect to address list or form with validation errors</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ContactDetailVM vm)
    {
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
            return Challenge();

        var existing = await _contactRepository.GetByIdAsync(vm.ContactId);

        // Security check: ensure user owns this address
        if (existing is null || existing.FkRegisteredUserId != userId.Value)
        {
            SetWarningMessage("Address not found");
            return RedirectToAction(nameof(Index));
        }

        // Get the Identity UserId from claims
        var identityUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(identityUserId))
        {
            SetErrorMessage("Unable to identify user");
            return RedirectToAction(nameof(Index));
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

        return RedirectToAction(nameof(Index));
    }

    #endregion

    #region Address Deletion

    /// <summary>
    /// GET: UserAddress/Delete/5 - Display delete confirmation
    /// </summary>
    /// <param name="id">Address ID to delete</param>
    /// <returns>Delete confirmation view or redirect if not found</returns>
    public async Task<IActionResult> Delete(int id)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
            return Challenge();

        var contact = await _contactRepository.GetByIdAsync(id);

        // Security check: ensure user owns this address
        if (contact is null || contact.FkRegisteredUserId != userId.Value)
        {
            SetWarningMessage("Address not found");
            return RedirectToAction(nameof(Index));
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

    /// <summary>
    /// POST: UserAddress/Delete/5 - Confirm address deletion
    /// </summary>
    /// <param name="id">Address ID to delete</param>
    /// <returns>Redirect to address list with result message</returns>
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
            return Challenge();

        var contact = await _contactRepository.GetByIdAsync(id);

        // Security check: ensure user owns this address
        if (contact is null || contact.FkRegisteredUserId != userId.Value)
        {
            SetWarningMessage("Address not found");
            return RedirectToAction(nameof(Index));
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

        return RedirectToAction(nameof(Index));
    }

    #endregion

    #region Default Address Management

    /// <summary>
    /// POST: UserAddress/SetDefault/5 - Set address as default
    /// </summary>
    /// <param name="id">Address ID to set as default</param>
    /// <returns>Redirect to address list with result message</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefault(int id)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
            return Challenge();

        var contact = await _contactRepository.GetByIdAsync(id);

        // Security check: ensure user owns this address
        if (contact is null || contact.FkRegisteredUserId != userId.Value)
        {
            SetWarningMessage("Address not found");
            return RedirectToAction(nameof(Index));
        }

        // First clear any existing default addresses for this user
        var userAddresses = await _contactRepository.GetAllByUserIdAsync(userId.Value);
        foreach (var address in userAddresses.Where(a => a.IsDefault))
        {
            address.IsDefault = false;
            await _contactRepository.UpdateAndSaveAsync(address);
        }

        // Set the new default
        contact.IsDefault = true;
        bool success = await _contactRepository.UpdateAndSaveAsync(contact);

        if (success)
        {
            await _logRepository.LogActivityAsync(User.Identity?.Name ?? string.Empty, "DefaultAddressChanged", $"Set default address to {contact.Street}, {contact.City}");
            SetSuccessMessage("Default address updated successfully");
        }
        else
        {
            SetErrorMessage("Failed to update default address");
        }

        return RedirectToAction(nameof(Index));
    }

    #endregion
}
