using ELKH.Data;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Controllers
{
    /// <summary>
    /// Manager controller for operational oversight and product/order management.
    /// Accessible to users with Admin or Manager roles.
    /// </summary>
    /// <remarks>
    /// TABLE OF CONTENTS
    /// ================================================================================
    /// 1. Constructor & Dependencies                                   (lines 27-35)
    /// 2. Dashboard                                                    (lines 37-56)
    ///    - Index()                              // KPIs and statistics overview
    /// 3. Product Management                                           (lines 58-174)
    ///    - ListOfProducts()                     // Paginated product list with filters
    ///    - ToggleActive()                       // Enable/disable products
    ///    - ProductDetails()                     // Detailed product view with ratings
    /// 4. Transaction Management                                       (lines 176-207)
    ///    - ListAllTransactions()                // Paginated transaction list
    /// 5. Staff Management                                             (lines 209-238)
    ///    - ListOfStaffAccount()                 // Staff/Manager/Admin user list
    /// ================================================================================
    ///
    /// ROLE-BASED ACCESS:
    /// - Requires Admin OR Manager role for all endpoints
    /// - Staff accounts can be viewed but not modified
    /// - Product activation/deactivation allowed without full CRUD permissions
    ///
    /// OPERATIONAL SCOPE:
    /// - Dashboard: Real-time KPIs (products, stock, orders, staff counts)
    /// - Products: Filtering, search, stock monitoring, activation toggle
    /// - Transactions: Order payment tracking and status monitoring
    /// - Staff: User list with role-based filtering
    /// </remarks>
    [Authorize(Roles = "Admin,Manager")]
    public class ManagerController : Controller
    {
        #region Constructor & Dependencies

        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public ManagerController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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
            ViewBag.TotalProducts = await _context.Products.CountAsync();
            ViewBag.ActiveProducts = await _context.Products.CountAsync(p => p.IsActive);
            ViewBag.InactiveProducts = await _context.Products.CountAsync(p => !p.IsActive);

            // Stock health indicators (thresholds: 5 = critical, 20 = low)
            ViewBag.StockUpCount = await _context.Products.CountAsync(p => p.StockQuantity > 20);
            ViewBag.StockDownCount = await _context.Products.CountAsync(p => p.StockQuantity <= 20);
            ViewBag.LowStockCount = await _context.Products.CountAsync(p => p.StockQuantity <= 5);

            // Order activity metrics
            ViewBag.WeeklyOrders = await _context.Orders.CountAsync(o => o.CreatedAt >= weekAgo);
            ViewBag.MonthlyOrders = await _context.Orders.CountAsync(o => o.CreatedAt >= monthAgo);

            // Staff headcount (Manager + Staff roles combined)
            ViewBag.TotalStaff = (await _userManager.GetUsersInRoleAsync("Staff")).Count
                               + (await _userManager.GetUsersInRoleAsync("Manager")).Count;

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
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImage)
                .AsQueryable();

            // Apply search filter (product name or category name)
            if (!string.IsNullOrEmpty(search))
                query = query.Where(p => p.Name.Contains(search) ||
                                         (p.Category != null && p.Category.CategoryName.Contains(search)));

            // Apply stock level filter
            if (stockFilter == "low")
                query = query.Where(p => p.StockQuantity <= 5);
            else if (stockFilter == "medium")
                query = query.Where(p => p.StockQuantity > 5 && p.StockQuantity <= 20);
            else if (stockFilter == "stocked")
                query = query.Where(p => p.StockQuantity > 20);

            // Apply active status filter
            if (activeFilter == "active")
                query = query.Where(p => p.IsActive);
            else if (activeFilter == "inactive")
                query = query.Where(p => !p.IsActive);

            // Get total count before pagination
            int total = await query.CountAsync();

            // Execute paginated query ordered by stock quantity (critical items first)
            var rawProducts = await query
                .OrderBy(p => p.StockQuantity)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

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

            TempData["Success"] = $"'{p.Name}' is now {(p.IsActive ? "Active" : "Inactive")}.";

            return RedirectToAction("ListOfProducts", new { search, stockFilter, activeFilter, page });
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
            if (p.ProductRatings != null && p.ProductRatings.Any())
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
            var query = _context.Transactions.AsQueryable();

            // Apply status search filter
            if (!string.IsNullOrEmpty(search))
                query = query.Where(t => t.TransactionStatus.Contains(search));

            int total = await query.CountAsync();

            // Execute paginated query
            var transactions = await query
                .OrderByDescending(t => t.TransactionDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TransactionVM
                {
                    PkTransactionId = t.PkTransactionId,
                    TransactionStatus = t.TransactionStatus,
                    Amount = t.Amount,
                    TransactionDate = t.TransactionDate,
                    DeliveryFee = t.DeliveryFee,
                    FkOrderId = t.FkOrderId
                })
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.Search = search;

            return View(transactions);
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
            var staffRoles = new[] { "Manager", "Staff", "Admin" };
            var allUsers = _userManager.Users.ToList();
            var staffList = new List<UserListVM>();

            // N+1 pattern: GetRolesAsync called for each user
            // Acceptable for small staff counts (<100 users)
            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);

                // Include user if they have any staff-related role
                if (roles.Any(r => staffRoles.Contains(r)))
                    staffList.Add(new UserListVM
                    {
                        Id = user.Id,
                        Email = user.Email ?? "",
                        Name = user.UserName ?? "",
                        Roles = roles.ToList()
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

        #endregion
    }
}
