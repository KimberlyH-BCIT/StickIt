using Microsoft.AspNetCore.Identity;
using ELKH.Data;
using ELKH.ViewModels;

namespace ELKH.Repositories
{
    public class RoleRepository : IRoleRepository
    {
        private readonly ApplicationDbContext _context;

        public RoleRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<RoleVM> GetAllRoles()
        {
            return _context.Roles
                .Select(r => new RoleVM
                {
                    RoleId   = r.Id,
                    RoleName = r.Name
                })
                .ToList();
        }

        public RoleVM GetRoleById(string roleId)
        {
            var role = _context.Roles.FirstOrDefault(r => r.Id == roleId);
            if (role == null) return null!;

            return new RoleVM
            {
                RoleId   = role.Id,
                RoleName = role.Name
            };
        }

        public void CreateRole(RoleVM role)
        {
            _context.Roles.Add(new IdentityRole { Name = role.RoleName });
            _context.SaveChanges();
        }

        public void UpdateRole(RoleVM role)
        {
            var existing = _context.Roles.FirstOrDefault(r => r.Id == role.RoleId);
            if (existing != null)
            {
                existing.Name = role.RoleName;
                _context.SaveChanges();
            }
        }

        public void DeleteRole(string roleId)
        {
            var role = _context.Roles.FirstOrDefault(r => r.Id == roleId);
            if (role != null)
            {
                _context.Roles.Remove(role);
                _context.SaveChanges();
            }
        }
    }
}
