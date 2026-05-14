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
    [Authorize(Roles = "Admin")]
    public class AdminRoleController : Controller
    {
        #region Constructor & Dependencies

        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        /// <summary>
        /// Initializes the role management controller with Identity managers.
        /// </summary>
        /// <param name="userManager">ASP.NET Core Identity user manager for user operations</param>
        /// <param name="roleManager">ASP.NET Core Identity role manager for role CRUD</param>
        public AdminRoleController(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        #endregion

        #region Role Listing

        /// <summary>
        /// Displays all application roles with their IDs and names.
        /// </summary>
        /// <returns>View with list of all roles in the system</returns>
        /// <remarks>
        /// Queries all roles from Identity's RoleManager and projects to RoleVM.
        /// This endpoint serves as the main role management dashboard.
        /// </remarks>
        public IActionResult ListRoles()
        {
            var roles = _roleManager.Roles
                .Select(r => new RoleVM { RoleId = r.Id, RoleName = r.Name })
                .ToList();

            return View(roles);
        }

        #endregion

#region Role Creation

/// <summary>
/// Renders the form for creating a new role.
/// </summary>
/// <returns>Empty role creation form</returns>
public IActionResult CreateRole()
{
    return View();
}

/// <summary>
/// Persists the new role to the Identity store.
/// </summary>
/// <param name="model">Role view model containing the new role name</param>
/// <returns>Redirects to ListRoles on success, re-displays form with errors on failure</returns>
/// <remarks>
/// Delegates to RoleManager.CreateAsync for Identity validation:
/// - Role name uniqueness enforcement
/// - Role name format validation (no special characters, etc.)
/// - Concurrency token generation
/// All Identity errors are captured and displayed in the form.
/// </remarks>
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> CreateRole(RoleVM model)
{
    if (ModelState.IsValid)
    {
        // Delegate to Identity for validation and persistence
        var result = await _roleManager.CreateAsync(new IdentityRole(model.RoleName!));

        if (result.Succeeded)
        {
            TempData["Success"] = "Role created successfully.";
            return RedirectToAction("ListRoles");
        }

        // Capture all Identity validation errors
        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);
    }
    return View(model);
}

#endregion

        #region Role Editing

        /// <summary>
        /// Renders the edit form for an existing role.
        /// </summary>
        /// <param name="roleId">Identity role ID (GUID string)</param>
        /// <returns>Edit form view with role data, or NotFound if role doesn't exist</returns>
        /// <remarks>
        /// Retrieves role from Identity store and projects to RoleVM for editing.
        /// </remarks>
        public async Task<IActionResult> EditRole(string roleId)
        {
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null) return NotFound();

            return View(new RoleVM { RoleId = role.Id, RoleName = role.Name });
        }

        /// <summary>
        /// Persists a role-name change to the Identity store.
        /// </summary>
        /// <param name="model">Role view model with updated role name</param>
        /// <returns>Redirects to ListRoles on success, re-displays form with errors on failure</returns>
        /// <remarks>
        /// Identity validation enforced by RoleManager.UpdateAsync:
        /// - Uniqueness of new role name
        /// - Concurrency conflict detection (optimistic concurrency)
        /// - Role name format validation
        /// The role's NormalizedName is automatically updated by Identity.
        /// </remarks>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(RoleVM model)
        {
            if (!ModelState.IsValid) return View(model);

            var role = await _roleManager.FindByIdAsync(model.RoleId);
            if (role == null) return NotFound();

            // Update role name (NormalizedName auto-updated by Identity)
            role.Name = model.RoleName;
            var result = await _roleManager.UpdateAsync(role);

            if (result.Succeeded)
            {
                TempData["Success"] = "Role updated successfully.";
                return RedirectToAction("ListRoles");
            }

            // Capture concurrency and validation errors
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        #endregion

#region Role Assignment

/// <summary>
/// Renders the role assignment form with context-aware pre-population.
/// </summary>
/// <param name="userId">Optional user ID to pre-fill email (when assigning from UserDetails)</param>
/// <param name="returnTo">Navigation context: "UserDetails" or "RoleDetails"</param>
/// <param name="roleId">Optional role ID to lock role selection (when assigning from RoleUsers)</param>
/// <returns>Assignment form view with contextual pre-population</returns>
/// <remarks>
/// CONTEXT-AWARE WORKFLOW:
/// 1. From UserDetails page (userId provided):
///    - Email is pre-filled from user lookup
///    - Role dropdown is open for selection
///    - Returns to UserDetails after assignment
/// 
/// 2. From RoleUsers page (roleId provided, returnTo="RoleDetails"):
///    - Role is locked and pre-selected
///    - Email field is empty for manual entry
///    - Returns to RoleUsers after assignment
/// 
/// 3. Direct navigation (no context):
///    - Both email and role fields are empty
///    - Returns to ListRoles after assignment
/// </remarks>
public async Task<IActionResult> AssignRoles(string? userId, string? returnTo, string? roleId)
{
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // STEP 1: Pre-fill email when navigating from UserDetails
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    string? prefilledEmail = null;
    if (!string.IsNullOrEmpty(userId))
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();
        prefilledEmail = user.Email;
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // STEP 2: Lock role when navigating from RoleUsers page
    // Prevents changing the role while assigning users to it
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    bool isRoleLocked = returnTo == "RoleDetails" && !string.IsNullOrEmpty(roleId);
    string? preselectedRoleName = null;
    if (isRoleLocked)
    {
        var role = await _roleManager.FindByIdAsync(roleId!);
        preselectedRoleName = role?.Name;
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // STEP 3: Build view model with navigation context
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    var model = new AssignRoleVM
    {
        UserId       = userId,
        Email        = prefilledEmail,
        ReturnTo     = returnTo,
        RoleId       = roleId,
        IsRoleLocked = isRoleLocked,
        RoleName     = preselectedRoleName,
        Roles        = _roleManager.Roles
                        .Select(r => new RoleVM { RoleId = r.Id, RoleName = r.Name })
                        .ToList()
    };

    return View(model);
}

/// <summary>
/// Assigns a role to the user identified by email address.
/// </summary>
/// <param name="model">Assignment view model with email, role, and navigation context</param>
/// <returns>Context-aware redirect on success, form with errors on failure</returns>
/// <remarks>
/// VALIDATION STEPS:
/// 1. Email address presence and user existence
/// 2. Role selection presence
/// 3. Duplicate assignment prevention (IsInRoleAsync check)
/// 4. Identity role assignment with error capture
/// 
/// NAVIGATION:
/// - UserDetails context â†’ Returns to Admin/AccountDetails
/// - RoleDetails context â†’ Returns to RoleUsers
/// - Default â†’ Returns to ListRoles
/// </remarks>
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AssignRoles(AssignRoleVM model)
{
    // Remove Roles collection from validation (repopulated on error)
    ModelState.Remove("Roles");

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // STEP 1: Validate email presence
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    if (string.IsNullOrEmpty(model.Email))
    {
        ModelState.AddModelError("", "Please enter user email.");
        await ReloadRoles(model);
        return View(model);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // STEP 2: Validate role selection
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    if (string.IsNullOrEmpty(model.RoleName))
    {
        ModelState.AddModelError("", "Please select a role.");
        await ReloadRoles(model);
        return View(model);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // STEP 3: Verify user exists
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    var user = await _userManager.FindByEmailAsync(model.Email);
    if (user == null)
    {
        ModelState.AddModelError("", "User not found.");
        await ReloadRoles(model);
        return View(model);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // STEP 4: Prevent duplicate role assignment
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    if (await _userManager.IsInRoleAsync(user, model.RoleName!))
    {
        ModelState.AddModelError("", "User is already assigned to this role.");
        await ReloadRoles(model);
        return View(model);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // STEP 5: Assign role via Identity manager
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    var result = await _userManager.AddToRoleAsync(user, model.RoleName);

    if (result.Succeeded)
    {
        TempData["Success"] = "Role assigned successfully.";

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // STEP 6: Context-aware navigation after success
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

/// <summary>
/// Repopulates the role dropdown on the assignment form after a validation failure.
/// </summary>
/// <param name="model">Assignment view model to update with role list</param>
/// <remarks>
/// Required because the Roles collection is excluded from ModelState validation
/// and must be reloaded to display the form after POST validation errors.
/// </remarks>
private async Task ReloadRoles(AssignRoleVM model)
{
    model.Roles = _roleManager.Roles
        .Select(r => new RoleVM { RoleId = r.Id, RoleName = r.Name })
        .ToList();
}

#endregion

        #region Role Users Management

        /// <summary>
        /// Displays all users assigned to a specific role.
        /// </summary>
        /// <param name="roleId">Identity role ID (GUID string)</param>
        /// <returns>View with list of users in the specified role</returns>
        /// <remarks>
        /// PERFORMANCE OPTIMIZATION:
        /// Uses GetUsersInRoleAsync for a single database query instead of
        /// iterating all users and calling IsInRoleAsync repeatedly.
        /// 
        /// The role ID and name are passed via ViewBag for display and
        /// navigation (e.g., "Add User to Role" button with locked role).
        /// </remarks>
        public async Task<IActionResult> RoleUsers(string roleId)
        {
            if (string.IsNullOrEmpty(roleId)) return NotFound();

            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null) return NotFound();

            // Efficient single query instead of N+1 IsInRoleAsync calls
            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);

            ViewBag.RoleId = role.Id;
            ViewBag.RoleName = role.Name;

            return View(usersInRole);
        }

        /// <summary>
        /// Removes a user from a specified role.
        /// </summary>
        /// <param name="roleId">Identity role ID (GUID string)</param>
        /// <param name="userId">Identity user ID (GUID string)</param>
        /// <returns>Redirects to RoleUsers view to show updated user list</returns>
        /// <remarks>
        /// Delegates to UserManager.RemoveFromRoleAsync for Identity validation.
        /// Returns to RoleUsers page regardless of success to show current state.
        /// </remarks>
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

            // Delegate to Identity for role removal
            var result = await _userManager.RemoveFromRoleAsync(user, role.Name!);

            TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
                ? "User removed from role successfully."
                : "Failed to remove user from role.";

            return RedirectToAction("RoleUsers", new { roleId });
        }

        #endregion

        #region Role Deletion

        /// <summary>
        /// Renders the confirmation page for deleting a role.
        /// </summary>
        /// <param name="roleId">Identity role ID (GUID string)</param>
        /// <returns>Deletion confirmation view with role details</returns>
        public async Task<IActionResult> DeleteRole(string roleId)
        {
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null) return NotFound();

            return View(new RoleVM { RoleId = role.Id, RoleName = role.Name });
        }

        /// <summary>
        /// Deletes a role after validating it's not assigned to any users.
        /// </summary>
        /// <param name="model">Role view model with ID to delete</param>
        /// <returns>Redirects to ListRoles with success or error message</returns>
        /// <remarks>
        /// DELETION SAFETY:
        /// 1. Verifies role exists
        /// 2. Checks if any users are assigned to the role
        /// 3. Prevents deletion if role is in use (referential integrity)
        /// 4. Delegates to RoleManager.DeleteAsync for Identity validation
        /// 
        /// EXCEPTION HANDLING:
        /// Catches unexpected errors (database connectivity, concurrency conflicts)
        /// and provides user-friendly error message instead of 500 error page.
        /// </remarks>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRole(RoleVM model)
        {
            try
            {
                // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                // STEP 1: Verify role exists
                // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                var role = await _roleManager.FindByIdAsync(model.RoleId);
                if (role == null)
                {
                    TempData["Error"] = "Role not found.";
                    return RedirectToAction("ListRoles");
                }

                // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                // STEP 2: Prevent deletion if users are assigned
                // Enforces referential integrity at application level
                // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
                if (usersInRole.Any())
                {
                    TempData["Error"] = "Cannot delete role because it is assigned to users.";
                    return RedirectToAction("ListRoles");
                }

                // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                // STEP 3: Delete role via Identity manager
                // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                var result = await _roleManager.DeleteAsync(role);
                TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
                    ? "Role deleted successfully."
                    : "Failed to delete role.";

                return RedirectToAction("ListRoles");
            }
            catch
            {
                // Catch unexpected errors (DB connectivity, concurrency conflicts)
                TempData["Error"] = "An unexpected error occurred while deleting the role.";
                return RedirectToAction("ListRoles");
            }
        }

        #endregion
    }
}
