using ELKH.ViewModels;

namespace ELKH.Services;

/// <summary>
/// Builds paged admin user list results for search and role-filter scenarios.
/// </summary>
public interface IAdminUserListService
{
    Task<AdminUserListResultVM> BuildAsync(string? search, string? roleFilter, int page, int pageSize = 5, CancellationToken ct = default);
}

public sealed class AdminUserListResultVM
{
    /// <summary>
    /// Gets or sets the users included in the current page of results.
    /// </summary>
    public List<UserListVM> Users { get; set; } = [];

    /// <summary>
    /// Gets or sets the current page number.
    /// </summary>
    public int CurrentPage { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages available.
    /// </summary>
    public int TotalPages { get; set; }
}
