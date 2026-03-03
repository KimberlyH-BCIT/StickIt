using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    public class CustomerController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
