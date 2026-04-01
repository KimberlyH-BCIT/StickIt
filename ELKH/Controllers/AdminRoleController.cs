using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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

            return View(roles); // ← was missing return statement
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
                TempData["Success"] = "Role updated successfully."; // ← block was cut off
                return RedirectToAction("ListRoles");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        // ================= ASSIGN ROLE (GET) =================
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
                var role = await _roleManager.FindByIdAsync(roleId!);
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

        // ================= ASSIGN ROLE (POST) =================
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

            var result = await _userManager.AddToRoleAsync(user, model.RoleName!);
            if (result.Succeeded)
            {
                TempData["Success"] = $"Role '{model.RoleName}' assigned successfully.";

                // ── Redirect back to where the user came from ──────────
                return model.ReturnTo switch
                {
                    // Came from AccountDetails → go back to that user's detail page
                    "UserDetails" => RedirectToAction("AccountDetails", "Admin",
                                         new { id = model.UserId }),

                    // Came from the role's user list → go back to that list
                    "RoleDetails" when !string.IsNullOrEmpty(model.RoleId)
                                  => RedirectToAction("RoleUsers",
                                         new { roleId = model.RoleId }),

                    // Default fallback
                    _ => RedirectToAction("ListRoles")
                };
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

            // Single efficient query instead of per-user IsInRoleAsync calls
            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);

            ViewBag.RoleId = role.Id;
            ViewBag.RoleName = role.Name;

            return View(usersInRole); // ← was returning wrong variable 'users'
        }

        // ================= REMOVE USER FROM ROLE =================
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

            // ← removed duplicate TempData assignment
            TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
                ? "User removed from role successfully."
                : "Failed to remove user from role.";

            return RedirectToAction("RoleUsers", new { roleId });
        }

        // ================= DELETE ROLE (GET) =================
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

        // ================= DELETE ROLE (POST) =================
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