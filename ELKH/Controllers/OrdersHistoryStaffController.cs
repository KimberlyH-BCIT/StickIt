using ELKH.Repositories;
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



        public async Task<IActionResult> Index(string? searchString,int page = 1)
        {
            int pageSize = 10;
            var orders = await _repo.GetAllOrders(searchString,page,pageSize);
            ViewBag.SearchString = searchString;
            return View(orders);
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
