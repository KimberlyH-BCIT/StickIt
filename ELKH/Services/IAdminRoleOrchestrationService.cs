using ELKH.ViewModels;

namespace ELKH.Services;

/// <summary>
/// Coordinates role listing, assignment, and deletion workflows for the admin area.
/// </summary>
public interface IAdminRoleOrchestrationService
{
    Task<List<RoleVM>> GetRolesAsync(CancellationToken ct = default);

    Task<RoleVM?> GetRoleByIdAsync(string roleId, CancellationToken ct = default);

    Task<AssignRoleVM?> BuildAssignRoleVmAsync(string? userId, string? returnTo, string? roleId, CancellationToken ct = default);

    Task<RoleAssignmentResult> AssignRoleAsync(AssignRoleVM model, CancellationToken ct = default);

    Task<RoleDeletionResult> DeleteRoleAsync(string roleId, CancellationToken ct = default);
}

public sealed record RoleAssignmentResult(bool Succeeded, string? ErrorMessage, List<string> ModelErrors);

public sealed record RoleDeletionResult(bool Succeeded, string? ErrorMessage);
