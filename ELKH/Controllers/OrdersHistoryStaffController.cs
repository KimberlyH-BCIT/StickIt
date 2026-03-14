using ELKH.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    [Authorize(Roles = "Staff,Admin")]
    public class OrdersHistoryStaffController : Controller
    {
        private readonly OrderHistoryStaffRepo _repo;
        public OrdersHistoryStaffController(OrderHistoryStaffRepo repo)
        {
            _repo = repo;
        }



        public async Task<IActionResult> Index()
        {
            var orders = await _repo.GetAllOrders();

            return View(orders);
        }

        public async Task<IActionResult> OrderDetail(int? orderId, int? transactionId)
        {
            var details = await _repo.OrderDetails(orderId, transactionId);

            return View(details);
        }
    }
}
