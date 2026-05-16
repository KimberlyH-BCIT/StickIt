using ELKH.Services;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ELKH.Controllers
{
    /// <summary>
    /// Category controller for displaying products organized by categories.
    /// Provides category-specific views for better product organization and discovery.
    /// </summary>
    public class CategoryController : Controller
    {
        private readonly ICategoryBrowseService _categoryBrowseService;

        public CategoryController(ICategoryBrowseService categoryBrowseService)
        {
            _categoryBrowseService = categoryBrowseService;
        }

        /// <summary>
        /// Displays all products within a specific category with pagination and sorting.
        /// </summary>
        /// <param name="id">Category ID to filter products by</param>
        /// <param name="page">Page number for pagination (default: 1)</param>
        /// <param name="sort">Sort order: name_asc, name_desc, price_low, price_high, newest, oldest</param>
        /// <returns>Category view with filtered products</returns>
        [Microsoft.AspNetCore.OutputCaching.OutputCache(PolicyName = "ProductList")]
        public async Task<IActionResult> ByCategory(int id, int page = 1, string sort = "name_asc")
        {
            var result = await _categoryBrowseService.GetProductsByCategoryAsync(id, page, sort);
            if (result == null || result.CurrentCategory == null)
            {
                TempData["Message"] = "warning, Category not found.";
                return RedirectToAction("Index", "Product");
            }

            ViewBag.CategoryId = id;
            ViewBag.CategoryName = result.CurrentCategory.CategoryName;
            ViewBag.Sort = result.Sort;
            ViewBag.Page = result.Page;
            ViewBag.TotalPages = result.TotalPages;
            ViewBag.Total = result.Total;
            ViewBag.Categories = new SelectList(result.Categories, "PkCategoryId", "CategoryName", id);
            ViewBag.SortingVM = new ProductSortingVM
            {
                CategoryId = id,
                CurrentSort = result.Sort
            };
            ViewBag.PaginationVM = new PaginationVM
            {
                CurrentPage = result.Page,
                TotalPages = result.TotalPages,
                CategoryId = id
            };

            return View(result.Items);
        }

        /// <summary>
        /// Displays all available categories for browsing.
        /// </summary>
        /// <returns>Index view with all categories</returns>
        [Microsoft.AspNetCore.OutputCaching.OutputCache(PolicyName = "ProductList")]
        public async Task<IActionResult> Index()
        {
            var categoryCounts = await _categoryBrowseService.GetCategoryCountsAsync();
            ViewBag.CategoryProductCounts = categoryCounts;
            return View(categoryCounts.Select(x => x.Category).ToList());
        }

        /// <summary>
        /// Displays promotional products within a specific category.
        /// </summary>
        /// <param name="id">Category ID to filter promotional products by</param>
        /// <param name="page">Page number for pagination (default: 1)</param>
        /// <param name="sort">Sort order with promotion-focused options</param>
        /// <returns>Category promotions view with filtered promotional products</returns>
        [Microsoft.AspNetCore.OutputCaching.OutputCache(PolicyName = "ProductList")]
        public async Task<IActionResult> Promotions(int id, int page = 1, string sort = "discount_high")
        {
            var result = await _categoryBrowseService.GetPromotionalProductsByCategoryAsync(id, page, sort);
            if (result == null || result.CurrentCategory == null)
            {
                TempData["Message"] = "warning, Category not found.";
                return RedirectToAction("Index", "Promotions");
            }

            ViewBag.CategoryId = id;
            ViewBag.CategoryName = result.CurrentCategory.CategoryName;
            ViewBag.Sort = result.Sort;
            ViewBag.Page = result.Page;
            ViewBag.TotalPages = result.TotalPages;
            ViewBag.Total = result.Total;
            ViewBag.Categories = new SelectList(result.Categories, "PkCategoryId", "CategoryName", id);
            ViewBag.SortingVM = new ProductSortingVM
            {
                CategoryId = id,
                CurrentSort = result.Sort,
                IsPromotionView = true
            };
            ViewBag.PaginationVM = new PaginationVM
            {
                CurrentPage = result.Page,
                TotalPages = result.TotalPages,
                CategoryId = id
            };

            return View(result.Items);
        }
    }
}
