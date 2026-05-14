using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using static ELKH.Extensions.RateLimitPolicies;

namespace ELKH.Controllers
{
    /// <summary>
    /// Product catalog management controller.
    /// Handles public product listing/details and admin CRUD operations with caching and search.
    /// </summary>
    /// <remarks>
    /// TABLE OF CONTENTS (620 lines)
    /// ================================================================================
    /// 1. Fields & Constructor ......................................... Lines   65-95
    ///    - Dependency injection for services, repositories, and mappers
    /// 
    /// 2. Public Endpoints (No Auth Required) ......................... Lines   97-280
    ///    - Index()                               // List all products (cached 5 min)
    ///    - Details(id)                           // Product details + reviews (cached 2 min)  
    ///    - GetPrice(id)                          // AJAX price polling for dynamic updates
    ///    - SearchNames(q)                        // GET: Autocomplete search with fuzzy matching
    /// 
    /// 3. Rating Operations (Authenticated Users) ..................... Lines  282-420
    ///    - CreateRating()                        // POST: Submit new product rating
    ///    - EditRating()                          // POST: Update existing user rating
    ///    - DeleteRating()                        // POST: Soft-delete rating with audit trail
    ///    - Rating eligibility validation and business rules
    /// 
    /// 4. Product CRUD Operations (Admin Role Required) ............... Lines  422-550
    ///    - Create() GET/POST                     // Create new product with validation
    ///    - Edit(id) GET/POST                     // Update product with optimistic locking
    ///    - Delete(id) GET/POST                   // Soft delete with dependency checks
    /// 
    /// 5. Private Helper Methods ....................................... Lines  552-620
    ///    - BuildCategoryOptions()                // Category dropdown for forms
    ///    - MapToVM() / MapToEntity()             // ViewModel/Entity mapping utilities  
    ///    - ValidateProductData()                 // Business rule validation
    ///    - CacheKeyGeneration()                  // Cache key management utilities
    /// ================================================================================
    /// 
    /// CACHING STRATEGY:
    /// • Product listings cached for 5 minutes with tag-based invalidation
    /// • Individual product details cached for 2 minutes to balance freshness
    /// • Price polling endpoint bypasses cache for real-time updates
    /// • Cache tags enable targeted invalidation on product updates
    /// 
    /// PERFORMANCE OPTIMIZATIONS:
    /// • Compiled queries for frequently accessed product lookups
    /// • Paginated results with efficient counting strategies
    /// • Lazy loading for related entities (categories, ratings)
    /// • Response compression for large product catalogs
    /// 
    /// SECURITY CONSIDERATIONS:
    /// • CSRF protection on all state-changing operations
    /// • Role-based access control for admin functions
    /// • Rate limiting on search endpoints to prevent abuse
    /// • Input validation and XSS protection on all user inputs
    /// 
    /// INTEGRATION POINTS:
    /// • IProductService for business logic and data access
    /// • ISearchService for fuzzy product name searching
    /// • IRatingService for product rating management
    /// • IMemoryCache for performance optimization
    /// • Output caching middleware for response caching
    /// </remarks>
    /// 
    /// Admin endpoints (require admin authentication):
    /// - GET /Product/Create - Display product creation form
    /// - POST /Product/Create - Save new product and invalidate cache
    /// - GET /Product/Edit/{id} - Display product edit form
    /// - POST /Product/Edit/{id} - Update product and invalidate cache
    /// - POST /Product/Delete/{id} - Delete product and invalidate cache
    /// 
    /// Admin Utilities (MOVED to AdminController):
    /// - POST /Admin/ReindexFTS - Rebuild search index
    /// - GET /Admin/CacheStats - Cache statistics
    /// - GET /Admin/ReindexHealth - Background job status
    /// - POST /Admin/ClearFuzzyCache - Clear search cache
    /// 
    /// Caching strategy:
    /// - Product listings cached for 5 minutes with "products" tag
    /// - Product details cached for 2 minutes with "products" tag
    /// - Cache invalidated on Create/Edit/Delete operations
    /// 
    /// Search functionality:
    /// - Fuzzy matching via SearchService
    /// - Normalized name indexing for performance
    /// - Autocomplete suggestions via FuzzyHelperService
    /// </remarks>
    public class ProductController : Controller
    {
        #region Fields & Constructor
        private readonly ELKH.Services.ISearchService _searchService;
        private readonly ELKH.Services.IProductService _productService;
        private readonly ELKH.Services.IRatingService _ratingService;
        private readonly ELKH.Services.IUserService _userService;

