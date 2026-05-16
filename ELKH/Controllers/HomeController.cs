using System.Diagnostics;
using ELKH.Models;
using ELKH.Services;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    /// <summary>
    /// Handles the application's public landing page and informational content.
    /// No authentication is required for any action in this controller.
    /// </summary>
    /// <remarks>
    /// This controller serves public-facing content including:
    /// - Homepage with featured products and store reviews
    /// - Privacy policy and contact information  
    /// - FAQ, shipping, and returns policy pages
    /// - Error handling for application exceptions
    /// 
    /// All actions are accessible to anonymous users and provide essential
    /// e-commerce information for customers.
    /// </remarks>
    public class HomeController : Controller
    {
        private readonly IStoreReviewService _storeReviewService;

        public HomeController(IStoreReviewService storeReviewService)
        {
            _storeReviewService = storeReviewService;
        }

        /// <summary>
        /// Displays the main landing page with featured content and customer reviews.
        /// </summary>
        /// <returns>Homepage view with store reviews in ViewBag</returns>
        [Microsoft.AspNetCore.OutputCaching.OutputCache(PolicyName = "ProductList")]
        public async Task<IActionResult> Index()
        {
            // Fetch approved store reviews for homepage carousel
            var reviews = await _storeReviewService.GetApprovedReviewsAsync(count: 10);
            ViewBag.StoreReviews = reviews;

            return View();
        }

        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByHeader = "Accept-Encoding")]
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByHeader = "Accept-Encoding")]
        public IActionResult Contact()
        {
            return View();
        }

        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByHeader = "Accept-Encoding")]
        public IActionResult FAQ()
        {
            return View();
        }

        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByHeader = "Accept-Encoding")]
        public IActionResult Shipping()
        {
            return View();
        }

        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByHeader = "Accept-Encoding")]
        public IActionResult Returns()
        {
            return View();
        }

        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByHeader = "Accept-Encoding")]
        public IActionResult About()
        {
            return View();
        }

        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByHeader = "Accept-Encoding")]
        public IActionResult Terms()
        {
            return View();
        }

        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByHeader = "Accept-Encoding")]
        public IActionResult Accessibility()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var traceIdentifier = ControllerContext.HttpContext?.TraceIdentifier;
            return View(new ErrorVM { RequestId = traceIdentifier is null ? null : Activity.Current?.Id ?? traceIdentifier });
        }
    }
}
