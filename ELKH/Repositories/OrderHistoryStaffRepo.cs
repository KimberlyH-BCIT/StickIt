using ELKH.Data;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ELKH.Repositories
{
    /// <summary>
    /// Repository for staff order management operations.
    /// Provides filtered, paginated order listings, delivery status updates,
    /// order detail retrieval, and order count queries by status.
    /// </summary>
    public class OrderHistoryStaffRepo
    {
        private readonly ApplicationDbContext _context;

        public OrderHistoryStaffRepo(ApplicationDbContext context)
        {
            _context = context;
        }

        // UPDATED: 'status' parameter changed to DeliveryStatus? Enum
        public async Task<PagedResult<OrderDetailsVM>> GetAllOrders(string? searchString, int page = 1, int pageSize = 10, DeliveryStatus? status = null)
        {
            var query = _context.Orders.Include(o => o.RegisteredUser).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(o => o.RegisteredUser != null && o.RegisteredUser.Email.Contains(searchString) || o.PkOrderId.ToString(CultureInfo.InvariantCulture).Contains(searchString));
            }

            // FIXED: Comparing Enum to Enum instead of string
            if (status.HasValue)
            {
                query = query.Where(o => o.DeliveryStatus == status.Value);
            }

            var countOrders = await query.CountAsync();

            var orders = await query
                                    .OrderByDescending(o => o.PkOrderId)
                                    .Skip((page - 1) * pageSize)
                                    .Take(pageSize)
                                    .ToListAsync();

            var vm = orders.Select(o => new OrderDetailsVM
            {
                OrderId = o.PkOrderId,
                UserEmail = o.RegisteredUser?.Email ?? string.Empty,
                // Ensure OrderDetailsVM.DeliveryStatus is updated to the Enum type or use .ToString()
                DeliveryStatus = o.DeliveryStatus.ToString()
            }).ToList();

            return new PagedResult<OrderDetailsVM>
            {
                Items = vm,
                PageSize = pageSize,
                CurrentPage = page,
                TotalItems = countOrders
            };
        }

        // UPDATED: Parameter changed to DeliveryStatus Enum
        public async Task<int> GetCountByStatus(DeliveryStatus status)
        {
            return await _context.Orders
                                 .Where(o => o.DeliveryStatus == status)
                                 .CountAsync();
        }

        public async Task<PagedResult<OrderDetailsVM>> OrderDetails(int? orderId, int? transactionId, string? searchString, int page = 1, int pageSize = 10)
        {
            int actualOrderId = 0;
            if (orderId.HasValue)
            {
                actualOrderId = orderId.Value;
            }
            else if (transactionId.HasValue)
            {
                var trans = await _context.Transactions.FirstOrDefaultAsync(t => t.PkTransactionId == transactionId);
                actualOrderId = trans?.FkOrderId ?? 0;
            }

            var query = _context.OrderItems.Include(oi => oi.Product)
                                           .Include(oi => oi.Order!)
                                           .ThenInclude(o => o.RegisteredUser!)
                                           .Include(oi => oi.Order!)
                                           .ThenInclude(o => o.Transaction!)
                                           .Where(oi => oi.FkOrderId == actualOrderId);

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(oi => oi.Product != null
                                    && oi.Product.Name.Contains(searchString)
                                    && oi.Product.Description.Contains(searchString));
            }

            var totalItems = await query.CountAsync();

            var items = (await query.OrderBy(oi => oi.PkOrderItemId)
                                    .Skip((page - 1) * pageSize)
                                    .Take(pageSize)
                                    .ToListAsync())
                        .Select(oi => new OrderDetailsVM
                        {
                            UserEmail = oi.Order?.RegisteredUser?.Email ?? string.Empty,
                            // Update these mapping based on your RegisteredUserModel fields
                            FirstName = "User",
                            LastName = "Name",
                            Address = "Address placeholder",
                            OrderId = oi.Order?.PkOrderId ?? 0,
                            TransactionId = oi.Order?.Transaction?.PkTransactionId ?? 0,
                            DeliveryStatus = oi.Order?.DeliveryStatus.ToString() ?? string.Empty, // Converted to string for VM
                            ProductName = oi.Product?.Name ?? string.Empty,
                            Quantity = oi.Quantity,
                            UnitPrice = oi.Product?.Price ?? 0,
                            TotalOrderAmount = oi.Order?.TotalAmount ?? 0
                        })
                        .ToList();

            return new PagedResult<OrderDetailsVM>
            {
                Items = items,
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems,
            };
        }

        // UPDATED: Changed newStatus from string to DeliveryStatus Enum
        public async Task<bool> UpdateOrderStatus(int orderId, DeliveryStatus newStatus)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.PkOrderId == orderId);

            if (order == null) return false;

            // FIXED: Assigning Enum directly
            order.DeliveryStatus = newStatus;

            _context.Entry(order).State = EntityState.Modified;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
