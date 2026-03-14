using ELKH.Data;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace ELKH.Repositories
{
    public class OrderHistoryStaffRepo
    {
        private readonly ApplicationDbContext _context;

        public OrderHistoryStaffRepo(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<OrderDetailsVM>> GetAllOrders()
        {
            var orders = await _context.Orders.Include(o => o.RegisteredUser)
                                              .ToListAsync();
            var vm = orders.Select(o => new OrderDetailsVM
            {
                OrderId = o.PkOrderId,
                UserEmail = o.RegisteredUser.Email,
                DeliveryStatus = o.DeliveryStatus
            }).ToList();

            return vm;
        }

        public async Task<OrderDetailsVM> OrderDetails(int? orderId, int? transactionId)
        {
            if (orderId.HasValue)
            {
                var orderDetails = await _context.Orders.Include(o => o.RegisteredUser)
                                                        .Include(o => o.Transaction)
                                                        .Include(o => o.OrderItems)
                                                        .ThenInclude(oi => oi.Product)
                                                        .FirstOrDefaultAsync(o => o.PkOrderId == orderId);

                var ordervm = new OrderDetailsVM
                {
                    OrderId = orderId ?? 0,
                    TransactionId = orderDetails.Transaction.PkTransactionId,
                    OrderItems = orderDetails.OrderItems.Select(oi => new OrderItemVM
                    {
                        ProductId = oi.Product.PkProductId,
                        Quantity = oi.Quantity,
                        ProductName = oi.Product.Name,
                        ProductPrice = oi.Product.Price
                    }).ToList()
                };

                return ordervm;

            }
            var transOrderDetails = await _context.Transactions.Include(t => t.Order)
                                                               .ThenInclude(o => o.RegisteredUser)
                                                               .Include(o => o.Order)
                                                               .ThenInclude(o => o.OrderItems)
                                                               .FirstOrDefaultAsync(t => t.PkTransactionId == transactionId);

            var vm = new OrderDetailsVM
            {
                OrderId = transOrderDetails.Order.PkOrderId,
                TransactionId = transactionId ?? 0,
                OrderItems = transOrderDetails.Order.OrderItems.Select(o => new OrderItemVM
                {
                    ProductId = o.Product.PkProductId,
                    Quantity = o.Quantity,
                    ProductName = o.Product.Name,
                    ProductPrice = o.Product.Price
                }).ToList()
            };

            return vm;
        }
    }
}
