using ELKH.ViewModels;

namespace ELKH.Repositories;

public interface ITransactionRepo
{
    Task<List<TransactionVM>> GetAllTransactions();
}