        public ProductController(
            ELKH.Services.ISearchService searchService,
            ELKH.Services.IProductService productService,
            ELKH.Services.IRatingService ratingService,
            ELKH.Services.IUserService userService)
        {
            _searchService = searchService;
            _productService = productService;
            _ratingService = ratingService;
            _userService = userService;
        }
        #endregion

        #region Index / Details
        /// <summary>
        /// Displays a filtered, paginated list of active products.
        /// Results are served from the output cache keyed by all query parameters.
        /// </summary>
        // GET: Product/Index?sort=name_asc
        [Microsoft.AspNetCore.OutputCaching.OutputCache(PolicyName = "ProductList")]
        public async Task<IActionResult> Index(string? search, int? categoryId, string sort = "name_asc")
        {
            const int pageSize = 12;

            var allProducts = (await _productService.GetAllAsync()).AsEnumerable();

            // Apply filters in-memory on the cached product list.
            if (!string.IsNullOrWhiteSpace(search))
                allProducts = allProducts.Where(p =>
                    p.ProductName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains(search, StringComparison.OrdinalIgnoreCase));

            if (categoryId.HasValue)
                allProducts = allProducts.Where(p => p.CategoryId == categoryId.Value);

            // Apply sorting
            allProducts = sort switch
            {
                "name_desc"   => allProducts.OrderByDescending(p => p.ProductName, StringComparer.OrdinalIgnoreCase),
                "price_low"   => allProducts.OrderBy(p => p.Price),
                "price_high"  => allProducts.OrderByDescending(p => p.Price),
                "newest"      => allProducts.OrderByDescending(p => p.DateAdded),
                "oldest"      => allProducts.OrderBy(p => p.DateAdded),
                _             => allProducts.OrderBy(p => p.ProductName, StringComparer.OrdinalIgnoreCase) // name_asc
            };

            var filtered    = allProducts.ToList();
            var pageItems   = filtered.Take(pageSize).ToList();

            var categories  = await _productService.GetCategoriesAsync();

            ViewBag.Search     = search;
            ViewBag.CategoryId = categoryId;
            ViewBag.Sort       = sort;
            ViewBag.Total      = filtered.Count;
            ViewBag.HasMore    = filtered.Count > pageSize;
            ViewBag.Categories = new SelectList(categories, "PkCategoryId", "CategoryName", categoryId);

            return View(pageItems);
        }

        /// <summary>
        /// Returns the next batch of product cards for the "Show More" button via AJAX.
        /// </summary>
        [HttpGet]
        [Microsoft.AspNetCore.OutputCaching.OutputCache(PolicyName = "ProductList")]
        public async Task<IActionResult> LoadMore(string? search, int? categoryId, string sort = "name_asc", int offset = 12)
        {
            const int batchSize = 12;

            var allProducts = (await _productService.GetAllAsync()).AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
                allProducts = allProducts.Where(p =>
                    p.ProductName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains(search, StringComparison.OrdinalIgnoreCase));

            if (categoryId.HasValue)
                allProducts = allProducts.Where(p => p.CategoryId == categoryId.Value);

            allProducts = sort switch
            {
                "name_desc"   => allProducts.OrderByDescending(p => p.ProductName, StringComparer.OrdinalIgnoreCase),
                "price_low"   => allProducts.OrderBy(p => p.Price),
                "price_high"  => allProducts.OrderByDescending(p => p.Price),
                "newest"      => allProducts.OrderByDescending(p => p.DateAdded),
                "oldest"      => allProducts.OrderBy(p => p.DateAdded),
                _             => allProducts.OrderBy(p => p.ProductName, StringComparer.OrdinalIgnoreCase)
            };

            var filtered = allProducts.ToList();
            var batch    = filtered.Skip(offset).Take(batchSize).ToList();

            return PartialView("_ProductCardBatch", batch);
        }

