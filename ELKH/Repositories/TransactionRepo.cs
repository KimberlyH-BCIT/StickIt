using ELKH.Data;
using ELKH.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Repositories
{
    /// <summary>
    /// Repository for read-only transaction queries used by admin listing views.
    /// Projects directly to <see cref="TransactionVM"/> to avoid loading full entity graphs.
    /// </summary>
    public class TransactionRepo
    {
        private readonly ApplicationDbContext _context;

        public TransactionRepo(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Returns all transactions projected to <see cref="TransactionVM"/>.
        /// Eager-loads <c>Order</c> and <c>ContactDetail</c> in the same query so the
        /// SELECT projection can read related fields without issuing additional round-trips.
        /// </summary>
        public async Task<List<TransactionVM>> GetAllTransactions()
        {
            return await _context.Transactions
                .Include(t => t.Order)
                .Include(t => t.ContactDetail)
                .Select(t => new TransactionVM
                {
                    PkTransactionId = t.PkTransactionId,
                    TransactionStatus = t.TransactionStatus,
                    Amount = t.Amount,
                    TransactionDate = t.TransactionDate,
                    DeliberyFee = t.DeliveryFee,
                    FkOrderId = t.FkOrderId
                }).ToListAsync();
        }
    }
}
