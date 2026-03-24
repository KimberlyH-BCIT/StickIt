using System.Diagnostics;
using ELKH.Models;
using ELKH.Services;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    /// <summary>
    /// Handles the application's public landing page and global error display.
    /// No authentication is required for any action in this controller.
    /// </summary>
    public class HomeController : Controller
    {
        private readonly IStoreReviewService _storeReviewService;

        public HomeController(IStoreReviewService storeReviewService)
        {
            _storeReviewService = storeReviewService;
        }

        public async Task<IActionResult> Index()
        {
            // Fetch approved store reviews for homepage carousel
            var reviews = await _storeReviewService.GetApprovedReviewsAsync(count: 10);
            ViewBag.StoreReviews = reviews;

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult FAQ()
        {
            return View();
        }

        public IActionResult Shipping()
        {
            return View();
        }

        public IActionResult Returns()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Terms()
        {
            return View();
        }

        public IActionResult Accessibility()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
