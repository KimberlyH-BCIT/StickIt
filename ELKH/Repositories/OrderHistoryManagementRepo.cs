using ELKH.Data;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Repositories
{
    /// <summary>
    /// Data access layer for the order history staff/admin views.
    /// Provides queries that eager-load only the navigation properties
    /// required by each view, avoiding unnecessary over-fetching.
    /// </summary>
    public class OrderHistoryManagementRepo
    {
        private ApplicationDbContext _context;

        public OrderHistoryManagementRepo(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Returns all orders with their associated registered user.
        /// Used by the admin summary listing — only <see cref="OrderModel.RegisteredUser"/>
        /// is included because the listing view needs email and delivery status only.
        /// </summary>
        public async Task<IEnumerable<OrderModel>> GetAllOrders()
        {
            return await _context.Orders
                .Include(o => o.RegisteredUser)
                .ToListAsync();
        }

        /// <summary>
        /// Returns a single order with full detail for the given user and order ID.
        /// Eager-loads <see cref="OrderModel.Transaction"/>, order items, and each
        /// item's product so the detail view can render line items without extra queries.
        /// Returns <c>null</c> when no matching order exists for the supplied credentials.
        /// </summary>
        /// <param name="email">Email of the signed-in user; used to scope the query to their orders.</param>
        /// <param name="orderId">Primary key of the order to retrieve.</param>
        public async Task<OrderModel?> OrderDetails(string email, int orderId)
        {
            return await _context.Orders
                .Where(o => o.PkOrderId == orderId && o.RegisteredUser.Email == email)
                .Include(o => o.Transaction)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Returns a single order by primary key for admin viewing, with full detail:
        /// transaction, order items, each item's product, and the registered user.
        /// No email scoping is applied — this is intentionally an admin-only query.
        /// Returns <c>null</c> when no order with <paramref name="orderId"/> exists.
        /// </summary>
        /// <param name="orderId">Primary key of the order to retrieve.</param>
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
        /// Updates the delivery status of an order and returns the full order (with
        /// <see cref="OrderModel.RegisteredUser"/> included) so the caller can send a
        /// status-change notification email without issuing a second query.
        /// Returns <c>null</c> when no order with <paramref name="orderId"/> exists.
        /// </summary>
        /// <param name="orderId">Primary key of the order to update.</param>
        /// <param name="deliveryStatus">The new delivery status value (e.g. "Shipped", "Delivered").</param>
        public async Task<OrderModel?> UpdateDeliveryStatusAsync(int orderId, string deliveryStatus)
        {
            var order = await _context.Orders
                .Include(o => o.RegisteredUser)
                .FirstOrDefaultAsync(o => o.PkOrderId == orderId);

            if (order is null) return null;

            order.DeliveryStatus = deliveryStatus;

            // Mirror a human-readable OrderStatus so the customer-facing history badge stays
            // in sync with the delivery state set by staff. Only update for customer-visible
            // milestones (Shipped / Delivered); all other transitions leave OrderStatus unchanged.
            order.OrderStatus = deliveryStatus switch
            {
                "Shipped"   => "Shipped",
                "Delivered" => "Delivered",
                _           => order.OrderStatus
            };

            await _context.SaveChangesAsync();
            return order;
        }
    }
}

