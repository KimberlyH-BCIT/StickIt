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
        private readonly IProductService _productService;

        public CategoryController(IProductService productService)
        {
            _productService = productService;
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
            const int pageSize = 12;

            // Get all products and filter by category
            var allProducts = (await _productService.GetAllAsync()).AsEnumerable()
                .Where(p => p.CategoryId == id);

            // Get category information
            var categories = await _productService.GetCategoriesAsync();
            var currentCategory = categories.FirstOrDefault(c => c.PkCategoryId == id);
            
            if (currentCategory == null)
            {
                TempData["Message"] = "warning, Category not found.";
                return RedirectToAction("Index", "Product");
            }

            // Apply sorting
            allProducts = sort switch
            {
                "name_desc" => allProducts.OrderByDescending(p => p.ProductName, StringComparer.OrdinalIgnoreCase),
                "price_low" => allProducts.OrderBy(p => p.Price),
                "price_high" => allProducts.OrderByDescending(p => p.Price),
                "newest" => allProducts.OrderByDescending(p => p.DateAdded),
                "oldest" => allProducts.OrderBy(p => p.DateAdded),
                _ => allProducts.OrderBy(p => p.ProductName, StringComparer.OrdinalIgnoreCase) // name_asc
            };

            var filtered = allProducts.ToList();
            var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);
            var pageItems = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // Set ViewBag data for the view
            ViewBag.CategoryId = id;
            ViewBag.CategoryName = currentCategory.CategoryName;
            ViewBag.Sort = sort;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Total = filtered.Count;
            ViewBag.Categories = new SelectList(categories, "PkCategoryId", "CategoryName", id);

            // Prepare ViewModels for partial views
            ViewBag.SortingVM = new ProductSortingVM
            {
                CategoryId = id,
                CurrentSort = sort
            };

            ViewBag.PaginationVM = new PaginationVM
            {
                CurrentPage = page,
                TotalPages = totalPages,
                CategoryId = id
            };

            return View(pageItems);
        }

        /// <summary>
        /// Displays all available categories for browsing.
        /// </summary>
        /// <returns>Index view with all categories</returns>
        public async Task<IActionResult> Index()
        {
            var categories = await _productService.GetCategoriesAsync();

            // Get product count for each category
            var allProducts = await _productService.GetAllAsync();
            var categoryProductCounts = categories.Select(category => new
            {
                Category = category,
                ProductCount = allProducts.Count(p => p.CategoryId == category.PkCategoryId)
            }).ToList();

            ViewBag.CategoryProductCounts = categoryProductCounts;

            return View(categories);
        }

        /// <summary>
        /// Displays promotional products within a specific category.
        /// </summary>
        /// <param name="id">Category ID to filter promotional products by</param>
        /// <param name="page">Page number for pagination (default: 1)</param>
        /// <param name="sort">Sort order with promotion-focused options</param>
        /// <returns>Category promotions view with filtered promotional products</returns>
        public async Task<IActionResult> Promotions(int id, int page = 1, string sort = "discount_high")
        {
            const int pageSize = 12;

            // Get promotional products filtered by category
            var allPromotionalProducts = (await _productService.GetPromotionalProductsAsync())
                .Where(p => p.CategoryId == id);

            // Get category information
            var categories = await _productService.GetCategoriesAsync();
            var currentCategory = categories.FirstOrDefault(c => c.PkCategoryId == id);

            if (currentCategory == null)
            {
                TempData["Message"] = "warning, Category not found.";
                return RedirectToAction("Index", "Promotions");
            }

            // Apply sorting with promotion-focused options
            allPromotionalProducts = sort switch
            {
                "name_desc" => allPromotionalProducts.OrderByDescending(p => p.ProductName, StringComparer.OrdinalIgnoreCase),
                "price_low" => allPromotionalProducts.OrderBy(p => p.Price),
                "price_high" => allPromotionalProducts.OrderByDescending(p => p.Price),
                "discount_high" => allPromotionalProducts.OrderByDescending(p => p.DiscountPercent).ThenBy(p => p.ProductName),
                "newest" => allPromotionalProducts.OrderByDescending(p => p.DateAdded),
                "oldest" => allPromotionalProducts.OrderBy(p => p.DateAdded),
                "name_asc" => allPromotionalProducts.OrderBy(p => p.ProductName, StringComparer.OrdinalIgnoreCase),
                _ => allPromotionalProducts.OrderByDescending(p => p.DiscountPercent).ThenBy(p => p.ProductName) // discount_high default
            };

            var filtered = allPromotionalProducts.ToList();
            var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);
            var pageItems = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // Set ViewBag data for the view
            ViewBag.CategoryId = id;
            ViewBag.CategoryName = currentCategory.CategoryName;
            ViewBag.Sort = sort;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Total = filtered.Count;
            ViewBag.Categories = new SelectList(categories, "PkCategoryId", "CategoryName", id);

            // Prepare ViewModels for partial views
            ViewBag.SortingVM = new ProductSortingVM
            {
                CategoryId = id,
                CurrentSort = sort,
                IsPromotionView = true
            };

            ViewBag.PaginationVM = new PaginationVM
            {
                CurrentPage = page,
                TotalPages = totalPages,
                CategoryId = id
            };

            return View(pageItems);
        }
    }
}