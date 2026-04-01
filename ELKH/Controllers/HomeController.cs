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
        public async Task<IActionResult> Index()
        {
            // Fetch approved store reviews for homepage carousel
            var reviews = await _storeReviewService.GetApprovedReviewsAsync(count: 10);
            ViewBag.StoreReviews = reviews;

            return View();
        }

        /// <summary>
        /// Displays the privacy policy page.
        /// </summary>
        public IActionResult Privacy()
        {
            return View();
        }

        /// <summary>
        /// Displays the contact information page.
        /// </summary>
        public IActionResult Contact()
        {
            return View();
        }

        /// <summary>
        /// Displays the frequently asked questions page.
        /// </summary>
        public IActionResult FAQ()
        {
            return View();
        }

        /// <summary>
        /// Displays the shipping information and policies page.
        /// </summary>
        public IActionResult Shipping()
        {
            return View();
        }

        /// <summary>
        /// Displays the returns and refunds policy page.
        /// </summary>
        public IActionResult Returns()
        {
            return View();
        }

        /// <summary>
        /// Displays the about us page with company information.
        /// </summary>
        public IActionResult About()
        {
            return View();
        }

        /// <summary>
        /// Demo page showcasing image optimization, lazy loading, and Kawaii UI features
        /// </summary>
        public IActionResult ImageDemo()
        {
            return View();
        }

        /// <summary>
        /// Displays the terms of service page.
        /// </summary>
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
