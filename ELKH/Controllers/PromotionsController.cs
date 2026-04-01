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
        private readonly IProductService _productService;
        private readonly ICouponService _couponService;

        public PromotionsController(IProductService productService, ICouponService couponService)
        {
            _productService = productService;
            _couponService = couponService;
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

            // Get all promotional products
            var allPromotionalProducts = (await _productService.GetPromotionalProductsAsync()).AsEnumerable();

            // Apply sorting with discount-focused options
            allPromotionalProducts = sort switch
            {
                "name_asc" => allPromotionalProducts.OrderBy(p => p.ProductName, StringComparer.OrdinalIgnoreCase),
                "name_desc" => allPromotionalProducts.OrderByDescending(p => p.ProductName, StringComparer.OrdinalIgnoreCase),
                "price_low" => allPromotionalProducts.OrderBy(p => p.Price),
                "price_high" => allPromotionalProducts.OrderByDescending(p => p.Price),
                "discount_high" => allPromotionalProducts.OrderByDescending(p => p.DiscountPercent).ThenBy(p => p.ProductName),
                "newest" => allPromotionalProducts.OrderByDescending(p => p.DateAdded),
                "oldest" => allPromotionalProducts.OrderBy(p => p.DateAdded),
                _ => allPromotionalProducts.OrderByDescending(p => p.DiscountPercent).ThenBy(p => p.ProductName) // discount_high default
            };

            var filtered = allPromotionalProducts.ToList();
            var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);
            var pageItems = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // Get categories for any additional filtering in the view
            var categories = await _productService.GetCategoriesAsync();

            ViewBag.Sort = sort;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Total = filtered.Count;
            ViewBag.Categories = new SelectList(categories, "PkCategoryId", "CategoryName");

            // Prepare ViewModels for partial views
            ViewBag.SortingVM = new ProductSortingVM
            {
                CurrentSort = sort,
                // Add promotion-specific sorting options
                IsPromotionView = true
            };

            ViewBag.PaginationVM = new PaginationVM
            {
                CurrentPage = page,
                TotalPages = totalPages
            };

            return View(pageItems);
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
            var allPromotionalProducts = (await _productService.GetPromotionalProductsAsync())
                .Where(p => p.CategoryId == categoryId);

            // Apply sorting
            allPromotionalProducts = sort switch
            {
                "name_asc" => allPromotionalProducts.OrderBy(p => p.ProductName, StringComparer.OrdinalIgnoreCase),
                "name_desc" => allPromotionalProducts.OrderByDescending(p => p.ProductName, StringComparer.OrdinalIgnoreCase),
                "price_low" => allPromotionalProducts.OrderBy(p => p.Price),
                "price_high" => allPromotionalProducts.OrderByDescending(p => p.Price),
                "discount_high" => allPromotionalProducts.OrderByDescending(p => p.DiscountPercent).ThenBy(p => p.ProductName),
                "newest" => allPromotionalProducts.OrderByDescending(p => p.DateAdded),
                "oldest" => allPromotionalProducts.OrderBy(p => p.DateAdded),
                _ => allPromotionalProducts.OrderByDescending(p => p.DiscountPercent).ThenBy(p => p.ProductName)
            };

            var result = allPromotionalProducts.ToList();
            
            return Json(new { 
                products = result,
                count = result.Count
            });
        }
    }
}