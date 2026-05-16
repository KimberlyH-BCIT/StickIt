using Microsoft.AspNetCore.Identity;

namespace ELKH.Services;

public sealed class AdminUserListService(UserManager<IdentityUser> userManager) : IAdminUserListService
{
    public async Task<AdminUserListResultVM> BuildAsync(string? search, string? roleFilter, int page, int pageSize = 5, CancellationToken ct = default)
    {
        pageSize = Math.Max(1, pageSize);
        page = Math.Max(1, page);

        IList<IdentityUser> candidates;
        bool hasRoleFilter = !string.IsNullOrEmpty(roleFilter) && roleFilter != "All";

        if (hasRoleFilter)
        {
            candidates = await userManager.GetUsersInRoleAsync(roleFilter!);

            if (!string.IsNullOrEmpty(search))
            {
                candidates = candidates
                    .Where(u => u.Email?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                    .ToList();
            }
        }
        else
        {
            IQueryable<IdentityUser> query = userManager.Users;
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => u.Email != null && u.Email.Contains(search));
            }

            candidates = await query.ToListAsync(ct);
        }

        var totalUsers = candidates.Count;
        var pageUsers = candidates
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var userList = new List<UserListVM>(pageUsers.Count);
        foreach (var user in pageUsers)
        {
            var roles = await userManager.GetRolesAsync(user);
            userList.Add(new UserListVM
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                Roles = roles.ToList()
            });
        }

        return new AdminUserListResultVM
        {
            Users = userList,
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling(totalUsers / (double)pageSize)
        };
    }
}
