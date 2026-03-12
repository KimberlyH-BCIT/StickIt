using ELKH.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    //[Authorize(Roles = "Staff")]
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

        public async Task<IActionResult> OrderDetail(int orderId)
        {
            var details = await _repo.OrderDetails(orderId);

            return View(details);
        }
    }
}
