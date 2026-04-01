using ELKH.Repositories;
using ELKH.Models; // Essential for DeliveryStatus Enum
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    [Authorize(Roles = "Staff,Admin,Manager")]
    public class OrdersHistoryStaffController : Controller
    {
        private readonly OrderHistoryStaffRepo _repo;

        public OrdersHistoryStaffController(OrderHistoryStaffRepo repo)
        {
            _repo = repo;
        }

        public async Task<IActionResult> Index(string? searchString, DeliveryStatus? status, int page = 1)
        {
            int pageSize = 10;

            // FIXED: 'status' is now DeliveryStatus?, matching the Repo signature
            var orders = await _repo.GetAllOrders(searchString, page, pageSize, status);

            // FIXED: Passing Enum members instead of strings to GetCountByStatus
            ViewBag.CancelledOrders = await _repo.GetCountByStatus(DeliveryStatus.Cancelled);
            ViewBag.PendingOrders = await _repo.GetCountByStatus(DeliveryStatus.Pending);
            ViewBag.DeliveredOrders = await _repo.GetCountByStatus(DeliveryStatus.Delivered);

            ViewBag.SearchString = searchString;
            ViewBag.CurrentStatus = status;

            return View(orders);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int orderId, DeliveryStatus status)
        {
            // FIXED: Passing DeliveryStatus Enum directly
            bool success = await _repo.UpdateOrderStatus(orderId, status);

            if (success)
            {
                TempData["StatusMessage"] = $"Order #{orderId} successfully updated to {status}.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to update order status.";
            }

            return RedirectToAction("OrderDetail", new { orderId = orderId });
        }

        public async Task<IActionResult> OrderDetail(int? orderId, int? transactionId, string? searchString, int page = 1)
        {
            int pageSize = 10;
            var details = await _repo.OrderDetails(orderId, transactionId, searchString, page, pageSize);

            ViewBag.orderId = orderId;
            ViewBag.transactionId = transactionId;
            ViewBag.SearchString = searchString;

            return View(details);
        }
    }
}