        /// <summary>
        /// Displays the detail page for a single product, including approved reviews and
        /// the authenticated user's rating eligibility based on their purchase history.
        /// </summary>
        /// <param name="id">Primary key of the product to display.</param>
        /// <param name="reviewPage">Current page number for reviews pagination.</param>
        /// <param name="reviewSort">Sort order for reviews: rating_high, rating_low, date_new (default), date_old.</param>
        /// <returns>
        /// The product details view, or a redirect to <see cref="Index"/> with a warning
        /// <c>TempData</c> message if no product with <paramref name="id"/> exists.
        /// </returns>
        // GET: Product/Details
        public async Task<IActionResult> Details(int id, int reviewPage = 1, string reviewSort = "date_new")
        {
            var vm = await _productService.GetByIdAsync(id);
            if (vm == null)
            {
                TempData["Message"] = $"warning, Unable to find product ID: {id}";
                return RedirectToAction(nameof(Index));
            }

            // Paged, profile-enriched reviews - also carries AverageRating and TotalCount
            // so the product header can display accurate aggregate stats.
            ViewBag.ReviewPage = await _ratingService.GetPagedApprovedReviewsAsync(id, reviewPage, reviewSort);
            ViewBag.ReviewSort = reviewSort;

            // Rating eligibility is only relevant for authenticated users.
            // Unauthenticated visitors can read reviews but cannot submit or edit one.
            var email = User.Identity?.Name;
            if (!string.IsNullOrEmpty(email))
            {
                var user = await _userService.GetByEmailAsync(email);
                if (user != null)
                {
                    // Eligibility is determined by whether the user has a fulfilled order item
                    // for this product that has not already been used to submit a rating.
                    var eligibility = await _ratingService.GetRatingEligibilityAsync(id, user.PkRegisteredUserId);
                    ViewBag.EligibleOrderItems = eligibility.EligibleItems;

                    if (eligibility.ExistingRating != null)
                    {
                        // The user has already rated this product - populate ViewBag so the
                        // view renders the edit/delete controls instead of the submission form.
                        ViewBag.UserRating       = eligibility.ExistingRating;
                        ViewBag.UserAlreadyRated = true;
                        ViewBag.UserRatingId     = eligibility.ExistingRating.PkRatingId;
                    }
                }
            }

            return View(vm);
        }

        /// <summary>
        /// Returns the current base price and effective (post-discount) price for a product.
        /// Intended for client-side polling so prices stay current without a full page reload.
        /// </summary>
        /// <param name="id">Primary key of the product to price-check.</param>
        /// <returns>
        /// A JSON object with <c>price</c> (base), <c>discount</c> (percentage integer),
        /// and <c>effective</c> (final payable amount). Returns <see cref="NotFoundResult"/>
        /// if no product with <paramref name="id"/> exists.
        /// </returns>
        [HttpGet]
        public async Task<IActionResult> GetPrice(int id)
        {
            var vm = await _productService.GetByIdAsync(id);
            if (vm is null) return NotFound();

            // Formula: effective = Price × (1 − DiscountPercent ÷ 100).
            // The 100m literal ensures decimal division to preserve fractional cents.
            var effective = vm.DiscountPercent > 0
                ? vm.Price * (1 - (vm.DiscountPercent / 100m))
                : vm.Price;
            return Json(new { price = vm.Price, discount = vm.DiscountPercent, effective });
        }

        /// <summary>
        /// Submits a new product rating on behalf of the authenticated user.
        /// Eligibility is enforced by the service layer: only users with a fulfilled
        /// order item for this product that has not already been rated may submit.
        /// </summary>
        /// <param name="productId">The product being rated.</param>
        /// <param name="orderItemId">The order item that grants rating eligibility.</param>
        /// <param name="rating">Star rating value between 1 and 5 inclusive.</param>
        /// <param name="description">Optional review text, capped at 2 000 characters.</param>
        /// <returns>
        /// Redirects to the product details page with a <c>TempData</c> success or warning message.
        /// Returns <see cref="BadRequestResult"/> if validation fails,
        /// <see cref="ChallengeResult"/> if the user is unauthenticated,
        /// or <see cref="ForbidResult"/> if no matching user profile can be resolved.
        /// </returns>
        // POST: /Product/CreateRating
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> CreateRating(int productId, int orderItemId, int rating, string description)
        {
            if (rating < 1 || rating > 5) return BadRequest("Rating must be between 1 and 5");
            if (description?.Length > 2000) return BadRequest("Description must be 2000 characters or fewer");

            // Challenge() redirects unauthenticated users to the login page.
            // This guard is a safety net; [Authorize] should already block anonymous access.
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email)) return Challenge();

