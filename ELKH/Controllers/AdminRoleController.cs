using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

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

        // ================= LIST ROLES =================
        public IActionResult ListRoles()
        {
            var roles = _roleManager.Roles
                .Select(r => new RoleVM
                {
                    RoleId = r.Id,
                    RoleName = r.Name
                }).ToList();

            return View(roles);
        }

        // ================= CREATE ROLE =================
        public IActionResult CreateRole()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(RoleVM model)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await _roleManager.CreateAsync(new IdentityRole(model.RoleName!));

            if (result.Succeeded)
            {
                TempData["Success"] = "Role created successfully.";
                return RedirectToAction("ListRoles");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        // ================= EDIT ROLE =================
        public async Task<IActionResult> EditRole(string roleId)
        {
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null) return NotFound();

            return View(new RoleVM
            {
                RoleId = role.Id,
                RoleName = role.Name
            });
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

        // ================= ASSIGN ROLE =================
        public async Task<IActionResult> AssignRoles(string? userId, string? returnTo, string? roleId)
        {
            string? email = null;

            if (!string.IsNullOrEmpty(userId))
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null) return NotFound();
                email = user.Email;
            }

            bool lockRole = returnTo == "RoleDetails" && !string.IsNullOrEmpty(roleId);
            string? roleName = null;

            if (lockRole)
            {
                var role = await _roleManager.FindByIdAsync(roleId);
                roleName = role?.Name;
            }

            var model = new AssignRoleVM
            {
                UserId = userId,
                Email = email,
                ReturnTo = returnTo,
                RoleId = roleId,
                IsRoleLocked = lockRole,
                RoleName = roleName,
                Roles = GetRoles()
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
                model.Roles = GetRoles();
                return View(model);
            }

            if (string.IsNullOrEmpty(model.RoleName))
            {
                ModelState.AddModelError("", "Please select a role.");
                model.Roles = GetRoles();
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                model.Roles = GetRoles();
                return View(model);
            }

            if (await _userManager.IsInRoleAsync(user, model.RoleName!))
            {
                ModelState.AddModelError("", "User already has this role.");
                model.Roles = GetRoles();
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
            model.Roles = GetRoles();
            return View(model);
        }

        // ================= VIEW USERS IN ROLE =================
        public async Task<IActionResult> RoleUsers(string roleId)
        {
            if (string.IsNullOrEmpty(roleId)) return NotFound();

            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null) return NotFound();

            var users = new List<IdentityUser>();

            foreach (var user in _userManager.Users)
            {
                if (await _userManager.IsInRoleAsync(user, role.Name))
                    users.Add(user);
            }

            ViewBag.RoleId = role.Id;
            ViewBag.RoleName = role.Name;

            return View(users);
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

            TempData[result.Succeeded ? "Success" : "Error"] =
                result.Succeeded ? "User removed from role." : "Failed to remove user.";

            return RedirectToAction("RoleUsers", new { roleId });
        }

        // ================= DELETE ROLE =================
        public async Task<IActionResult> DeleteRole(string roleId)
        {
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null) return NotFound();

            return View(new RoleVM
            {
                RoleId = role.Id,
                RoleName = role.Name
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRole(RoleVM model)
        {
            var role = await _roleManager.FindByIdAsync(model.RoleId);
            if (role == null)
            {
                TempData["Error"] = "Role not found.";
                return RedirectToAction("ListRoles");
            }

            var users = await _userManager.GetUsersInRoleAsync(role.Name!);

            if (users.Any())
            {
                TempData["Error"] = "Role cannot be deleted because it is assigned.";
                return RedirectToAction("ListRoles");
            }

            var result = await _roleManager.DeleteAsync(role);

            TempData[result.Succeeded ? "Success" : "Error"] =
                result.Succeeded ? "Role deleted successfully." : "Failed to delete role.";

            return RedirectToAction("ListRoles");
        }

        // ================= HELPER =================
        private List<RoleVM> GetRoles()
        {
            return _roleManager.Roles
                .Select(r => new RoleVM
                {
                    RoleId = r.Id,
                    RoleName = r.Name
                }).ToList();
        }
    }
}