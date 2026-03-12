using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace ELKH.Controllers
{
    /// <summary>
    /// Admin controller for ASP.NET Core Identity role management.
    /// Provides CRUD operations for roles and role-assignment for individual users.
    /// </summary>
    /// <remarks>
    /// TABLE OF CONTENTS
    /// ================================================================================
    /// 1. Constructor &amp; Dependencies
    /// 2. Role Listing
    ///    - ListRoles()                          // GET: All roles
    /// 3. Role Creation
    ///    - CreateRole() GET                     // GET: New role form
    ///    - CreateRole() POST                    // POST: Persist new role
    /// 4. Role Editing
    ///    - EditRole(roleId) GET                 // GET: Edit role form
    ///    - EditRole(model) POST                 // POST: Persist role name change
    /// 5. Role Assignment
    ///    - AssignRoles(roleName) GET            // GET: Assignment form (optionally pre-filtered)
    ///    - AssignRoles(model) POST              // POST: Assign role to a user by email
    /// ================================================================================
    ///
    /// All endpoints require the Admin role.
    /// Role mutations delegate directly to <see cref="RoleManager{TRole}"/> so that
    /// Identity's own validation and concurrency handling are exercised.
    /// </remarks>
    [Authorize(Roles = "Admin")]
    public class AdminRoleController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminRoleController(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // =====================================================================
        // Role Listing
        // =====================================================================

        /// <summary>Displays all application roles with their IDs and names.</summary>
        public IActionResult ListRoles()
        {
            var roles = _roleManager.Roles
                .Select(r => new RoleVM { RoleId = r.Id, RoleName = r.Name })
                .ToList();

            return View(roles);
        }

// =====================================================================
// Role Creation
// =====================================================================

/// <summary>Renders the form for creating a new role.</summary>
public IActionResult CreateRole()
{
    return View();
}

        /// <summary>Persists the new role to the Identity store. Re-displays the form with errors on failure.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(RoleVM model)
        {
            if (ModelState.IsValid)
            {
                var result = await _roleManager.CreateAsync(new IdentityRole(model.RoleName!));
                if (result.Succeeded)
                {
                    TempData["Success"] = "Role created successfully.";
                    return RedirectToAction("ListRoles");
                }
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
            }
            return View(model);
        }

        // =====================================================================
        // Role Editing
        // =====================================================================

        /// <summary>
        /// Renders the edit form for an existing role.
        /// Returns <see cref="NotFoundResult"/> when no role with <paramref name="roleId"/> exists.
        /// </summary>
        public async Task<IActionResult> EditRole(string roleId)
        {
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null) return NotFound();
            return View(new RoleVM { RoleId = role.Id, RoleName = role.Name });
        }

        /// <summary>Persists a role-name change to the Identity store. Re-displays the form with errors on failure.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(RoleVM model)
        {
            if (!ModelState.IsValid) return View(model);

            var role = await _roleManager.FindByIdAsync(model.RoleId);
            if (role == null) return NotFound();

            role.Name = model.RoleName;
            var result = await _roleManager.UpdateAsync(role);

            if (result.Succeeded)
            {
                TempData["Success"] = "Role updated successfully.";
                return RedirectToAction("ListRoles");
            }
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

public async Task<IActionResult> AssignRoles(string? userId, string? returnTo, string? roleId)
        {
            // Pre-fill email only when we already know the user
            string? prefilledEmail = null;
            if (!string.IsNullOrEmpty(userId))
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null) return NotFound();
                prefilledEmail = user.Email;
            }

            // Lock role when coming from RoleUsers page
            bool isRoleLocked = returnTo == "RoleDetails" && !string.IsNullOrEmpty(roleId);
            string? preselectedRoleName = null;
            if (isRoleLocked)
            {
                var role = await _roleManager.FindByIdAsync(roleId!);
                preselectedRoleName = role?.Name;
            }

            var model = new AssignRoleVM
            {
                UserId      = userId,
                Email       = prefilledEmail,
                ReturnTo    = returnTo,
                RoleId      = roleId,
                IsRoleLocked = isRoleLocked,
                RoleName    = preselectedRoleName,
                Roles       = _roleManager.Roles
                                .Select(r => new RoleVM { RoleId = r.Id, RoleName = r.Name })
                                .ToList()
            };

            return View(model);
        }
