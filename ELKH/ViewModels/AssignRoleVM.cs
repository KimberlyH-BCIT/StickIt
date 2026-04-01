namespace ELKH.ViewModels;

/// <summary>
/// View model for role assignment operations providing user and role selection
/// functionality for administrative role management interfaces.
/// </summary>
public class AssignRoleVM
{
    [EmailAddress]
    public string? Email { get; set; }

    public string? RoleName { get; set; }

    public bool IsRoleLocked { get; set; }

    public List<RoleVM> Roles { get; set; } = [];

    public string? ReturnTo { get; set; }
    public string? UserId { get; set; }   // nullable - not always known (e.g. from ListRoles)
    public string? RoleId { get; set; }   // nullable - not always known (e.g. from AccountDetails)
}
