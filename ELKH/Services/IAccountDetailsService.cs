using ELKH.ViewModels;

namespace ELKH.Services;

/// <summary>
/// Builds the account details view model for the authenticated user.
/// </summary>
public interface IAccountDetailsService
{
    Task<AccountDetailsVM?> BuildAsync(string identityUserId, CancellationToken ct = default);
}
