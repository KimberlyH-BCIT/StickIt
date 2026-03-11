using ELKH.ViewModels;

namespace ELKH.Repositories
{
    public interface IRoleRepository
    {
        List<RoleVM> GetAllRoles();
        RoleVM GetRoleById(string roleId);
        void CreateRole(RoleVM role);
        void UpdateRole(RoleVM role);
        void DeleteRole(string roleId);
    }
}
