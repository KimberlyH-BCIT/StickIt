using ELKH.ViewModels;

namespace ELKH.Repositories
{
    public interface IRoleRepository
    {
        List<RoleVM> GetAllRoles();
        RoleVM GetRoleById(string roleId);
        Task CreateRoleAsync(RoleVM role);
        Task UpdateRoleAsync(RoleVM role);
        Task DeleteRoleAsync(string roleId);
    }
}
