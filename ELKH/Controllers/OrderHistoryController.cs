using ELKH.Repositories;
using ELKH.Services;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    /// <summary>
    /// Provides staff/admin views of order history and order-status management.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class OrderHistoryController : Controller
    {
        private readonly OrderHistoryManagementRepo _orderManagementRepo;
        private readonly IOrderEmailService _orderEmail;
        private readonly IRegisteredUserProfileRepo _profileRepo;

        /// <summary>
        /// Server-side allowlist for the delivery status dropdown.
        /// Must stay in sync with the <c>statusOptions</c> array in <c>Views/OrderHistory/Index.cshtml</c>.
        /// Any value not in this set is rejected before it reaches the database.
        /// </summary>
        private static readonly HashSet<string> ValidDeliveryStatuses =
        [
            "Pending",
            "Processing",
            "Shipped",
            "Delivered",
            "Cancelled"
        ];

        public OrderHistoryController(
            OrderHistoryManagementRepo orderManagementRepo,
            IOrderEmailService orderEmail,
            IRegisteredUserProfileRepo profileRepo)
        {
            _orderManagementRepo = orderManagementRepo;
            _orderEmail          = orderEmail;
            _profileRepo         = profileRepo;
        }

        /// <summary>
        /// GET: /OrderHistory/Index
        /// Displays a summary list of all orders in the system for staff/admin review.
        /// Projects only the fields needed by the listing view to avoid over-fetching.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var orders = await _orderManagementRepo.GetAllOrders();

            // Project to a flat summary VM — only the fields required by the listing view.
            var orderVM = orders.Select(o => new OrderDetailsVM
            {
                OrderId        = o.PkOrderId,
                UserEmail      = o.RegisteredUser?.Email ?? string.Empty,
                DeliveryStatus = o.DeliveryStatus,
            }).ToList();

            return View(orderVM);
        }

        /// <summary>
        /// GET: /OrderHistory/OrderDetails
        /// Displays full line-item detail for a single order.
        /// Admin-only — looks up the order by ID with no email scoping so staff can
        /// view any customer's order, not just orders belonging to the acting user.
        /// </summary>
        /// <param name="orderId">Primary key of the order to display.</param>
        public async Task<IActionResult> OrderDetails(int orderId)
        {
            // Use the unscoped admin lookup — not OrderDetails(email, orderId) which was
            // designed for customer-facing history and would silently return null here
            // because the admin account never places orders itself.
            var details = await _orderManagementRepo.GetByIdAsync(orderId);

            if (details is null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToAction(nameof(Index));
            }

            // Map the full domain model to a flat VM consumed by the view.
            // Transaction and RegisteredUser are included by GetByIdAsync.
            // Transaction may be null for orders that have not yet been paid (Pending status).
            var detailsVM = new OrderDetailsVM
            {
                OrderId       = details.PkOrderId,
                UserEmail     = details.RegisteredUser?.Email ?? string.Empty,
                TransactionId = details.Transaction?.PkTransactionId ?? 0,
                OrderItems    = details.OrderItems.Select(oi => new OrderItemVM
                {
                    ProductId    = oi.Product?.PkProductId ?? 0,
                    Quantity     = oi.Quantity,
                    ProductName  = oi.Product?.Name ?? string.Empty,
                    ProductPrice = oi.Product?.Price ?? 0m
                }).ToList()
            };
            return View(detailsVM);
        }

        /// <summary>
        /// POST: /OrderHistory/UpdateDeliveryStatus
        /// Updates an order's delivery status and sends the customer a notification email
        /// when the status transitions to Shipped or Delivered.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDeliveryStatus(int orderId, string deliveryStatus)
        {
            // Reject any value not in the allowlist before it reaches the database.
            // The view renders the same set of options, but server-side validation is
            // the only reliable guard — client-side controls can always be bypassed.
            if (!ValidDeliveryStatuses.Contains(deliveryStatus))
            {
                TempData["Message"] = "warning, Invalid delivery status.";
                return RedirectToAction(nameof(Index));
            }

            var order = await _orderManagementRepo.UpdateDeliveryStatusAsync(orderId, deliveryStatus);
            if (order is null)
            {
                TempData["Message"] = "warning, Order not found.";
                return RedirectToAction(nameof(Index));
            }

            // Fire notification email for customer-visible milestones.
            var customerEmail = order.RegisteredUser?.Email;
            if (!string.IsNullOrEmpty(customerEmail))
            {
                var profile   = _profileRepo.GetById(customerEmail);
                var firstName = profile?.FirstName ?? "Customer";

                try
                {
                    if (deliveryStatus == "Shipped")
                        await _orderEmail.SendShippedAsync(customerEmail, firstName, orderId);
                    else if (deliveryStatus == "Delivered")
                        await _orderEmail.SendDeliveredAsync(customerEmail, firstName, orderId);
                }
                catch { /* email failure must not block the status update */ }
            }

            TempData["Message"] = $"success, Order #{orderId} status updated to {deliveryStatus}.";
            return RedirectToAction(nameof(Index));
        }
    }
}
