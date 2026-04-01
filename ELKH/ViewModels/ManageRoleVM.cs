namespace ELKH.ViewModels;

/// <summary>
/// View model for role management operations providing role collection management
/// and administrative interface support for role-based access control.
/// </summary>
public class ManageRoleVM
{
    public List<RoleVM> Roles { get; set; } = [];
}
