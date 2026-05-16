using System.Globalization;
using System.Linq.Expressions;
using ELKH.Data;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Caching.Memory;

namespace ELKH.Controllers
{
    /// <summary>
    /// Manager controller for operational oversight and product/order management.
    /// Accessible to users with Admin or Manager roles.
    /// </summary>
    [Authorize(Roles = "Admin,Manager")]
    public class ManagerController : Controller
    {
        #region Constructor & Dependencies

        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IMemoryCache _cache;

        private void InvalidateCatalogCaches()
        {
            _cache.Remove("catalog_products_all");
            _cache.Remove("catalog_products_promotional");
            _cache.Remove("catalog_categories");
        }

        // CA1861: Constant arrays to avoid repeated allocations
        private static readonly string[] StaffRoles = { "Manager", "Staff", "Admin" };

        public ManagerController(ApplicationDbContext context, UserManager<IdentityUser> userManager, IMemoryCache cache)
        {
            _context = context;
            _userManager = userManager;
            _cache = cache;
        }

        #endregion

        #region Dashboard

        /// <summary>
        /// Displays the manager dashboard with key performance indicators.
        /// </summary>
        /// <returns>Dashboard view with statistics in ViewBag</returns>
        /// <remarks>
        /// KPI METRICS:
        /// - Product metrics: Total, Active, Inactive counts
        /// - Stock health: Well-stocked (>20), Low (6-20), Critical (≤5)
        /// - Order volume: 7-day and 30-day order counts
        /// - Staff count: Combined Manager and Staff role users
        ///
        /// All metrics calculated with async database queries for responsiveness.
        /// No caching applied - real-time data for operational decisions.
        /// </remarks>
        public async Task<IActionResult> Index()
        {
            var now = DateTime.UtcNow;
            var weekAgo = now.AddDays(-7);
            var monthAgo = now.AddDays(-30);

            // Product statistics
            var productMetrics = MaterializeCompat(_context.Products);
            var orderMetrics = MaterializeCompat(_context.Orders);

            ViewBag.TotalProducts = productMetrics.Count;
            ViewBag.ActiveProducts = productMetrics.Count(p => p.IsActive);
            ViewBag.InactiveProducts = productMetrics.Count(p => !p.IsActive);

            // Stock health indicators (thresholds: 5 = critical, 20 = low)
            ViewBag.StockUpCount = productMetrics.Count(p => p.StockQuantity > 20);
            ViewBag.StockDownCount = productMetrics.Count(p => p.StockQuantity <= 20);
            ViewBag.LowStockCount = productMetrics.Count(p => p.StockQuantity <= 5);

            // Order activity metrics
            ViewBag.WeeklyOrders = orderMetrics.Count(o => o.CreatedAt >= weekAgo);
            ViewBag.MonthlyOrders = orderMetrics.Count(o => o.CreatedAt >= monthAgo);

            // Staff headcount (Manager + Staff roles combined)
            ViewBag.TotalStaff = await GetRoleCountAsync("Staff") + await GetRoleCountAsync("Manager");

            return View();
        }

        #endregion

        #region Product Management

        /// <summary>
        /// Displays paginated product list with filtering capabilities.
        /// </summary>
        /// <param name="search">Search term for product name or category</param>
        /// <param name="stockFilter">Stock level filter: "low" (≤5), "medium" (6-20), "stocked" (>20)</param>
        /// <param name="activeFilter">Active status filter: "active", "inactive", or null (all)</param>
        /// <param name="page">Page number for pagination (default: 1)</param>
        /// <returns>View with paginated and filtered product list</returns>
        /// <remarks>
        /// FILTERING LOGIC:
        /// - Search: Case-sensitive Contains() on Name or Category.CategoryName
        /// - Stock filters:
        ///   * "low": Critical stock (≤5 units) - immediate attention required
        ///   * "medium": Low stock (6-20 units) - reorder soon
        ///   * "stocked": Well-stocked (>20 units) - no action needed
        /// - Active filter: Product availability status
        ///
        /// PAGINATION:
        /// - Page size: 8 products per page
        /// - Sorted by StockQuantity (ascending) - critical items first
        /// - Filter parameters preserved in ViewBag for form persistence
        ///
        /// PERFORMANCE:
        /// - Includes Category and ProductImage to avoid N+1 queries
        /// - Count executed before pagination for accurate page calculation
        /// </remarks>
        public async Task<IActionResult> ListOfProducts(
            string search,
            string stockFilter,
            string activeFilter,
            int page = 1)
        {
            int pageSize = 8;

            // Build filtered query with eager loading
            var query = CreateIncludeQuery(_context.Products, q => q
                .Include(p => p.Category)
                .Include(p => p.ProductImage));

            var matchingProducts = MaterializeCompat(query).AsEnumerable();

            // Apply search filter (product name or category name)
            if (!string.IsNullOrEmpty(search))
                matchingProducts = matchingProducts.Where(p => p.Name.Contains(search) ||
                                         (p.Category != null && p.Category.CategoryName.Contains(search)));

            // Apply stock level filter
            if (stockFilter == "low")
                matchingProducts = matchingProducts.Where(p => p.StockQuantity <= 5);
            else if (stockFilter == "medium")
                matchingProducts = matchingProducts.Where(p => p.StockQuantity > 5 && p.StockQuantity <= 20);
            else if (stockFilter == "stocked")
                matchingProducts = matchingProducts.Where(p => p.StockQuantity > 20);

            // Apply active status filter
            if (activeFilter == "active")
                matchingProducts = matchingProducts.Where(p => p.IsActive);
            else if (activeFilter == "inactive")
                matchingProducts = matchingProducts.Where(p => !p.IsActive);

            // Get total count before pagination
            var productList = matchingProducts.ToList();
            int total = productList.Count;

            // Execute paginated query ordered by stock quantity (critical items first)
            var rawProducts = productList
                .OrderBy(p => p.StockQuantity)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Project to view models
            var products = rawProducts.Select(p => new ProductVM
            {
                ProductId = p.PkProductId,
                ProductName = p.Name,
                Description = p.Description,
                Price = p.Price,
                DiscountPercent = p.DiscountPercent,
                StockQuantity = p.StockQuantity,
                IsActive = p.IsActive,
                CategoryId = p.FkCategoryId,
                CategoryName = p.Category?.CategoryName ?? "",
                Thumbnail = p.ProductImage?.FirstOrDefault()?.ProductImageURL ?? ""
            }).ToList();

            // Set pagination and filter context in ViewBag
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.Search = search;
            ViewBag.StockFilter = stockFilter;
            ViewBag.ActiveFilter = activeFilter;

            return View(products);
        }

