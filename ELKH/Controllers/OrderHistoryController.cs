using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    [Authorize]
    public class OrderHistoryController : Controller
    {
        private readonly OrderHistoryManagementRepo _orderManagementRepo;
        public OrderHistoryController(OrderHistoryManagementRepo orderManagementRepo)
        {
            _orderManagementRepo = orderManagementRepo;
        }
       
        public async Task<IActionResult> Index()
        {
            var orders = await _orderManagementRepo.GetAllOrders();

            var orderVM = orders.Select(o => new OrderDetailsVM
            {
                OrderId = o.PkOrderId,
                UserEmail = o.RegisteredUser.Email,
                DeliveryStatus = o.DeliveryStatus,
            }).ToList();

            return View(orderVM);
        }

        public async Task<IActionResult> OrderDetails(int orderId)
        {
            var userEmail = User.Identity.Name;
            if(userEmail == null)
            {
                TempData["Error"] = "Please Log In staff accout to check the details";
                return RedirectToAction("Index");
            }
            var details = await _orderManagementRepo.OrderDetails(userEmail, orderId);

            var detailsVM = new OrderDetailsVM
            {
                OrderId = details.PkOrderId,
                UserEmail = details.RegisteredUser.Email,
                TransactionId = details.Transaction.PkTransactionId,
                OrderItems = details.OrderItems.Select(oi => new OrderItemVM
                {
                    ProductId = oi.Products.PkProductId,
                    Quantity = oi.Quantity,
                    ProductName = oi.Products.Name,
                    ProductPrice = oi.Products.Price
                }).ToList()
            };

            return View(details);
        }
    }
}