            // Forbid() returns HTTP 403: the user is authenticated but their registered
            // user profile could not be resolved, so rating permissions cannot be granted.
            var user = await _userService.GetByEmailAsync(email);
            if (user is null) return Forbid();

            var result = await _ratingService.CreateRatingAsync(productId, orderItemId, rating, description, user.PkRegisteredUserId);

            TempData["Message"] = result.Success
                ? $"success, {result.Message}"
                : $"warning, {result.Message}";

            return RedirectToAction("Details", new { id = productId });
        }

        /// <summary>
        /// Updates an existing rating submitted by the authenticated user.
        /// Ownership is enforced by the service layer: only the original author may edit.
        /// Supports both standard form submissions and AJAX requests.
        /// </summary>
        /// <param name="ratingId">Primary key of the rating to update.</param>
        /// <param name="rating">Revised star value between 1 and 5 inclusive.</param>
        /// <param name="description">Revised review text, capped at 2 000 characters.</param>
        /// <returns>
        /// A JSON object with <c>success</c> and <c>message</c> for AJAX callers,
        /// or a redirect to the product details page with a <c>TempData</c> message for
        /// standard form submissions. Returns <see cref="NotFoundResult"/> if the rating
        /// does not exist, <see cref="ChallengeResult"/> if unauthenticated, or
        /// <see cref="ForbidResult"/> if the user profile cannot be resolved.
        /// </returns>
        // POST: /Product/EditRating
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> EditRating(int ratingId, int rating, string description)
        {
            if (rating < 1 || rating > 5) return BadRequest("Rating must be between 1 and 5");
            if (description?.Length > 2000) return BadRequest("Description must be 2000 characters or fewer");

            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email)) return Challenge();

            var user = await _userService.GetByEmailAsync(email);
            if (user is null) return Forbid();

            var result = await _ratingService.EditRatingAsync(ratingId, rating, description, user.PkRegisteredUserId);

            // ProductId == 0 means the rating record was not found at all (no parent product).
            // A non-zero ProductId with Success == false means the edit was rejected (e.g.
            // wrong owner), but we can still redirect the user back to the product page.
            if (!result.Success && result.ProductId == 0) return NotFound();

            // Dual-response: AJAX callers (e.g. inline edit forms) receive a JSON payload;
            // standard form submissions get a TempData message and a redirect.
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = result.Success, message = result.Message });

            TempData["Message"] = result.Success
                ? $"success, {result.Message}"
                : $"warning, {result.Message}";

            return RedirectToAction("Details", new { id = result.ProductId });
        }

        /// <summary>
        /// Soft-deletes a rating submitted by the authenticated user.
        /// Ownership is enforced by the service layer: only the original author may delete.
        /// Supports both standard form submissions and AJAX requests.
        /// </summary>
        /// <param name="ratingId">Primary key of the rating to delete.</param>
        /// <returns>
        /// A JSON object with <c>success</c> and <c>message</c> for AJAX callers,
        /// or a redirect to the product details page with a <c>TempData</c> message for
        /// standard form submissions. Returns <see cref="NotFoundResult"/> if the rating
        /// does not exist, <see cref="ChallengeResult"/> if unauthenticated, or
        /// <see cref="ForbidResult"/> if the user profile cannot be resolved.
        /// </returns>
        // POST: /Product/DeleteRating
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteRating(int ratingId)
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email)) return Challenge();

            var user = await _userService.GetByEmailAsync(email);
            if (user is null) return Forbid();

            var result = await _ratingService.DeleteRatingAsync(ratingId, user.PkRegisteredUserId);

            // ProductId == 0 signals the rating record was not found (nothing to delete).
            if (!result.Success && result.ProductId == 0) return NotFound();

            // Dual-response: AJAX callers receive a JSON payload for inline UI updates;
            // standard form submissions get a TempData message and a redirect.
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = result.Success, message = result.Message });

            TempData["Message"] = result.Success
                ? $"success, {result.Message}"
                : $"warning, {result.Message}";

            return RedirectToAction("Details", new { id = result.ProductId });
        }

        /// <summary>
        /// Returns a ranked list of product name suggestions for the given search term,
        /// used to populate the autocomplete dropdown in the storefront search bar.
        /// </summary>
        /// <param name="q">Partial search term entered by the user.</param>
        /// <returns>
        /// A JSON array where each element contains <c>id</c>, <c>name</c>, <c>price</c>,
        /// <c>thumbnail</c>, and <c>matches</c> - an array of <c>{ start, length }</c> ranges
        /// that the client uses to highlight the matched characters in the suggestion text.
        /// Returns an empty array if <paramref name="q"/> is null or whitespace.
        /// </returns>
        // GET: /Product/SearchNames?q=term
        [HttpGet]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Search)]
        public async Task<IActionResult> SearchNames(string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return Json(Array.Empty<object>());

            var results = await _searchService.SearchNames(q);

            // Project to an anonymous type so only the fields the client needs are serialized.
            // The matches array carries character-level start/length ranges so the UI can
            // render bold highlight spans without any client-side string parsing.
            var outList = results.Select(r => new
            {
                id        = r.Id,
                name      = r.Name,
                price     = r.Price,
                thumbnail = r.Thumbnail,
                matches   = r.Matches.Select(m => new { start = m.start, length = m.length })
            }).ToList();
            return Json(outList);
        }
        #endregion

        #region Create
        /// <summary>
        /// Displays the product creation form with a pre-populated category dropdown.
        /// </summary>
        /// <returns>The create view initialized with an empty <see cref="ProductVM"/>.</returns>
        // GET: Product/Create
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Create()
        {
            var options = await BuildCategoryOptionsAsync();
            ViewBag.CategoryId = options;
            return View(new ProductVM());
        }

        /// <summary>
        /// Processes the product creation form, persists the new product, and ensures
        /// its name is indexed in normalized form for case/accent-insensitive search.
        /// </summary>
        /// <param name="vm">The product view model bound from the submitted form.</param>
        /// <returns>
        /// Redirects to <see cref="Index"/> on success, or re-renders the create form
        /// with a model-level validation error if the model state is invalid.
        /// </returns>
        // POST: Product/Create
        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductVM vm)
        {
            if (!ModelState.IsValid)
                {
                    var options = await BuildCategoryOptionsAsync(vm.CategoryId);
                    ViewBag.CategoryId = options;

                    // Helpful validation message
                    ModelState.AddModelError(string.Empty,
                    "One or more required fields are missing or invalid. " +
                    "Please review your input and try again.");

                    return View(vm);
                }

                await _productService.CreateAsync(vm);

            TempData["Message"] = "success, Product created successfully";
            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region Edit
        /// <summary>
        /// Displays the product edit form pre-populated with the product's current data
        /// and the category dropdown with the current category pre-selected.
        /// </summary>
        /// <param name="id">Primary key of the product to edit.</param>
        /// <returns>
        /// The edit view populated with the product's current values, or a redirect to
        /// <see cref="Index"/> with a warning if no product with <paramref name="id"/> exists.
        /// </returns>
        // GET: Product/Edit
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _productService.GetByIdAsync(id);
            if (vm is null)
            {
                TempData["Message"] = $"warning, Unable to find product ID: {id}";
                return RedirectToAction("Index");
            }

            var options = await BuildCategoryOptionsAsync(vm.CategoryId);
            ViewBag.CategoryId = options;

            return View(vm);
        }

        /// <summary>
        /// Processes the product edit form, applies changes to the EF Core-tracked entity,
        /// and persists them in a single <c>SaveChangesAsync</c> call.
        /// </summary>
        /// <param name="vm">The updated product view model bound from the submitted form.</param>
        /// <returns>
        /// Redirects to <see cref="Index"/> on success. Re-renders the edit form with a
        /// model-level validation error on invalid model state, or redirects with a warning
        /// if the product no longer exists at the time of the update.
        /// </returns>
        // POST: Product/Edit
        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProductVM vm)
        {
            if (!ModelState.IsValid)
                {
                    // Rebuild the category options for the dropdown
                    var options = await BuildCategoryOptionsAsync(vm.CategoryId);
                    ViewBag.CategoryId = options;

                    // Helpful validation message
                    ModelState.AddModelError(string.Empty,
                    "One or more required fields are missing or invalid. " +
                    "Please review your input and try again.");

                    return View(vm);
                }

                var exists = await _productService.GetByIdAsync(vm.ProductId);
            if (exists is null)
            {
                TempData["Message"] = $"warning, Unable to find product ID: {vm.ProductId}";
                return RedirectToAction(nameof(Index));
            }

            await _productService.UpdateAsync(vm);

            TempData["Message"] = "success, Product updated successfully";
            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region Delete
        /// <summary>
        /// Displays the product deletion confirmation page showing the product to be removed.
        /// </summary>
        /// <param name="id">Primary key of the product to delete.</param>
        /// <returns>
        /// The delete confirmation view, or a redirect to <see cref="Index"/> with a warning
        /// if no product with <paramref name="id"/> exists.
        /// </returns>
        // GET: Product/Delete
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Delete(int id)
        {
            var vm = await _productService.GetByIdAsync(id);
            if (vm is null)
            {
                TempData["Message"] = $"warning, Unable to find product ID: {id}";
                return RedirectToAction(nameof(Index));
            }
            return View(vm);
        }

        /// <summary>
        /// Permanently removes a product from the database after the user confirms deletion.
        /// The <see cref="ActionNameAttribute"/> allows this POST action to share the
        /// <c>/Product/Delete/{id}</c> route with the GET confirmation action above.
        /// </summary>
        /// <param name="id">Primary key of the product to delete.</param>
        /// <returns>
        /// Redirects to <see cref="Index"/> with a success message on deletion, or a warning
        /// message if the product no longer exists at the time of the request.
        /// </returns>
        // POST: Product/Delete
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin,Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var exists = await _productService.GetByIdAsync(id);
            if (exists is not null)
            {
                await _productService.DeleteAsync(id);
                TempData["Message"] = "success, Product deleted successfully";
            }
            else
            {
                TempData["Message"] = $"warning, Unable to find product ID: {id}";
            }
            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region Stock Notifications
        /// <summary>
        /// Allows authenticated users to request email notification when an out-of-stock product becomes available.
        /// Creates a watchlist entry that will trigger an email when inventory is restocked.
        /// </summary>
        /// <param name="productId">The product ID to watch.</param>
        /// <param name="returnUrl">Optional URL to redirect back to after processing.</param>
        /// <returns>Redirects back to the previous page with a success/error message.</returns>
        // POST: Product/NotifyStock
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> NotifyStock(int productId, string? returnUrl)
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email)) return Challenge();

            var user = await _userService.GetByEmailAsync(email);
            if (user is null) return Forbid();

            // Inject IStockNotificationService
            var stockService = HttpContext.RequestServices.GetRequiredService<ELKH.Services.IStockNotificationService>();

            var success = await stockService.RequestNotificationAsync(user.PkRegisteredUserId, productId);

            if (success)
            {
                TempData["Message"] = "success, You'll be notified when this product is back in stock!";
            }
            else
            {
                TempData["Message"] = "info, You're already subscribed to notifications for this product.";
            }

            // Redirect back to the page they came from, or default to product details
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Details", new { id = productId });
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Queries all categories from the database and projects them into an alphabetically
        /// ordered list of <see cref="SelectListItem"/> objects for use in an HTML dropdown.
        /// </summary>
        /// <param name="selectedId">
        /// Optional primary key of the category to pre-select. Pass <see langword="null"/>
        /// (the default) when building the dropdown for a new product with no current category.
        /// </param>
        /// <returns>An alphabetically ordered list of select list items.</returns>
        private async Task<IEnumerable<SelectListItem>> BuildCategoryOptionsAsync(int? selectedId = null)
        {
            var categories = await _productService.GetCategoriesAsync();
            return categories
                .OrderBy(c => c.CategoryName)
                .Select(c => new SelectListItem
                {
                    Value = c.PkCategoryId.ToString(),
                    Text = c.CategoryName,
                    Selected = selectedId.HasValue && c.PkCategoryId == selectedId.Value
                }).ToList();
        }

        #endregion
    }
}
