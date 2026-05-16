using Microsoft.AspNetCore.Identity;

namespace ELKH.Services;

public sealed class AdminUserRoleService(
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager) : IAdminUserRoleService
{
    public async Task<RoleUsersResult?> GetRoleUsersAsync(string roleId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(roleId))
        {
            return null;
        }

        var role = await roleManager.FindByIdAsync(roleId);
        if (role == null || string.IsNullOrWhiteSpace(role.Name))
        {
            return null;
        }

        var users = await userManager.GetUsersInRoleAsync(role.Name);
        return new RoleUsersResult(role.Id, role.Name, users.ToList());
    }

    public async Task<RoleMembershipResult> RemoveUserFromRoleAsync(string roleId, string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(roleId) || string.IsNullOrWhiteSpace(userId))
        {
            return new RoleMembershipResult(false, "Invalid role or user.", null, null);
        }

        var role = await roleManager.FindByIdAsync(roleId);
        var user = await userManager.FindByIdAsync(userId);
        if (role == null || string.IsNullOrWhiteSpace(role.Name) || user == null)
        {
            return new RoleMembershipResult(false, "Role or user not found.", user?.Email, role?.Name);
        }

        var result = await userManager.RemoveFromRoleAsync(user, role.Name);
        return result.Succeeded
            ? new RoleMembershipResult(true, null, user.Email, role.Name)
            : new RoleMembershipResult(false, string.Join(", ", result.Errors.Select(e => e.Description)), user.Email, role.Name);
    }

    public async Task<RoleMembershipResult> RemoveUserFromRoleByNameAsync(string userId, string role, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(role))
        {
            return new RoleMembershipResult(false, "Invalid role or user.", null, role);
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return new RoleMembershipResult(false, "User not found.", null, role);
        }

        var result = await userManager.RemoveFromRoleAsync(user, role);
        return result.Succeeded
            ? new RoleMembershipResult(true, null, user.Email, role)
            : new RoleMembershipResult(false, string.Join(", ", result.Errors.Select(e => e.Description)), user.Email, role);
    }

    public async Task<RoleMembershipResult> AddUserToRoleAsync(string userId, string role, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(role))
        {
            return new RoleMembershipResult(false, "Invalid role or user.", null, role);
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return new RoleMembershipResult(false, "User not found.", null, role);
        }

        var roleEntity = await roleManager.FindByNameAsync(role);
        if (roleEntity == null)
        {
            return new RoleMembershipResult(false, "Role not found.", user.Email, role);
        }

        if (await userManager.IsInRoleAsync(user, role))
        {
            return new RoleMembershipResult(false, $"User already has the {role} role.", user.Email, role);
        }

        var result = await userManager.AddToRoleAsync(user, role);
        return result.Succeeded
            ? new RoleMembershipResult(true, null, user.Email, role)
            : new RoleMembershipResult(false, string.Join(", ", result.Errors.Select(e => e.Description)), user.Email, role);
    }

    public async Task<IReadOnlyList<string>?> GetAvailableRolesAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return null;
        }

        var userRoles = await userManager.GetRolesAsync(user);
        return await roleManager.Roles
            .Select(r => r.Name ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name) && !userRoles.Contains(name))
            .ToListAsync(ct);
    }
}
