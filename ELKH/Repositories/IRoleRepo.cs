using ELKH.ViewModels;

namespace ELKH.Repositories
{
    /// <summary>
    /// Repository interface for role management operations providing data access methods for
    /// managing user roles, role assignments, and role-related CRUD operations.
    /// </summary>
    public interface IRoleRepo
    {
        List<RoleVM> GetAllRoles();
        RoleVM GetRoleById(string roleId);
        Task CreateRoleAsync(RoleVM role);
        Task UpdateRoleAsync(RoleVM role);
        Task DeleteRoleAsync(string roleId);
    }
}
