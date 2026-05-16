using ELKH.Services;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ELKH.Controllers
{
    /// <summary>
    /// Promotions controller for displaying products that are on sale or have coupons available.
    /// Provides a dedicated view for promotional items to help customers find deals.
    /// </summary>
    public class PromotionsController : Controller
    {
        private readonly ICategoryBrowseService _categoryBrowseService;

        public PromotionsController(ICategoryBrowseService categoryBrowseService)
        {
            _categoryBrowseService = categoryBrowseService;
        }

        /// <summary>
        /// Displays all promotional products (items with discounts or available coupons) with pagination and sorting.
        /// </summary>
        /// <param name="page">Page number for pagination (default: 1)</param>
        /// <param name="sort">Sort order: name_asc, name_desc, price_low, price_high, discount_high, newest, oldest</param>
        /// <returns>Promotions view with filtered promotional products</returns>
        [Microsoft.AspNetCore.OutputCaching.OutputCache(PolicyName = "ProductList")]
        public async Task<IActionResult> Index(int page = 1, string sort = "discount_high")
        {
            const int pageSize = 12;
            var result = await _categoryBrowseService.GetPromotionalProductsAsync(page, sort, pageSize);

            ViewBag.Sort = result.Sort;
            ViewBag.Page = result.Page;
            ViewBag.TotalPages = result.TotalPages;
            ViewBag.Total = result.Total;
            ViewBag.Categories = new SelectList(result.Categories, "PkCategoryId", "CategoryName");

            ViewBag.SortingVM = new ProductSortingVM
            {
                CurrentSort = result.Sort,
                IsPromotionView = true
            };

            ViewBag.PaginationVM = new PaginationVM
            {
                CurrentPage = result.Page,
                TotalPages = result.TotalPages
            };

            return View(result.Items);
        }

        /// <summary>
        /// Gets promotional products filtered by category for AJAX requests.
        /// </summary>
        /// <param name="categoryId">Category ID to filter by</param>
        /// <param name="sort">Sort order</param>
        /// <returns>JSON response with filtered promotional products</returns>
        [HttpGet]
        public async Task<IActionResult> GetByCategory(int categoryId, string sort = "discount_high")
        {
            var categoryResult = await _categoryBrowseService.GetPromotionalProductsByCategoryAsync(categoryId, 1, sort, int.MaxValue);
            var result = categoryResult?.Items ?? [];

            return Json(new
            {
                products = result,
                count = result.Count
            });
        }
    }
}
