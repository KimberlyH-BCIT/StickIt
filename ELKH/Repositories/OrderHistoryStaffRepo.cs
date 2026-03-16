using ELKH.Data;
using ELKH.Models;
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

        public async Task<PagedResult<OrderDetailsVM>> GetAllOrders(string? searchString,int page = 1, int pageSize = 10)
        {

            var query = _context.Orders.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = _context.Orders.Where(o => o.RegisteredUser.Email.Contains(searchString) || o.PkOrderId.ToString().Contains(searchString));
            }

            var countOrders = await query.CountAsync();

            var orders = await query
                                    .Include(o => o.RegisteredUser)
                                    .OrderBy(o => o.PkOrderId)           
                                    .Skip((page - 1) * pageSize)        
                                    .Take(pageSize)                       
                                    .ToListAsync();

            var vm = orders.Select(o => new OrderDetailsVM
            {
                OrderId = o.PkOrderId,
                UserEmail = o.RegisteredUser.Email,
                DeliveryStatus = o.DeliveryStatus
            }).ToList();

            var result = new PagedResult<OrderDetailsVM>
            {
                Items = vm,
                PageSize = pageSize,
                CurrentPage = page,
                TotalItems = countOrders
            };

            return result;
        }

        public async Task<PagedResult<OrderDetailsVM>> OrderDetails(int? orderId, int? transactionId,string? searchString, int page = 1, int pageSize = 10)
        {
            int actualOrderId = 0;
            if (orderId.HasValue)
            {
                actualOrderId = orderId.Value;
            }else if (transactionId.HasValue)
            {
                var trans = await _context.Transactions.FirstOrDefaultAsync(t => t.PkTransactionId == transactionId);
                actualOrderId = trans?.FkOrderId ?? 0;
            }

            var query = _context.OrderItems.Include(oi => oi.Product)
                                           .Include(oi => oi.Order)
                                           .ThenInclude(o => o.RegisteredUser)
                                           .Include(oi => oi.Order)
                                           .ThenInclude(o => o.Transaction)
                                           .Where(oi => oi.FkOrderId == actualOrderId);

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(oi => oi.Product.Name.Contains(searchString)
                                    || oi.Product.Description.Contains(searchString));
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var items = await query.OrderBy(oi => oi.PkOrderItemId)
                                   .Skip((page - 1) * pageSize)
                                   .Take(pageSize)
                                   .Select(oi => new OrderDetailsVM
                                   {
                                       UserEmail = oi.Order.RegisteredUser.Email,
                                       OrderId = oi.Order.PkOrderId,
                                       TransactionId = oi.Order.Transaction.PkTransactionId,
                                       DeliveryStatus = oi.Order.DeliveryStatus,
                                       ProductName = oi.Product.Name,
                                       Quantity = oi.Quantity,
                                       UnitPrice = oi.Product.Price
                                   })
                                   .ToListAsync();

            var result = new PagedResult<OrderDetailsVM>
            {
                Items = items,
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems,
            };

            return result;

            //if (orderId.HasValue)
            //{

            //    var orderDetails = await _context.Orders.Include(o => o.RegisteredUser)
            //                                            .Include(o => o.Transaction)
            //                                            .Include(o => o.OrderItems)
            //                                            .ThenInclude(oi => oi.Product)
            //                                            .FirstOrDefaultAsync(o => o.PkOrderId == orderId);

            //    var ordervm = new OrderDetailsVM
            //    {  
            //        UserEmail = orderDetails.RegisteredUser.Email,
            //        OrderId = orderId ?? 0,
            //        TransactionId = orderDetails.Transaction.PkTransactionId,
            //        OrderItems = orderDetails.OrderItems.Select(oi => new OrderItemVM
            //        {
            //            ProductId = oi.Product.PkProductId,
            //            Quantity = oi.Quantity,
            //            ProductName = oi.Product.Name,
            //            ProductPrice = oi.Product.Price
            //        }).ToList()
            //    };

            //    return ordervm;

            //}
            //var transOrderDetails = await _context.Transactions.Include(t => t.Order)
            //                                                   .ThenInclude(o => o.RegisteredUser)
            //                                                   .Include(o => o.Order)
            //                                                   .ThenInclude(o => o.OrderItems)
            //                                                   .FirstOrDefaultAsync(t => t.PkTransactionId == transactionId);

            //var vm = new OrderDetailsVM
            //{
            //    UserEmail = transOrderDetails.Order.RegisteredUser.Email,
            //    OrderId = transOrderDetails.Order.PkOrderId,
            //    TransactionId = transactionId ?? 0,
            //    OrderItems = transOrderDetails.Order.OrderItems.Select(o => new OrderItemVM
            //    {
            //        ProductId = o.Product.PkProductId,
            //        Quantity = o.Quantity,
            //        ProductName = o.Product.Name,
            //        ProductPrice = o.Product.Price
            //    }).ToList()
            //};

            //return vm;
        }
    }
}
