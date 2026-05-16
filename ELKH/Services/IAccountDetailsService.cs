using ELKH.ViewModels;

namespace ELKH.Services;

public interface IAccountDetailsService
{
    Task<AccountDetailsVM?> BuildAsync(string identityUserId, CancellationToken ct = default);
}
