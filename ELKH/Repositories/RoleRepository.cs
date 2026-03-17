using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
