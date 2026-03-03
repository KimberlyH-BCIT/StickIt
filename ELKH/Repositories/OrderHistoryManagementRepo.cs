using ELKH.Data;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Repositories
{
    public class OrderHistoryManagementRepo
    {
        private ApplicationDbContext _context;
        public OrderHistoryManagementRepo(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<IEnumerable<Order>> GetAllOrders()
        {
            var orders = await _context.Orders
                                 .Include(o => o.RegisteredUser)
                                 .ToListAsync();
            return orders;
        }

        public async Task<Order?> OrderDetails(string email, int orderId )
        {
            var orderDetails = await _context.Orders
                                      .Where(o => o.PkOrderId == orderId && o.RegisteredUser.Email == email)
                                      .Include(o => o.Transaction)
                                      .Include(o => o.OrderItems)
                                      .ThenInclude(oi => oi.Products)
                                      .FirstOrDefaultAsync();
            return orderDetails;
        }
    }
}