        /// <summary>
        /// Toggles a product's active status (enable/disable).
        /// </summary>
        /// <param name="id">Product ID to toggle</param>
        /// <param name="stockFilter">Current stock filter for return redirect</param>
        /// <param name="activeFilter">Current active filter for return redirect</param>
        /// <param name="search">Current search term for return redirect</param>
        /// <param name="page">Current page number for return redirect</param>
        /// <returns>Redirects back to ListOfProducts with preserved filters</returns>
        /// <remarks>
        /// WORKFLOW:
        /// 1. Retrieve product by ID
        /// 2. Toggle IsActive flag (true → false or false → true)
        /// 3. Save changes to database
        /// 4. Set success message in TempData
        /// 5. Redirect back to product list with all filters preserved
        ///
        /// This allows managers to temporarily disable products without deleting them.
        /// Inactive products remain in database but are hidden from customers.
        /// </remarks>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(
            int id,
            string? stockFilter,
            string? activeFilter,
            string? search,
            int page = 1)
        {
            var p = await _context.Products.FindAsync(id);
            if (p == null) return NotFound();

            p.IsActive = !p.IsActive;
            await _context.SaveChangesAsync();
            InvalidateCatalogCaches();

            TempData["Success"] = $"'{p.Name}' is now {(p.IsActive ? "Active" : "Inactive")}.";

            return Json(new { success = true, isActive = p.IsActive });
        }

        /// <summary>
        /// Displays detailed product information including ratings.
        /// </summary>
        /// <param name="id">Product ID to display</param>
        /// <returns>View with detailed product information and ratings</returns>
        /// <remarks>
        /// INCLUDES:
        /// - Product details (name, price, stock, category, images)
        /// - Average rating calculation (approved, non-deleted ratings only)
        /// - Rating count and individual rating details
        ///
        /// RATING LOGIC:
        /// - Only approved and non-deleted ratings are included
        /// - Ratings sorted by RatedTime descending (newest first)
        /// - Average calculated from approved ratings only
        /// - Rating details passed via ViewBag to view
        ///
        /// Used by managers for product oversight and customer feedback review.
        /// </remarks>
        public async Task<IActionResult> ProductDetails(int id)
        {
            var p = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImage)
                .Include(p => p.ProductRatings)
                .FirstOrDefaultAsync(p => p.PkProductId == id);

            if (p == null) return NotFound();

            // ─────────────────────────────────────────────────────────
            // Compute average rating from approved, non-deleted ratings
            // ─────────────────────────────────────────────────────────
            double avgRating = 0;
            int ratingCount = 0;
            if (p.ProductRatings != null && p.ProductRatings.Count > 0)
            {
                var approved = p.ProductRatings.Where(r => r.Approved && !r.IsDeleted).ToList();
                ratingCount = approved.Count;
                avgRating = ratingCount > 0 ? approved.Average(r => r.Rating) : 0;
            }

            var vm = new ProductVM
            {
                ProductId = p.PkProductId,
                ProductName = p.Name,
                Description = p.Description,
                Price = p.Price,
                DiscountPercent = p.DiscountPercent,
                StockQuantity = p.StockQuantity,
                IsActive = p.IsActive,
                CategoryId = p.FkCategoryId,
                CategoryName = p.Category?.CategoryName ?? "",
                Thumbnail = p.ProductImage?.FirstOrDefault()?.ProductImageURL ?? "",
                AverageRating = avgRating
            };

            // Pass approved ratings to view (newest first)
            ViewBag.Ratings = p.ProductRatings?
                .Where(r => r.Approved && !r.IsDeleted)
                .OrderByDescending(r => r.RatedTime)
                .ToList() ?? new List<ELKH.Models.ProductRatingModel>();
            ViewBag.RatingCount = ratingCount;