/// <summary>
/// Assigns a role to the user identified by <c>model.Email</c>.
/// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRoles(AssignRoleVM model)
        {
            ModelState.Remove("Roles");

            if (string.IsNullOrEmpty(model.Email))
            {
                ModelState.AddModelError("", "Please enter user email.");
                await ReloadRoles(model);
                return View(model);
            }

            if (string.IsNullOrEmpty(model.RoleName))
            {
                ModelState.AddModelError("", "Please select a role.");
                await ReloadRoles(model);
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                await ReloadRoles(model);
                return View(model);
            }

            if (await _userManager.IsInRoleAsync(user, model.RoleName!))
            {
                ModelState.AddModelError("", "User is already assigned to this role.");
                await ReloadRoles(model);
                return View(model);
            }

            var result = await _userManager.AddToRoleAsync(user, model.RoleName);

            if (result.Succeeded)
            {
                TempData["Success"] = "Role assigned successfully.";

                if (model.ReturnTo == "UserDetails" && !string.IsNullOrEmpty(model.UserId))
                    return RedirectToAction("AccountDetails", "Admin", new { id = model.UserId });

                if (model.ReturnTo == "RoleDetails" && !string.IsNullOrEmpty(model.RoleId))
                    return RedirectToAction("RoleUsers", new { roleId = model.RoleId });

                return RedirectToAction("ListRoles");
            }

            TempData["Error"] = "Failed to assign role.";
            await ReloadRoles(model);
            return View(model);
        }
/// <summary>Repopulates the role drop-down on the assignment form after a validation failure.</summary>
        private async Task ReloadRoles(AssignRoleVM model)
        {
            model.Roles = _roleManager.Roles
                .Select(r => new RoleVM { RoleId = r.Id, RoleName = r.Name })
                .ToList();
        }

        // ================= VIEW USERS IN ROLE =================
        public async Task<IActionResult> RoleUsers(string roleId)
        {
            if (string.IsNullOrEmpty(roleId)) return NotFound();

            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null) return NotFound();

            var usersInRole = new List<IdentityUser>();
            foreach (var user in _userManager.Users)
            {
                if (role.Name != null && await _userManager.IsInRoleAsync(user, role.Name))
                    usersInRole.Add(user);
            }

            ViewBag.RoleId = role.Id;
            ViewBag.RoleName = role.Name;

            return View(usersInRole);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveUserFromRole(string roleId, string userId)
        {
            var role = await _roleManager.FindByIdAsync(roleId);
            var user = await _userManager.FindByIdAsync(userId);

            if (role == null || user == null)
            {
                TempData["Error"] = "Something went wrong.";
                return RedirectToAction("ListRoles");
            }

            var result = await _userManager.RemoveFromRoleAsync(user, role.Name!);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
                ? "User removed from role successfully."
                : "Failed to remove user from role.";

            return RedirectToAction("RoleUsers", new { roleId });
        }

        // ================= DELETE =================
        public async Task<IActionResult> DeleteRole(string roleId)
        {
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null) return NotFound();
            return View(new RoleVM { RoleId = role.Id, RoleName = role.Name });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRole(RoleVM model)
        {
            try
            {
                var role = await _roleManager.FindByIdAsync(model.RoleId);
                if (role == null)
                {
                    TempData["Error"] = "Role not found.";
                    return RedirectToAction("ListRoles");
                }

                var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
                if (usersInRole.Any())
                {
                    TempData["Error"] = "Cannot delete role because it is assigned to users.";
                    return RedirectToAction("ListRoles");
                }

                var result = await _roleManager.DeleteAsync(role);
                TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
                    ? "Role deleted successfully."
                    : "Failed to delete role.";

                return RedirectToAction("ListRoles");
            }
            catch
            {
                TempData["Error"] = "An unexpected error occurred while deleting the role.";
                return RedirectToAction("ListRoles");
            }
        }
    }
}
