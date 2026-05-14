using ELKH.Data;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELKH.Repositories
{
    /// <summary>
    /// Repository for order management and projections.
    /// Uses projection-based queries for admin views rather than standard CRUD.
    /// </summary>
    public class OrderManagementRepo : RepositoryBase<OrderModel, int>, IOrderManagementRepo
    {
        public OrderManagementRepo(ApplicationDbContext context, ILogger<OrderManagementRepo> logger)
            : base(context, logger)
        {
        }

        /// <summary>
        /// Get all orders as view models for admin listing.
        /// </summary>
        public async Task<IEnumerable<OrderDetailsVM>> GetAllOrdersAsync()
        {
            return await Context.Orders
                .Include(o => o.RegisteredUser)
                .Select(o => new OrderDetailsVM
                {
                    OrderId = o.PkOrderId,
                    UserEmail = o.RegisteredUser!.Email,
                    // FIXED: Convert Enum to string for the ViewModel
                    DeliveryStatus = o.DeliveryStatus.ToString()
                })
                .ToListAsync();
        }

        /// <summary>
        /// Get order details for a specific user.
        /// </summary>
        public async Task<IEnumerable<OrderDetailsVM>> OrderDetailsAsync(string email)
        {
            return await Context.Orders
                .Include(o => o.RegisteredUser)
                .Where(o => o.RegisteredUser!.Email == email)
                .Select(o => new OrderDetailsVM
                {
                    OrderId = o.PkOrderId,
                    UserEmail = o.RegisteredUser!.Email,
                    // FIXED: Convert Enum to string for the ViewModel
                    DeliveryStatus = o.DeliveryStatus.ToString()
                })
                .ToListAsync();
        }

        /// <summary>
        /// Get all orders as full OrderModel entities ordered by creation date (admin history view).
        /// </summary>
        public async Task<IEnumerable<OrderModel>> GetAllOrderModelsAsync()
        {
            return await Context.Orders
                .AsNoTracking()
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Get orders belonging to a specific user by email, ordered newest first.
        /// Eagerly loads OrderItems and their Products so the history view can
        /// display item counts and render "Buy it again" buttons.
        /// </summary>
        public async Task<IEnumerable<OrderModel>> GetUserOrdersAsync(string userEmail)
        {
            return await Context.Orders
                .AsNoTracking()
                .Include(o => o.RegisteredUser)
                .Where(o => o.RegisteredUser!.Email == userEmail)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Get a single order by ID with OrderItems and RegisteredUser eagerly loaded.
        /// Returns null if the order does not exist.
        /// </summary>
        public async Task<OrderModel?> GetOrderWithDetailsAsync(int orderId)
        {
            return await Context.Orders
                .AsNoTracking()
                .Include(o => o.OrderItems)
                .Include(o => o.RegisteredUser)
                .FirstOrDefaultAsync(o => o.PkOrderId == orderId);
        }
    }
}
