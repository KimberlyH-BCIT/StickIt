using ELKH.Data;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Repositories
{
    /// <summary>
    /// Repository implementation for role management operations providing concrete data access methods
    /// for managing user roles, role assignments, and role-related CRUD operations using Entity Framework.
    /// </summary>
    public class RoleRepo : IRoleRepo
    {
        private readonly ApplicationDbContext _context;

        public RoleRepo(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<RoleVM> GetAllRoles()
            => _context.Roles
                .Select(r => new RoleVM { RoleId = r.Id, RoleName = r.Name })
                .ToList();

        public RoleVM GetRoleById(string roleId)
        {
            var role = _context.Roles.FirstOrDefault(r => r.Id == roleId);
            if (role == null) return null!;
            return new RoleVM { RoleId = role.Id, RoleName = role.Name };
        }

        public async Task CreateRoleAsync(RoleVM role)
        {
            _context.Roles.Add(new IdentityRole { Name = role.RoleName });
            await _context.SaveChangesAsync();
        }

        public async Task UpdateRoleAsync(RoleVM role)
        {
            var existing = await _context.Roles.FirstOrDefaultAsync(r => r.Id == role.RoleId);
            if (existing is null) return;
            existing.Name = role.RoleName;
            await _context.SaveChangesAsync();
        }

        public async Task DeleteRoleAsync(string roleId)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == roleId);
            if (role is null) return;
            _context.Roles.Remove(role);
            await _context.SaveChangesAsync();
        }
    }
}
