using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    public class WishlistController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
