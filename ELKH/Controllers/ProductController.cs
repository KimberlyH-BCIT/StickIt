using ELKH.Data;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Controllers
{
    /// <summary>
    /// Product catalog management controller.
    /// Handles public product listing/details and admin CRUD operations with caching and search.
    /// </summary>
    /// <remarks>
    /// TABLE OF CONTENTS
    /// ================================================================================
    /// 1. Fields & Constructor
    /// 2. Public Endpoints (No Auth Required)
    ///    - Index()                               // List all products (cached 5 min)
    ///    - Details(id)                           // Product details + reviews (cached 2 min)
    ///    - GetPrice(id)                          // AJAX price polling
    ///    - SearchNames(q)                        // GET: Autocomplete search
    /// 3. Rating Operations (Authenticated)
    ///    - CreateRating()                        // POST: Submit new rating
    ///    - EditRating()                          // POST: Update existing rating
    ///    - DeleteRating()                        // POST: Soft-delete rating
    /// 4. Product CRUD Operations (Admin Role)
    ///    - Create() GET/POST                     // Create new product
    ///    - Edit(id) GET/POST                     // Update product
    ///    - Delete(id) GET/POST                   // Delete product
    /// 5. Private Helpers
    ///    - BuildCategoryOptions()                // Category dropdown builder
    ///    - MapToVM() / MapToEntity()             // ViewModel mapping
    ///    - NormalizeName()                       // String normalization
    /// ================================================================================
    /// 
    /// Public endpoints (no authentication required):
    /// - GET /Product - List all active products with caching (5 min)
    /// - GET /Product/Details/{id} - Product details with reviews and ratings (2 min cache)
    /// - GET /Product/SearchNames?q=term - Autocomplete search results
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
        private readonly ApplicationDbContext _context;
        private readonly ELKH.Services.ISearchService _searchService;
        private readonly ELKH.Services.IProductService _productService;
        private readonly ELKH.Services.IRatingService _ratingService;
        private readonly ELKH.Services.IUserService _userService;

        /// <summary>
        /// Initializes a new instance of <see cref="ProductController"/> with all required services.
        /// </summary>
        /// <param name="context">EF Core database context for direct product and category queries.</param>
        /// <param name="searchService">Full-text and fuzzy search service for product name lookups.</param>
        /// <param name="productService">Product business logic and output-cache service.</param>
        /// <param name="ratingService">Review submission, approval, and purchase-eligibility service.</param>
        /// <param name="userService">Registered user lookup service for identity resolution.</param>
        public ProductController(
            ApplicationDbContext context,
            ELKH.Services.ISearchService searchService,
            ELKH.Services.IProductService productService,
            ELKH.Services.IRatingService ratingService,
            ELKH.Services.IUserService userService)
        {
            _context = context;
            _searchService = searchService;
            _productService = productService;
            _ratingService = ratingService;
            _userService = userService;
        }
        #endregion

        #region Index / Details
        /// <summary>
        /// Displays the complete list of active products, served from the output cache when available.
        /// The cache entry is tagged <c>"products"</c> and expires after 5 minutes,
        /// governed by the <c>ProductList</c> output-cache policy defined at startup.
        /// </summary>
        /// <returns>The product index view populated with all active products.</returns>
        // GET: Product/Index
        [Microsoft.AspNetCore.OutputCaching.OutputCache(PolicyName = "ProductList")]
        public async Task<IActionResult> Index()
        {
            var prods = await _productService.GetAllAsync();
            return View(prods);
        }

        /// <summary>
        /// Displays the detail page for a single product, including approved reviews and
        /// the authenticated user's rating eligibility based on their purchase history.
        /// </summary>
        /// <param name="id">Primary key of the product to display.</param>
        /// <returns>
        /// The product details view, or a redirect to <see cref="Index"/> with a warning
        /// <c>TempData</c> message if no product with <paramref name="id"/> exists.
        /// </returns>
        // GET: Product/Details
        public async Task<IActionResult> Details(int id, int reviewPage = 1)
        {
            var vm = await _productService.GetByIdAsync(id);
            if (vm == null)
            {
                TempData["Message"] = $"warning, Unable to find product ID: {id}";
                return RedirectToAction(nameof(Index));
            }

            // Paged, profile-enriched reviews — also carries AverageRating and TotalCount
            // so the product header can display accurate aggregate stats.
            ViewBag.ReviewPage = await _ratingService.GetPagedApprovedReviewsAsync(id, reviewPage);

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
                        // The user has already rated this product — populate ViewBag so the
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
        /// <c>thumbnail</c>, and <c>matches</c> — an array of <c>{ start, length }</c> ranges
        /// that the client uses to highlight the matched characters in the suggestion text.
        /// Returns an empty array if <paramref name="q"/> is null or whitespace.
        /// </returns>
        // GET: /Product/SearchNames?q=term
        [HttpGet]
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
        public async Task<IActionResult> Create()
        {
            var options = await BuildCategoryOptionsAsync();
            ViewBag.FkCategoryId = options;
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductVM vm)
        {
            if (!ModelState.IsValid)
            {
                var options = await BuildCategoryOptionsAsync(vm.CategoryId);
                ViewBag.FkCategoryId = options;
                ViewBag.CategoryId = options;

                // Helpful validation message
                ModelState.AddModelError(string.Empty,
                "One or more required fields are missing or invalid. " +
                "Please review your input and try again.");

                return View(vm);
            }

            ProductModel product = MapToEntity(vm);
            _context.Products.Add(product);
            // MapToEntity already sets NameNormalized; this line re-applies it after the
            // entity is tracked by EF Core to guard against any post-construction mutation.
            product.NameNormalized = NormalizeName(product.Name);
            await _context.SaveChangesAsync();

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
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.PkProductId == id);

            if (product is null)
            {
                TempData["Message"] = $"warning, Unable to find product ID: {id}";
                return RedirectToAction("Index");
            }

            var options = await BuildCategoryOptionsAsync(product.FkCategoryId);
            ViewBag.FkCategoryId = options;
            ViewBag.CategoryId = options;

            ProductVM vm = MapToVM(product);

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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProductVM vm)
        {
            if (!ModelState.IsValid)
            {
                // Rebuild the category options for the dropdown
                var options = await BuildCategoryOptionsAsync(vm.CategoryId);
                ViewBag.FkCategoryId = options;
                ViewBag.CategoryId = options;

                // Helpful validation message
                ModelState.AddModelError(string.Empty,
                "One or more required fields are missing or invalid. " +
                "Please review your input and try again.");

                return View(vm);
            }

            var product = await _context.Products.FindAsync(vm.ProductId);
            if (product is null)
            {
                TempData["Message"] = $"warning, Unable to find product ID: {vm.ProductId}";
                return RedirectToAction(nameof(Index));
            }

            // Assign only the fields exposed by the form. Explicit property assignment
            // (rather than Attach + SetValues) prevents unintentionally overwriting fields
            // that are not part of the edit view model (e.g. audit timestamps, images).
            product.Name = vm.ProductName;
            product.NameNormalized = NormalizeName(vm.ProductName);
            product.Description = vm.Description;
            product.Price = vm.Price;
            product.StockQuantity = vm.StockQuantity;
            product.IsActive = vm.IsActive;
            product.FkCategoryId = vm.CategoryId;

            await _context.SaveChangesAsync();

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
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.PkProductId == id);

            if (product is null)
            {
                TempData["Message"] = $"warning, Unable to find product ID: {id}";
                return RedirectToAction(nameof(Index));
            }
            
            ProductVM vm = MapToVM(product);

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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product is not null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                TempData["Message"] = "success, Product deleted successfully";
            }
            else
            {
                TempData["Message"] = $"warning, Unable to find product ID: {id}";
            }
            
            return RedirectToAction(nameof(Index));
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
            var categories = await _context.Categories.ToListAsync();
            return categories
                .OrderBy(c => c.CategoryName)
                .Select(c => new SelectListItem
                {
                    Value = c.PkCategoryId.ToString(),
                    Text = c.CategoryName,
                    Selected = selectedId.HasValue && c.PkCategoryId == selectedId.Value
                }).ToList();
        }

        /// <summary>
        /// Maps a <see cref="ProductModel"/> entity to a <see cref="ProductVM"/> view model.
        /// The <c>Category</c> and <c>ProductRatings</c> navigation properties must be
        /// loaded (via <c>Include</c>) before calling this method.
        /// </summary>
        /// <param name="p">The product entity to map.</param>
        /// <returns>A fully populated <see cref="ProductVM"/>.</returns>
        private ProductVM MapToVM(ProductModel p)
        {
            return new ProductVM
            {
                ProductId = p.PkProductId,
                ProductName = p.Name,
                Description = p.Description,
                Price = p.Price,
                DiscountPercent = p.DiscountPercent,
                StockQuantity = p.StockQuantity,
                IsActive = p.IsActive,
                CategoryId = p.FkCategoryId,
                CategoryName = p.Category?.CategoryName ?? "Unknown"
                ,
                // Any() guard prevents the InvalidOperationException that Average() throws
                // on an empty sequence; unrated products default to zero.
                AverageRating = p.ProductRatings != null && p.ProductRatings.Any() ? p.ProductRatings.Average(r => r.Rating) : 0
            };
        }

        /// <summary>
        /// Maps a <see cref="ProductVM"/> view model to a new <see cref="ProductModel"/> entity.
        /// <c>NameNormalized</c> is set immediately so the entity is search-ready
        /// before it is handed to EF Core for persistence.
        /// </summary>
        /// <param name="vm">The view model to map.</param>
        /// <returns>A new <see cref="ProductModel"/> populated from the view model.</returns>
        private ProductModel MapToEntity(ProductVM vm)
        {
            return new ProductModel
            {
                PkProductId = vm.ProductId,
                Name = vm.ProductName,
                NameNormalized = NormalizeName(vm.ProductName),
                Description = vm.Description,
                Price = vm.Price,
                StockQuantity = vm.StockQuantity,
                IsActive = vm.IsActive,
                FkCategoryId = vm.CategoryId
            };
        }

        /// <summary>
        /// Produces a normalized, lowercase, diacritic-free version of a product name
        /// for case-insensitive and accent-insensitive full-text search indexing.
        /// </summary>
        /// <param name="name">The raw product name to normalize.</param>
        /// <returns>
        /// A lowercase string with all Unicode combining marks (accents/diacritics) removed,
        /// or <see cref="string.Empty"/> if <paramref name="name"/> is <see langword="null"/> or empty.
        /// </returns>
        /// <example>
        /// <c>NormalizeName("Cr\u00e8me Br\u00fbl\u00e9e")</c> returns <c>"creme brulee"</c>.
        /// </example>
        private static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;

            // NFD (Canonical Decomposition) splits composite characters into a base letter
            // followed by one or more separate combining-mark code points.
            // Example: "\u00e9" (e-acute) → "e" + "\u0301" (combining acute accent).
            var s = name.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var ch in s)
            {
                var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                // Drop every NonSpacingMark — these are the diacritic/accent code points
                // that NFD separated from their base letters. Keeping only base letters
                // effectively strips all accent marks from the string.
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }

            // NFC (Canonical Composition) re-combines any remaining decomposed sequences
            // into their canonical precomposed forms, then ToLowerInvariant applies
            // culture-independent case folding for consistent index storage.
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant();
        }
        #endregion
    }
}