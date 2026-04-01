using ELKH.Data;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Repositories
{
    public class OrderHistoryManagementRepo : IOrderHistoryManagementRepo
    {
        private ApplicationDbContext _context;

        public OrderHistoryManagementRepo(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<OrderModel>> GetAllOrders()
        {
            return await _context.Orders
                .Include(o => o.RegisteredUser)
                .ToListAsync();
        }

        public async Task<OrderModel?> OrderDetails(string email, int orderId)
        {
            return await _context.Orders
                .Where(o => o.PkOrderId == orderId && o.RegisteredUser!.Email == email)
                .Include(o => o.Transaction)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync();
        }

        public async Task<OrderModel?> GetByIdAsync(int orderId)
        {
            return await _context.Orders
                .Where(o => o.PkOrderId == orderId)
                .Include(o => o.RegisteredUser)
                .Include(o => o.Transaction)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// UPDATED: Parameter 'deliveryStatus' changed from string to DeliveryStatus Enum.
        /// </summary>
        public async Task<OrderModel?> UpdateDeliveryStatusAsync(int orderId, DeliveryStatus deliveryStatus)
        {
            var order = await _context.Orders
                .Include(o => o.RegisteredUser)
                .FirstOrDefaultAsync(o => o.PkOrderId == orderId);

            if (order is null) return null;

            // FIXED: Assigning Enum directly
            order.DeliveryStatus = deliveryStatus;

            // FIXED: Switch now evaluates Enum values instead of strings
            order.OrderStatus = deliveryStatus switch
            {
                DeliveryStatus.Shipped => OrderStatus.Shipped,
                // Note: If you removed 'Delivered' from your Enum earlier, 
                // this line can be removed or mapped to Shipped as well.
                _ => order.OrderStatus
            };

            await _context.SaveChangesAsync();
            return order;
        }
    }
}