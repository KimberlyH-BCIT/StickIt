using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    public class StaffController : Controller
    {
        public IActionResult Index()
        {
            return View("~/Views/Home/Index.cshtml");
        }
    }
}