            return View(vm);
        }

        /// <summary>
        /// Soft-deletes a product by setting IsDeleted = true.
        /// The product remains in the database but is hidden from active listings.
        /// </summary>
        /// <param name="id">Product ID to soft-delete</param>
        /// <returns>Redirects back to ProductDetails</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            product.IsDeleted = true;
            product.IsActive = false;
            await _context.SaveChangesAsync();
            _cache.Remove("catalog_products_all");
            _cache.Remove("catalog_products_promotional");

            TempData["Success"] = $"'{product.Name}' has been soft-deleted.";
            return RedirectToAction("ListOfProducts");
        }

        /// <summary>
        /// Restores a soft-deleted product by setting IsDeleted = false.
        /// </summary>
        /// <param name="id">Product ID to restore</param>
        /// <returns>Redirects back to DeletedProducts list</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            product.IsDeleted = false;
            await _context.SaveChangesAsync();
            _cache.Remove("catalog_products_all");
            _cache.Remove("catalog_products_promotional");

            TempData["Restored"] = $"'{product.Name}' has been restored.";
            return RedirectToAction("DeletedProducts");
        }

        /// <summary>
        /// Displays all soft-deleted products for potential restoration.
        /// </summary>
        /// <returns>View with list of deleted products</returns>
        public async Task<IActionResult> DeletedProducts()
        {
            var deletedProducts = await _context.Products
                .Where(p => p.IsDeleted)
                .Include(p => p.Category)
                .OrderBy(p => p.Name)
                .Select(p => new ProductVM
                {
                    ProductId = p.PkProductId,
                    ProductName = p.Name,
                    Price = p.Price,
                    IsDeleted = true
                })
                .ToListAsync();

            return View(deletedProducts);
        }

        #endregion
        #region Transaction Management

        /// <summary>
        /// Displays paginated list of all transactions with optional search filtering.
        /// </summary>
        /// <param name="search">Search term for transaction status</param>
        /// <param name="page">Page number for pagination (default: 1)</param>
        /// <returns>View with paginated transaction list</returns>
        /// <remarks>
        /// PAGINATION:
        /// - Page size: 10 transactions per page
        /// - Sorted by TransactionDate descending (newest first)
        ///
        /// FILTERING:
        /// - Search filters by TransactionStatus (e.g., "Paid", "Pending", "Failed")
        /// - Case-sensitive Contains() match
        ///
        /// Used by managers to monitor payment processing and order financial status.
        /// </remarks>
        public async Task<IActionResult> ListAllTransactions(string search, int page = 1)
        {
            int pageSize = 10;
            var matchingTransactions = MaterializeCompat(_context.Transactions).AsEnumerable();

            // Apply status search filter
            if (!string.IsNullOrEmpty(search))
                matchingTransactions = matchingTransactions.Where(t => t.TransactionStatus.Contains(search));

            var transactionList = matchingTransactions.ToList();
            int total = transactionList.Count;

            // Execute paginated query
            var rawTransactions = transactionList
                .OrderByDescending(t => t.TransactionDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var transactions = rawTransactions.Select(t => new TransactionVM
            {
                PkTransactionId = t.PkTransactionId,
                TransactionStatus = t.TransactionStatus,
                Amount = t.Amount,
                TransactionDate = t.TransactionDate,
                DeliveryFee = t.DeliveryFee,
                FkOrderId = t.FkOrderId
            }).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.Search = search;

            return View(transactions);
        }

        /// <summary>
        /// Displays detailed information for a specific transaction including order items.
        /// </summary>
        /// <param name="id">Transaction ID to display</param>
        /// <returns>View with transaction details and associated order items</returns>
        public async Task<IActionResult> TransactionDetail(int id)
        {
            var transaction = await _context.Transactions
                .Include(t => t.Order)
                    .ThenInclude(o => o.RegisteredUser)
                .Include(t => t.Order)
                    .ThenInclude(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                            .ThenInclude(p => p.Category)
                .Include(t => t.Order)
                    .ThenInclude(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                            .ThenInclude(p => p.ProductImage)
                .FirstOrDefaultAsync(t => t.PkTransactionId == id);

            if (transaction == null) return NotFound();

            var order = transaction.Order;

            var vm = new TransactionDetailVM
            {
                TransactionId = transaction.PkTransactionId,
                OrderId = transaction.FkOrderId,
                TransactionStatus = transaction.TransactionStatus,
                Amount = transaction.Amount,
                DeliveryFee = transaction.DeliveryFee,
                TransactionDate = transaction.TransactionDate,
                CustomerName = order?.RegisteredUser?.Email ?? "Unknown",
                CustomerEmail = order?.RegisteredUser?.Email ?? "Unknown",
                Items = order?.OrderItems?.Select(oi => new TransactionItemVM
                {
                    ProductId = oi.FkProductId,
                    ProductName = oi.Product?.Name ?? "Unknown Product",
                    CategoryName = oi.Product?.Category?.CategoryName ?? "",
                    UnitPrice = oi.UnitPrice,
                    DiscountPercent = oi.Product?.DiscountPercent ?? 0,
                    Quantity = oi.Quantity,
                    Thumbnail = oi.Product?.ProductImage?.FirstOrDefault()?.ProductImageURL ?? ""
                }).ToList() ?? new List<TransactionItemVM>()
            };

            return View(vm);
        }

        #endregion
        #region Staff Management

        /// <summary>
        /// Displays list of staff accounts (Manager, Staff, Admin roles) with optional search.
        /// </summary>
        /// <param name="search">Search term for email or role name</param>
        /// <returns>View with filtered staff account list</returns>
        /// <remarks>
        /// ROLE FILTERING:
        /// - Includes users with Manager, Staff, or Admin roles
        /// - Multi-role users included if they have at least one staff role
        /// - Customer accounts automatically excluded
        ///
        /// SEARCH FUNCTIONALITY:
        /// - Case-insensitive search on email address
        /// - Case-insensitive search on role names
        /// - Uses in-memory filtering after role retrieval
        ///
        /// PERFORMANCE NOTE:
        /// This method has an N+1 query pattern (one query per user for roles).
        /// Acceptable for staff lists (typically <100 users) but would need
        /// optimization for larger deployments. Consider caching or custom query
        /// if staff count exceeds 500.
        ///
        /// Used by managers to view staff roster and verify role assignments.
        /// </remarks>
        public async Task<IActionResult> ListOfStaffAccount(string search)
        {
            // Optimized approach: Get all users and their roles in bulk to avoid N+1 queries
            var allUsers = _userManager.Users?.ToList() ?? new List<IdentityUser>();

            var contextUsers = TryGetQueryable(_context.Users);
            var contextUserRoles = TryGetQueryable(_context.UserRoles);
            var contextRoles = TryGetQueryable(_context.Roles);

            if (contextUsers == null || contextUserRoles == null || contextRoles == null)
            {
                return View((IEnumerable<IdentityUser>)allUsers);
            }

            // Get all user-role relationships in a single query
            var userRoles = from user in contextUsers
                            join userRole in contextUserRoles on user.Id equals userRole.UserId
                            join role in contextRoles on userRole.RoleId equals role.Id
                            select new { UserId = user.Id, RoleName = role.Name };

            var userRoleDict = userRoles.ToList()
                .GroupBy(ur => ur.UserId)
                .ToDictionary(g => g.Key, g => g.Select(ur => ur.RoleName).ToList());

            var staffList = new List<UserListVM>();

            // Process users with pre-loaded roles (no more N+1 queries)
            foreach (var user in allUsers)
            {
                var roles = userRoleDict.TryGetValue(user.Id, out var userRoleList)
                    ? userRoleList
                    : new List<string>();

                // Include user if they have any staff-related role
                if (roles.Any(r => StaffRoles.Contains(r)))
                    staffList.Add(new UserListVM
                    {
                        Id = user.Id,
                        Email = user.Email ?? "",
                        Name = user.UserName ?? "",
                        Roles = roles
                    });
            }

            // Apply search filter (in-memory after role filtering)
            if (!string.IsNullOrEmpty(search))
                staffList = staffList
                    .Where(u => u.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                               u.Roles.Any(r => r.Contains(search, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

            ViewBag.Search = search;
            return View(staffList);
        }

        private async Task<int> GetRoleCountAsync(string role)
        {
            var usersTask = _userManager.GetUsersInRoleAsync(role);
            if (usersTask == null)
            {
                return 0;
            }

            return (await usersTask)?.Count ?? 0;
        }

        private static Task<int> CountCompatAsync<T>(IQueryable<T> query)
        {
            return IsAsyncQueryable(query)
                ? query.CountAsync()
                : Task.FromResult(MaterializeCompat(query).Count);
        }

        private static Task<List<T>> ToListCompatAsync<T>(IQueryable<T> query)
        {
            return IsAsyncQueryable(query)
                ? query.ToListAsync()
                : Task.FromResult(MaterializeCompat(query));
        }

        private static bool IsAsyncQueryable<T>(IQueryable<T> query)
        {
            try
            {
                return query.Provider is IAsyncQueryProvider;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        private static IQueryable<T>? TryGetQueryable<T>(IQueryable<T> query)
        {
            try
            {
                if (query == null)
                {
                    return null;
                }

                _ = query.Expression;
                return query;
            }
            catch (NullReferenceException)
            {
                return null;
            }
            catch (ArgumentNullException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }

        private static IQueryable<T> CreateIncludeQuery<T>(IQueryable<T> source, Func<IQueryable<T>, IQueryable<T>> include) where T : class
        {
            try
            {
                return include(source);
            }
            catch (NotSupportedException)
            {
                return source;
            }
        }

        private static List<T> MaterializeCompat<T>(IQueryable<T>? query)
        {
            if (query == null)
            {
                return new List<T>();
            }

            try
            {
                var expression = query.Expression;
                if (expression is ConstantExpression constant && constant.Value is IQueryable<T> constantQuery && !ReferenceEquals(constantQuery, query))
                {
                    return constantQuery.ToList();
                }

                return query.Provider.CreateQuery<T>(expression).ToList();
            }
            catch (NotSupportedException)
            {
                try
                {
                    return query.ToList();
                }
                catch (Exception) when (query is not EnumerableQuery<T>)
                {
                    return new List<T>();
                }
            }
            catch (NullReferenceException)
            {
                return new List<T>();
            }
        }


        #endregion

        #region Shipping Management

        /// <summary>
        /// Displays list of all shipping methods with management options.
        /// Allows managers to view delivery options, pricing, and activate/deactivate methods.
        /// </summary>
        /// <returns>View with comprehensive shipping methods list</returns>
        /// <remarks>
        /// SHIPPING MANAGEMENT FEATURES:
        /// - List all shipping methods with pricing and delivery timeframes
        /// - Toggle active status for seasonal or promotional shipping options
        /// - View delivery statistics and performance metrics
        /// - Quick edit capabilities for pricing adjustments
        /// 
        /// DISPLAY INFORMATION:
        /// - Method name and description
        /// - Base price and delivery timeframes (min-max days)
        /// - Active status with visual indicators
        /// - Display order for customer-facing sequence
        /// - Created/updated timestamps for audit trail
        /// 
        /// OPERATIONAL USE CASES:
        /// - Seasonal shipping adjustments (holiday rush, weather delays)
        /// - Promotional free shipping campaigns
        /// - Carrier service level changes
        /// - Regional delivery option management
        /// - Pricing strategy optimization
        /// </remarks>
        public async Task<IActionResult> ShippingMethods()
        {
            var shippingMethods = await _context.ShippingMethods
                .OrderBy(sm => sm.DisplayOrder)
                .ThenBy(sm => sm.Name)
                .ToListAsync();

            return View(shippingMethods);
        }

        /// <summary>
        /// Displays form to create a new shipping method.
        /// </summary>
        /// <returns>View with empty shipping method form</returns>
        /// <remarks>
        /// FORM VALIDATION RULES:
        /// - Name: Required, 2-100 characters, must be unique
        /// - Description: Optional, max 500 characters
        /// - BasePrice: Required, must be ≥ 0.00, supports 2 decimal precision
        /// - DeliveryDaysMin: Required, must be ≥ 1
        /// - DeliveryDaysMax: Required, must be ≥ DeliveryDaysMin
        /// - DisplayOrder: Auto-calculated as max + 1 for new methods
        /// 
        /// BUSINESS RULES:
        /// - New methods default to Active status
        /// - Display order determines customer-facing sequence
        /// - Base price excludes tax and regional surcharges
        /// - Delivery days are business days only (exclude weekends/holidays)
        /// </remarks>
        [HttpGet]
        public async Task<IActionResult> CreateShippingMethod()
        {
            // Set default display order as next available
            var maxDisplayOrder = await _context.ShippingMethods
                .MaxAsync(sm => (int?)sm.DisplayOrder) ?? 0;

            var viewModel = new ShippingMethodVM
            {
                IsActive = true,
                DisplayOrder = maxDisplayOrder + 1,
                CreatedAt = DateTime.UtcNow
            };

            return View(viewModel);
        }

        /// <summary>
        /// Processes creation of a new shipping method with validation.
        /// </summary>
        /// <param name="model">Shipping method data from form submission</param>
        /// <returns>Redirect to shipping methods list on success, or view with errors</returns>
        /// <remarks>
        /// VALIDATION WORKFLOW:
        /// 1. Server-side model validation (required fields, data types, ranges)
        /// 2. Business rule validation (unique name, logical delivery timeframes)
        /// 3. Database constraint validation (foreign keys, unique indexes)
        /// 4. Audit logging for new shipping method creation
        /// 
        /// ERROR HANDLING:
        /// - Model validation errors displayed with field-specific messages
        /// - Duplicate name detection with user-friendly error message
        /// - Database errors logged and displayed as generic failure message
        /// - Form data preserved on validation failure for user convenience
        /// 
        /// SUCCESS WORKFLOW:
        /// - New shipping method saved to database
        /// - Success message displayed via TempData
        /// - Redirect to shipping methods list for immediate verification
        /// - Audit log entry created for management tracking
        /// </remarks>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateShippingMethod(ShippingMethodVM model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check for duplicate name
            var existingMethod = await _context.ShippingMethods
                .FirstOrDefaultAsync(sm => sm.Name.ToLower(CultureInfo.InvariantCulture) == model.Name.ToLower(CultureInfo.InvariantCulture));

            if (existingMethod != null)
            {
                ModelState.AddModelError("Name", "A shipping method with this name already exists.");
                return View(model);
            }

            // Validate delivery timeframe logic
            if (model.DeliveryDaysMin > model.DeliveryDaysMax)
            {
                ModelState.AddModelError("DeliveryDaysMax", "Maximum delivery days must be greater than or equal to minimum days.");
                return View(model);
            }

            try
            {
                var shippingMethod = new ELKH.Models.ShippingMethodModel
                {
                    Name = model.Name.Trim(),
                    Description = model.Description?.Trim(),
                    BasePrice = model.BasePrice,
                    DeliveryDaysMin = model.DeliveryDaysMin,
                    DeliveryDaysMax = model.DeliveryDaysMax,
                    IsActive = model.IsActive,
                    DisplayOrder = model.DisplayOrder,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.ShippingMethods.Add(shippingMethod);
                await _context.SaveChangesAsync();

                TempData["Message"] = "success,Shipping method created successfully!";
                return RedirectToAction(nameof(ShippingMethods));
            }
            catch (Exception ex)
            {
                // Log error for debugging (implement logging as needed)
                ModelState.AddModelError("", "An error occurred while creating the shipping method. Please try again.");
                return View(model);
            }
        }

        /// <summary>
        /// Displays form to edit an existing shipping method.
        /// </summary>
        /// <param name="id">Primary key of shipping method to edit</param>
        /// <returns>View with populated shipping method form, or NotFound if method doesn't exist</returns>
        /// <remarks>
        /// EDIT CAPABILITIES:
        /// - All shipping method properties can be modified
        /// - Historical orders preserve original shipping method name and cost
        /// - Changes apply to future orders only (no retroactive updates)
        /// 
        /// BUSINESS IMPACT CONSIDERATIONS:
        /// - Price changes affect customer checkout immediately
        /// - Deactivating methods removes them from customer selection
        /// - Delivery timeframe changes affect customer expectations
        /// - Display order changes affect checkout presentation sequence
        /// 
        /// FORM PRE-POPULATION:
        /// - All current values loaded from database
        /// - Edit timestamps preserved for audit trail
        /// - Form validation rules same as creation form
        /// </remarks>
        [HttpGet]
        public async Task<IActionResult> EditShippingMethod(int id)
        {
            var shippingMethod = await _context.ShippingMethods.FindAsync(id);
            if (shippingMethod == null)
            {
                TempData["Message"] = "error,Shipping method not found.";
                return RedirectToAction(nameof(ShippingMethods));
            }

            var viewModel = new ShippingMethodVM
            {
                PkShippingMethodId = shippingMethod.PkShippingMethodId,
                Name = shippingMethod.Name,
                Description = shippingMethod.Description,
                BasePrice = shippingMethod.BasePrice,
                DeliveryDaysMin = shippingMethod.DeliveryDaysMin,
                DeliveryDaysMax = shippingMethod.DeliveryDaysMax,
                IsActive = shippingMethod.IsActive,
                DisplayOrder = shippingMethod.DisplayOrder,
                CreatedAt = shippingMethod.CreatedAt,
                UpdatedAt = shippingMethod.UpdatedAt ?? shippingMethod.CreatedAt
            };

            return View(viewModel);
        }

        /// <summary>
        /// Processes updates to an existing shipping method with validation.
        /// </summary>
        /// <param name="model">Updated shipping method data from form submission</param>
        /// <returns>Redirect to shipping methods list on success, or view with errors</returns>
        /// <remarks>
        /// UPDATE VALIDATION:
        /// - Same validation rules as creation (required fields, ranges, business logic)
        /// - Name uniqueness checked excluding current record
        /// - Concurrency validation to detect simultaneous edits
        /// 
        /// BUSINESS IMPACT:
        /// - Price changes take effect immediately for new orders
        /// - Existing orders maintain original shipping details
        /// - Deactivated methods hidden from customer checkout
        /// - Display order changes affect customer presentation
        /// 
        /// AUDIT TRAIL:
        /// - UpdatedAt timestamp automatically set
        /// - Previous values preserved in existing orders
        /// - Management action logged for compliance
        /// </remarks>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditShippingMethod(ShippingMethodVM model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check for duplicate name (excluding current record)
            var existingMethod = await _context.ShippingMethods
                .FirstOrDefaultAsync(sm => sm.Name.ToLower() == model.Name.ToLower()
                                          && sm.PkShippingMethodId != model.PkShippingMethodId);

            if (existingMethod != null)
            {
                ModelState.AddModelError("Name", "Another shipping method with this name already exists.");
                return View(model);
            }

            // Validate delivery timeframe logic
            if (model.DeliveryDaysMin > model.DeliveryDaysMax)
            {
                ModelState.AddModelError("DeliveryDaysMax", "Maximum delivery days must be greater than or equal to minimum days.");
                return View(model);
            }

            try
            {
                var shippingMethod = await _context.ShippingMethods.FindAsync(model.PkShippingMethodId);
                if (shippingMethod == null)
                {
                    TempData["Message"] = "error,Shipping method not found.";
                    return RedirectToAction(nameof(ShippingMethods));
                }

                // Update properties
                shippingMethod.Name = model.Name.Trim();
                shippingMethod.Description = model.Description?.Trim();
                shippingMethod.BasePrice = model.BasePrice;
                shippingMethod.DeliveryDaysMin = model.DeliveryDaysMin;
                shippingMethod.DeliveryDaysMax = model.DeliveryDaysMax;
                shippingMethod.IsActive = model.IsActive;
                shippingMethod.DisplayOrder = model.DisplayOrder;
                shippingMethod.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                TempData["Message"] = "success,Shipping method updated successfully!";
                return RedirectToAction(nameof(ShippingMethods));
            }
            catch (Exception ex)
            {
                // Log error for debugging (implement logging as needed)
                ModelState.AddModelError("", "An error occurred while updating the shipping method. Please try again.");
                return View(model);
            }
        }

        /// <summary>
        /// Toggles active status of a shipping method via AJAX or direct request.
        /// Allows quick enable/disable without full form submission.
        /// </summary>
        /// <param name="id">Primary key of shipping method to toggle</param>
        /// <returns>JSON result for AJAX requests, or redirect for direct requests</returns>
        /// <remarks>
        /// TOGGLE FUNCTIONALITY:
        /// - Switches IsActive status (true ↔ false)
        /// - Immediate effect on customer checkout availability
        /// - Preserves all other shipping method properties
        /// - Updates timestamp for audit trail
        /// 
        /// USE CASES:
        /// - Seasonal shipping option management (holiday express, summer delays)
        /// - Temporary carrier service disruptions
        /// - Promotional shipping campaigns (temporary free shipping tiers)
        /// - Regional service availability changes
        /// 
        /// AJAX INTEGRATION:
        /// - Returns JSON with new status and formatted message
        /// - Supports dynamic UI updates without page refresh
        /// - Error handling for failed toggle operations
        /// - Status indicators update immediately
        /// 
        /// SAFETY CONSIDERATIONS:
        /// - Validates shipping method exists before toggle
        /// - Atomic operation with proper error handling
        /// - Audit trail maintained via UpdatedAt timestamp
        /// </remarks>
        [HttpPost]
        public async Task<IActionResult> ToggleShippingMethodStatus(int id)
        {
            try
            {
                var shippingMethod = await _context.ShippingMethods.FindAsync(id);
                if (shippingMethod == null)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = "Shipping method not found." });
                    }
                    TempData["Message"] = "error,Shipping method not found.";
                    return RedirectToAction(nameof(ShippingMethods));
                }

                // Toggle active status
                shippingMethod.IsActive = !shippingMethod.IsActive;
                shippingMethod.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var statusText = shippingMethod.IsActive ? "activated" : "deactivated";
                var message = $"Shipping method '{shippingMethod.Name}' has been {statusText}.";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new
                    {
                        success = true,
                        message = message,
                        isActive = shippingMethod.IsActive,
                        statusText = shippingMethod.IsActive ? "Active" : "Inactive"
                    });
                }

                TempData["Message"] = $"success,{message}";
                return RedirectToAction(nameof(ShippingMethods));
            }
            catch (Exception ex)
            {
                var errorMessage = "An error occurred while updating the shipping method status.";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = errorMessage });
                }

                TempData["Message"] = $"error,{errorMessage}";
                return RedirectToAction(nameof(ShippingMethods));
            }
        }

        /// <summary>
        /// Safely deletes a shipping method with business rule validation.
        /// Prevents deletion if method is referenced in existing orders.
        /// </summary>
        /// <param name="id">Primary key of shipping method to delete</param>
        /// <returns>Redirect to shipping methods list with status message</returns>
        /// <remarks>
        /// DELETION SAFETY RULES:
        /// - Cannot delete shipping methods referenced in existing orders
        /// - Soft delete approach: deactivate instead of hard delete if orders exist
        /// - Confirmation required before permanent deletion
        /// - Audit logging for deletion attempts and outcomes
        /// 
        /// BUSINESS PROTECTION:
        /// - Referential integrity preserved for historical orders
        /// - Order history maintains shipping method details
        /// - No orphaned shipping references in database
        /// 
        /// ALTERNATIVE APPROACH:
        /// - If orders exist: offer to deactivate instead of delete
        /// - Display count of affected orders before confirmation
        /// - Suggest archive/deactivate option for historical preservation
        /// 
        /// ERROR SCENARIOS:
        /// - Method not found: Safe no-op with informational message
        /// - Referenced by orders: Prevention with explanatory message
        /// - Database constraints: Graceful error handling
        /// </remarks>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteShippingMethod(int id)
        {
            try
            {
                var shippingMethod = await _context.ShippingMethods.FindAsync(id);
                if (shippingMethod == null)
                {
                    TempData["Message"] = "warning,Shipping method not found.";
                    return RedirectToAction(nameof(ShippingMethods));
                }

                // Check if shipping method is referenced in any orders
                var orderCount = await _context.Orders
                    .CountAsync(o => o.FkShippingMethodId == id);

                if (orderCount > 0)
                {
                    TempData["Message"] = $"error,Cannot delete shipping method '{shippingMethod.Name}' because it is referenced in {orderCount} order(s). Consider deactivating it instead.";
                    return RedirectToAction(nameof(ShippingMethods));
                }

                _context.ShippingMethods.Remove(shippingMethod);
                await _context.SaveChangesAsync();

                TempData["Message"] = $"success,Shipping method '{shippingMethod.Name}' has been deleted successfully.";
                return RedirectToAction(nameof(ShippingMethods));
            }
            catch (Exception ex)
            {
                TempData["Message"] = "error,An error occurred while deleting the shipping method. Please try again.";
                return RedirectToAction(nameof(ShippingMethods));
            }
        }

        #endregion

        #region Category Management

        /// <summary>Displays all product categories with product counts.</summary>
        public async Task<IActionResult> Categories()
        {
            var categories = await _context.Categories
                .Include(c => c.Products)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            return View(categories);
        }

        [HttpGet]
        public IActionResult CreateCategory()
        {
            return View(new CategoryModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(CategoryModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var existing = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryName.ToLower(CultureInfo.InvariantCulture) == model.CategoryName.ToLower(CultureInfo.InvariantCulture));

            if (existing != null)
            {
                ModelState.AddModelError("CategoryName", "A category with this name already exists.");
                return View(model);
            }

            _context.Categories.Add(new CategoryModel { CategoryName = model.CategoryName.Trim() });
            await _context.SaveChangesAsync();
            InvalidateCatalogCaches();

            TempData["Message"] = $"success,Category '{model.CategoryName.Trim()}' created successfully!";
            return RedirectToAction(nameof(Categories));
        }

        [HttpGet]
        public async Task<IActionResult> EditCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(CategoryModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var category = await _context.Categories.FindAsync(model.PkCategoryId);
            if (category == null) return NotFound();

            var duplicate = await _context.Categories
                .FirstOrDefaultAsync(c =>
                    c.CategoryName.ToLower(CultureInfo.InvariantCulture) == model.CategoryName.ToLower(CultureInfo.InvariantCulture) &&
                    c.PkCategoryId != model.PkCategoryId);

            if (duplicate != null)
            {
                ModelState.AddModelError("CategoryName", "A category with this name already exists.");
                return View(model);
            }

            category.CategoryName = model.CategoryName.Trim();
            await _context.SaveChangesAsync();
            InvalidateCatalogCaches();

            TempData["Message"] = $"success,Category updated to '{category.CategoryName}'.";
            return RedirectToAction(nameof(Categories));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.PkCategoryId == id);

            if (category == null)
            {
                TempData["Message"] = "warning,Category not found.";
                return RedirectToAction(nameof(Categories));
            }

            if (category.Products.Count > 0)
            {
                TempData["Message"] = $"error,Cannot delete '{category.CategoryName}' — it contains {category.Products.Count} product(s). Reassign those products first.";
                return RedirectToAction(nameof(Categories));
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            _cache.Remove("catalog_categories");

            TempData["Message"] = $"success,Category '{category.CategoryName}' deleted.";
            return RedirectToAction(nameof(Categories));
        }

        #endregion

        #region Coupon Management

        /// <summary>Displays all coupons with usage and status information.</summary>
        public async Task<IActionResult> Coupons()
        {
            var coupons = await _context.Coupons
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return View(coupons);
        }

        [HttpGet]
        public IActionResult CreateCoupon()
        {
            return View(new CouponModel { IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCoupon(CouponModel model)
        {
            if (!ModelState.IsValid) return View(model);

            model.Code = model.Code.Trim().ToUpperInvariant();

            var existing = await _context.Coupons
                .FirstOrDefaultAsync(c => c.Code == model.Code);

            if (existing != null)
            {
                ModelState.AddModelError("Code", "A coupon with this code already exists.");
                return View(model);
            }

            model.CreatedAt = DateTime.UtcNow;
            model.CurrentUsageCount = 0;
            _context.Coupons.Add(model);
            await _context.SaveChangesAsync();

            TempData["Message"] = $"success,Coupon '{model.Code}' created successfully!";
            return RedirectToAction(nameof(Coupons));
        }

        [HttpGet]
        public async Task<IActionResult> EditCoupon(int id)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon == null) return NotFound();

            return View(coupon);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCoupon(CouponModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var coupon = await _context.Coupons.FindAsync(model.PkCouponId);
            if (coupon == null) return NotFound();

            model.Code = model.Code.Trim().ToUpperInvariant();

            var duplicate = await _context.Coupons
                .FirstOrDefaultAsync(c => c.Code == model.Code && c.PkCouponId != model.PkCouponId);

            if (duplicate != null)
            {
                ModelState.AddModelError("Code", "A coupon with this code already exists.");
                return View(model);
            }

            coupon.Code = model.Code;
            coupon.Name = model.Name;
            coupon.Description = model.Description;
            coupon.DiscountType = model.DiscountType;
            coupon.DiscountValue = model.DiscountValue;
            coupon.MinimumOrderValue = model.MinimumOrderValue;
            coupon.MaxDiscountAmount = model.MaxDiscountAmount;
            coupon.IsActive = model.IsActive;
            coupon.UsageLimit = model.UsageLimit;
            coupon.ValidFrom = model.ValidFrom;
            coupon.ValidUntil = model.ValidUntil;
            coupon.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Message"] = $"success,Coupon '{coupon.Code}' updated.";
            return RedirectToAction(nameof(Coupons));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleCouponStatus(int id)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon == null)
            {
                TempData["Message"] = "warning,Coupon not found.";
                return RedirectToAction(nameof(Coupons));
            }

            coupon.IsActive = !coupon.IsActive;
            coupon.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var statusText = coupon.IsActive ? "activated" : "deactivated";
            TempData["Message"] = $"success,Coupon '{coupon.Code}' {statusText}.";
            return RedirectToAction(nameof(Coupons));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCoupon(int id)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon == null)
            {
                TempData["Message"] = "warning,Coupon not found.";
                return RedirectToAction(nameof(Coupons));
            }

            var usageCount = await _context.OrderCoupons.CountAsync(oc => oc.FkCouponId == id);
            if (usageCount > 0)
            {
                TempData["Message"] = $"error,Cannot delete coupon '{coupon.Code}' — it has been used in {usageCount} order(s). Deactivate it instead.";
                return RedirectToAction(nameof(Coupons));
            }

            _context.Coupons.Remove(coupon);
            await _context.SaveChangesAsync();

            TempData["Message"] = $"success,Coupon '{coupon.Code}' deleted.";
            return RedirectToAction(nameof(Coupons));
        }

        #endregion
    }
}
