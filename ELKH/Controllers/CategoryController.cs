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
    /// <remarks>
    /// TABLE OF CONTENTS
    /// ================================================================================
    /// 1. Constructor & Dependencies ................................. Lines [16-19]
    ///    - CategoryController()           // Service injection setup
    /// 
    /// 2. ByCategory Action ........................................... Lines [22-87]
    ///    - Category product listing       // Paginated product display by category
    ///    - Sorting implementation         // Multiple sort options
    ///    - Output caching                 // Performance optimization
    /// 
    /// 3. Index Action ................................................ Lines [89-108]
    ///    - Categories overview            // All categories with product counts
    ///    - Category statistics           // Product count calculation
    /// 
    /// 4. Promotions Action ........................................... Lines [110-178]
    ///    - Promotional products           // Category-filtered promotional items
    ///    - Discount-focused sorting       // Promotion-specific sort options
    /// ================================================================================
    /// 
    /// ARCHITECTURAL CONTEXT:
    /// • ASP.NET Core MVC controller implementing category-based product navigation
    /// • Uses output caching for performance optimization on product listings
    /// • Integrates with ProductService for data access and business logic
    /// • Part of ELKH's e-commerce product discovery and browsing system
    /// • Supports both regular and promotional product views
    /// 
    /// BUSINESS LOGIC & FEATURES:
    /// • Category-based product filtering with comprehensive sorting options
    /// • Pagination support for large product catalogs (12 items per page)
    /// • Product count statistics for category overview
    /// • Promotional product display with discount-focused sorting
    /// • Error handling with user-friendly messages and redirects
    /// 
    /// PERFORMANCE OPTIMIZATIONS:
    /// • Output caching on ByCategory action using "ProductList" policy
    /// • Efficient product filtering using LINQ with minimal database calls
    /// • Pagination to limit data transfer and rendering time
    /// • Strategic product count calculations for category statistics
    /// 
    /// INTEGRATION POINTS:
    /// • Depends on: IProductService for all product and category data access
    /// • Used by: Category navigation, product browsing, promotional campaigns
    /// • ViewModels: ProductSortingVM, PaginationVM for partial view integration
    /// • Views: ByCategory, Index, Promotions with corresponding Razor templates
    /// </remarks>
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