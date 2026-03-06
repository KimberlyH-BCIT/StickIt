using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace ELKH.Controllers
{
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

        // ================= LIST =================
        public IActionResult ListRoles()
        {
            var roles = _roleManager.Roles
                .Select(r => new RoleVM { RoleId = r.Id, RoleName = r.Name })
                .ToList();

            return View(roles);
        }

        // ================= CREATE =================
        public IActionResult CreateRole() => View();

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

        // ================= EDIT =================
        public async Task<IActionResult> EditRole(string roleId)
        {
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null) return NotFound();
            return View(new RoleVM { RoleId = role.Id, RoleName = role.Name });
        }

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

            if (await _userManager.IsInRoleAsync(user, model.RoleName))
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
                if (await _userManager.IsInRoleAsync(user, role.Name))
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
