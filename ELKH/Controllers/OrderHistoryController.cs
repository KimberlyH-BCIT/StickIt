using ELKH.Models; // Ensure Models is included for Enums
using ELKH.Repositories;
using ELKH.Services;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ELKH.Controllers
{
    /// <summary>
    /// Admin-only controller for viewing and managing all customer order histories.
    /// Provides order listing, detailed order views, and delivery status updates.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class OrderHistoryController : Controller
    {
        // REMOVED: ValidDeliveryStatuses HashSet is no longer needed 
        // because Enums provide built-in type safety.

        private readonly IOrderHistoryManagementRepo _orderManagementRepo;
        private readonly IOrderEmailService _orderEmail;
        private readonly IRegisteredUserProfileRepo _profileRepo;
        private readonly ILogger<OrderHistoryController> _logger;

        public OrderHistoryController(
            IOrderHistoryManagementRepo orderManagementRepo,
            IOrderEmailService orderEmail,
            IRegisteredUserProfileRepo profileRepo,
            ILogger<OrderHistoryController> logger)
        {
            _orderManagementRepo = orderManagementRepo;
            _orderEmail = orderEmail;
            _profileRepo = profileRepo;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var orders = await _orderManagementRepo.GetAllOrders();

            var orderVM = orders.Select(o => new OrderDetailsVM
            {
                OrderId = o.PkOrderId,
                UserEmail = o.RegisteredUser?.Email ?? string.Empty,
                // FIXED: Convert Enum to string for the ViewModel
                DeliveryStatus = o.DeliveryStatus.ToString(),
            }).ToList();

            return View(orderVM);
        }

        public async Task<IActionResult> OrderDetails(int orderId)
        {
            var details = await _orderManagementRepo.GetByIdAsync(orderId);

            if (details is null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToAction(nameof(Index));
            }

            var detailsVM = new OrderDetailsVM
            {
                OrderId = details.PkOrderId,
                UserEmail = details.RegisteredUser?.Email ?? string.Empty,
                TransactionId = details.Transaction?.PkTransactionId ?? 0,
                // FIXED: Convert Enum to string for display
                DeliveryStatus = details.DeliveryStatus.ToString(),
                OrderItems = details.OrderItems.Select(oi => new OrderItemVM
                {
                    ProductId = oi.Product?.PkProductId ?? 0,
                    Quantity = oi.Quantity,
                    ProductName = oi.Product?.Name ?? string.Empty,
                    ProductPrice = oi.Product?.Price ?? 0m
                }).ToList()
            };
            return View(detailsVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // FIXED: Parameter changed from 'string deliveryStatus' to 'DeliveryStatus deliveryStatus'
        public async Task<IActionResult> UpdateDeliveryStatus(int orderId, DeliveryStatus deliveryStatus)
        {
            // The model binder automatically validates if the input matches an Enum member.
            // We no longer need the ValidDeliveryStatuses.Contains() check.

            var order = await _orderManagementRepo.UpdateDeliveryStatusAsync(orderId, deliveryStatus);

            if (order is null)
            {
                TempData["Message"] = "warning, Order not found.";
                return RedirectToAction(nameof(Index));
            }

            var customerEmail = order.RegisteredUser?.Email;
            if (!string.IsNullOrEmpty(customerEmail))
            {
                var profile = _profileRepo.GetById(customerEmail);
                var firstName = profile?.FirstName ?? "Customer";

                try
                {
                    // FIXED: Comparing against Enum members instead of strings
                    if (deliveryStatus == DeliveryStatus.Shipped)
                        await _orderEmail.SendShippedAsync(customerEmail, firstName, orderId);
                    else if (deliveryStatus == DeliveryStatus.Delivered)
                        await _orderEmail.SendDeliveredAsync(customerEmail, firstName, orderId);
                }
                catch { /* email failure non-blocking */ }
            }

            TempData["Message"] = $"success, Order #{orderId} status updated to {deliveryStatus}.";
            return RedirectToAction(nameof(Index));
        }
    }
}
