using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    /// <summary>
    /// Provides staff/admin views of order history.
    /// <see cref="Index"/> lists all orders across all customers.
    /// <see cref="OrderDetails"/> shows full line-item detail for a single order,
    /// scoped to the currently signed-in user's own orders for security.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class OrderHistoryController : Controller
    {
        private readonly OrderHistoryManagementRepo _orderManagementRepo;

        public OrderHistoryController(OrderHistoryManagementRepo orderManagementRepo)
        {
            _orderManagementRepo = orderManagementRepo;
        }

        /// <summary>
        /// Displays a summary list of all orders in the system (admin view).
        /// Projects only the fields needed for the listing to avoid over-fetching.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var orders = await _orderManagementRepo.GetAllOrders();

            // Project to a flat summary VM — only the fields required by the listing view.
            var orderVM = orders.Select(o => new OrderDetailsVM
            {
                OrderId        = o.PkOrderId,
                UserEmail      = o.RegisteredUser.Email,
                DeliveryStatus = o.DeliveryStatus,
            }).ToList();

            return View(orderVM);
        }

        /// <summary>
        /// Displays full line-item detail for a single order belonging to the signed-in user.
        /// Redirects to <see cref="Index"/> with an error message when the user is not authenticated.
        /// </summary>
        /// <param name="orderId">The primary key of the order to display.</param>
        public async Task<IActionResult> OrderDetails(int orderId)
        {
            // User.Identity?.Name is set by ASP.NET Core Identity after login.
            // A null value means the request is unauthenticated despite the [Authorize] class attribute,
            // which can happen if the attribute is removed — guard defensively.
            var userEmail = User.Identity?.Name;
            if (userEmail == null)
            {
                TempData["Error"] = "Please log in to a staff account to check the details.";
                return RedirectToAction(nameof(Index));
            }

            var details = await _orderManagementRepo.OrderDetails(userEmail, orderId);

            // Map the full order model to a flat VM that the view consumes.
            // Note: details.Transaction is included by the repo query via .Include(o => o.Transaction).
            var detailsVM = new OrderDetailsVM
            {
                OrderId       = details.PkOrderId,
                UserEmail     = details.RegisteredUser.Email,
                TransactionId = details.Transaction.PkTransactionId,
                OrderItems    = details.OrderItems.Select(oi => new OrderItemVM
                {
                    ProductId    = oi.Product.PkProductId,
                    Quantity     = oi.Quantity,
                    ProductName  = oi.Product.Name,
                    ProductPrice = oi.Product.Price
                }).ToList()
            };

            return View(detailsVM);
        }
    }
}
