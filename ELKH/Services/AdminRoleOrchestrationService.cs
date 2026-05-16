using Microsoft.AspNetCore.Identity;
using ELKH.ViewModels;

namespace ELKH.Services;

public sealed class AdminRoleOrchestrationService(
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager) : IAdminRoleOrchestrationService
{
    public Task<List<RoleVM>> GetRolesAsync(CancellationToken ct = default)
    {
        return Task.FromResult(roleManager.Roles
            .Select(r => new RoleVM { RoleId = r.Id, RoleName = r.Name })
            .ToList());
    }

    public async Task<RoleVM?> GetRoleByIdAsync(string roleId, CancellationToken ct = default)
    {
        var role = await roleManager.FindByIdAsync(roleId);
        return role == null ? null : new RoleVM(role.Id, role.Name);
    }

    public async Task<AssignRoleVM?> BuildAssignRoleVmAsync(string? userId, string? returnTo, string? roleId, CancellationToken ct = default)
    {
        string? prefilledEmail = null;
        if (!string.IsNullOrEmpty(userId))
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return null;
            }

            prefilledEmail = user.Email;
        }

        var isRoleLocked = returnTo == "RoleDetails" && !string.IsNullOrEmpty(roleId);
        string? preselectedRoleName = null;
        if (isRoleLocked)
        {
            var role = await roleManager.FindByIdAsync(roleId!);
            preselectedRoleName = role?.Name;
        }

        return new AssignRoleVM
        {
            UserId = userId,
            Email = prefilledEmail,
            ReturnTo = returnTo,
            RoleId = roleId,
            IsRoleLocked = isRoleLocked,
            RoleName = preselectedRoleName,
            Roles = await GetRolesAsync(ct)
        };
    }

    public async Task<RoleAssignmentResult> AssignRoleAsync(AssignRoleVM model, CancellationToken ct = default)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(model.Email))
        {
            errors.Add("Please enter user email.");
        }

        if (string.IsNullOrEmpty(model.RoleName))
        {
            errors.Add("Please select a role.");
        }

        if (errors.Count > 0)
        {
            return new RoleAssignmentResult(false, null, errors);
        }

        var user = await userManager.FindByEmailAsync(model.Email!);
        if (user == null)
        {
            return new RoleAssignmentResult(false, "User not found.", errors);
        }

        if (await userManager.IsInRoleAsync(user, model.RoleName!))
        {
            return new RoleAssignmentResult(false, "User is already assigned to this role.", errors);
        }

        var result = await userManager.AddToRoleAsync(user, model.RoleName!);
        if (result.Succeeded)
        {
            return new RoleAssignmentResult(true, null, errors);
        }

        return new RoleAssignmentResult(false, string.Join(", ", result.Errors.Select(e => e.Description)), errors);
    }

    public async Task<RoleDeletionResult> DeleteRoleAsync(string roleId, CancellationToken ct = default)
    {
        var role = await roleManager.FindByIdAsync(roleId);
        if (role == null)
        {
            return new RoleDeletionResult(false, "Role not found.");
        }

        var usersInRole = await userManager.GetUsersInRoleAsync(role.Name!);
        if (usersInRole.Any())
        {
            return new RoleDeletionResult(false, "Cannot delete role because it is assigned to users.");
        }

        var result = await roleManager.DeleteAsync(role);
        if (result.Succeeded)
        {
            return new RoleDeletionResult(true, null);
        }

        return new RoleDeletionResult(false, string.Join(", ", result.Errors.Select(e => e.Description)));
    }
}
