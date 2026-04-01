using ELKH.ViewModels;

namespace ELKH.Repositories;

/// <summary>
/// Repository interface for transaction operations providing data access methods for
/// managing financial transactions, payment processing, and transaction history.
/// </summary>
public interface ITransactionRepo
{
    Task<List<TransactionVM>> GetAllTransactions();
}
