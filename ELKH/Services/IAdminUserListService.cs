using ELKH.ViewModels;

namespace ELKH.Services;

public interface IAdminUserListService
{
    Task<AdminUserListResultVM> BuildAsync(string? search, string? roleFilter, int page, int pageSize = 5, CancellationToken ct = default);
}

public sealed class AdminUserListResultVM
{
    public List<UserListVM> Users { get; set; } = [];

    public int CurrentPage { get; set; }

    public int TotalPages { get; set; }
}
