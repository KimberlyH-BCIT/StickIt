using Microsoft.AspNetCore.Identity;

namespace ELKH.Services;

/// <summary>
/// Coordinates admin user role membership workflows.
/// </summary>
public interface IAdminUserRoleService
{
    Task<RoleUsersResult?> GetRoleUsersAsync(string roleId, CancellationToken ct = default);

    Task<RoleMembershipResult> RemoveUserFromRoleAsync(string roleId, string userId, CancellationToken ct = default);

    Task<RoleMembershipResult> RemoveUserFromRoleByNameAsync(string userId, string role, CancellationToken ct = default);

    Task<RoleMembershipResult> AddUserToRoleAsync(string userId, string role, CancellationToken ct = default);

    Task<IReadOnlyList<string>?> GetAvailableRolesAsync(string userId, CancellationToken ct = default);
}

public sealed record RoleUsersResult(string RoleId, string RoleName, List<IdentityUser> Users);

public sealed record RoleMembershipResult(bool Succeeded, string? ErrorMessage, string? UserEmail, string? RoleName);
