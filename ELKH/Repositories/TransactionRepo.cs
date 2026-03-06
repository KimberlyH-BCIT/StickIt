using ELKH.Data;
using ELKH.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Repositories
{
    public class TransactionRepo
    {
        private readonly ApplicationDbContext _context;
        public TransactionRepo(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<TransactionVM>> GetAllTransactions()
        {
            return await _context.Transactions.Include(t => t.FkContactId)
                                                          .Include(t=>t.FkOrderId)
                                                          .Select(t=> new TransactionVM
                                                          {
                                                              PkTransactionID = t.PkTransactionId,
                                                              PkOrderId = t.Order.PkOrderId,
                                                              TotalAmount = t.Order.TotalAmount,
                                                              FirstName = t.ContactDetail.FirstName
                                                          }).ToListAsync();
        }
    }
}
