using ELKH.Data;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.Services;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace ELKH.Controllers
{
    /// <summary>
    /// Administrative controller for system management, user administration,
    /// sales analytics, and search index maintenance.
    /// All actions require the Admin role for access.
    /// </summary>
    /// - Sales analytics fetches data in bulk and processes in-memory
    /// - Cache operations include error handling to prevent service disruption
    /// </remarks>
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        #region Fields & Constructor

        private readonly IRoleRepo _roleRepo;
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AdminController> _logger;
        private readonly IFuzzyReindexService _reindexService;
        private readonly UserManager<IdentityUser> _userManager;

        public AdminController(
            IRoleRepo roleRepo,
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<AdminController> logger,
            IFuzzyReindexService reindexService,
            UserManager<IdentityUser> userManager)
        {
            _roleRepo = roleRepo;
            _context = context;
            _cache = cache;
            _logger = logger;
            _reindexService = reindexService;
            _userManager = userManager;
        }

        #endregion

        #region Dashboard & Analytics

        /// <summary>
        /// Renders the admin dashboard with key performance indicators:
        /// - Weekly and monthly order counts
        /// - Stock level summaries (high/low)
        /// - Top 5 products by units sold
        /// </summary>
        /// <returns>Admin dashboard view with <see cref="SalesVM"/> model</returns>
        public async Task<IActionResult> Index()
        {
            var now = DateTime.UtcNow;
            var weekAgo  = now.AddDays(-7);
            var monthAgo = now.AddDays(-30);

            var vm = new SalesVM
            {
                WeeklyTotalOrders  = await _context.Orders.CountAsync(o => o.CreatedAt >= weekAgo),
                MonthlyTotalOrders = await _context.Orders.CountAsync(o => o.CreatedAt >= monthAgo),
                StockUpCount   = await _context.Products.CountAsync(p => p.StockQuantity > 100),
                StockDownCount = await _context.Products.CountAsync(p => p.StockQuantity <= 100),
            };

            // Top 5 products for dashboard widget - Group by product and aggregate sales
            var orderItems = await _context.OrderItems
                .Include(oi => oi.Product)
                .Select(oi => new
                {
                    oi.FkProductId,
                    ProductName  = oi.Product == null ? "Unknown" : oi.Product.Name,
                    ProductPrice = oi.Product == null ? 0m : oi.Product.Price,
                    oi.Quantity
                })
                .ToListAsync();

            ViewBag.TopProducts = orderItems
                .GroupBy(oi => new { oi.FkProductId, oi.ProductName, oi.ProductPrice })
                .Select(g => new TopProductVM
                {
                    ProductName = g.Key.ProductName,
                    UnitsSold   = g.Sum(oi => oi.Quantity),
                    Revenue     = g.Sum(oi => oi.Quantity * g.Key.ProductPrice)
                })
                .OrderByDescending(p => p.UnitsSold)
                .Take(5)
                .ToList();

            return View(vm);
        }

        /// <summary>
        /// Renders comprehensive sales analytics page with:
        /// - Weekly/monthly gross sales and order counts
        /// - 7-day sales trend chart
        /// - 12-month sales trend chart
        /// - Top 5 products by revenue
        /// </summary>
        /// <returns>Sales management view with detailed <see cref="SalesVM"/> model</returns>
        public async Task<IActionResult> ManageSales()
        {
            var now = DateTime.UtcNow;
            var weekStart = now.AddDays(-6).Date;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var yearStart = now.AddMonths(-11).Date;

            // Fetch all transactions for the analysis period
            // Note: Materializing to memory first enables decimal Sum() with SQLite
            var allTransactions = await _context.Transactions
                .Where(t => t.TransactionDate >= yearStart)
                .Select(t => new { t.TransactionDate, t.Amount })
                .ToListAsync();

            var weeklyTx = allTransactions.Where(t => t.TransactionDate.Date >= weekStart).ToList();
            var monthlyTx = allTransactions.Where(t => t.TransactionDate >= monthStart).ToList();

            // â”€â”€ Summary card metrics â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            decimal weeklyGross = weeklyTx.Count > 0 ? weeklyTx.Sum(t => t.Amount) : 0m;
            decimal monthlyGross = monthlyTx.Count > 0 ? monthlyTx.Sum(t => t.Amount) : 0m;
            int weeklyOrders = weeklyTx.Count;
            int monthlyOrders = monthlyTx.Count;
            int totalOrders = await _context.Orders.CountAsync();

            // â”€â”€ Weekly chart data: last 7 days â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var weeklyLabels = new List<string>();
            var weeklySalesData = new List<decimal>();

            for (int d = 6; d >= 0; d--)
            {
                var day = now.AddDays(-d).Date;
                var dayTx = allTransactions.Where(t => t.TransactionDate.Date == day).ToList();
                weeklyLabels.Add(day.ToString("ddd dd", CultureInfo.InvariantCulture));
                weeklySalesData.Add(dayTx.Count > 0 ? dayTx.Sum(t => t.Amount) : 0m);
            }

            // â”€â”€ Monthly chart data: last 12 months â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var monthlyLabels = new List<string>();
            var monthlySalesData = new List<decimal>();

            for (int m = 11; m >= 0; m--)
            {
                var month = now.AddMonths(-m);
                var monthTx = allTransactions
                    .Where(t => t.TransactionDate.Year == month.Year
                             && t.TransactionDate.Month == month.Month)
                    .ToList();
                monthlyLabels.Add(month.ToString("MMM yyyy", CultureInfo.InvariantCulture));
                monthlySalesData.Add(monthTx.Count > 0 ? monthTx.Sum(t => t.Amount) : 0m);
            }

            // â”€â”€ Top 5 products by revenue â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var orderItems = await _context.OrderItems
                .Include(oi => oi.Product)
                .Select(oi => new
                {
                    oi.FkProductId,
                    ProductName = oi.Product == null ? "Unknown" : oi.Product.Name,
                    ProductPrice = oi.Product == null ? 0m : oi.Product.Price,
                    oi.Quantity
                })
                .ToListAsync();

            var topProducts = orderItems
                .GroupBy(oi => new { oi.FkProductId, oi.ProductName, oi.ProductPrice })
                .Select(g => new TopProductVM
                {
                    ProductName = g.Key.ProductName,
                    UnitsSold = g.Sum(oi => oi.Quantity),
                    Revenue = g.Sum(oi => oi.Quantity * g.Key.ProductPrice)
                })
                .OrderByDescending(p => p.UnitsSold)
                .Take(5)
                .ToList();

            var vm = new SalesVM
            {
                WeeklyGrossSales = weeklyGross,
                MonthlyGrossSales = monthlyGross,
                WeeklyTotalOrders = weeklyOrders,
                MonthlyTotalOrders = monthlyOrders,
                TotalOrdersAllTime = totalOrders,
                WeeklyLabels = weeklyLabels,
                WeeklySalesData = weeklySalesData,
                MonthlyLabels = monthlyLabels,
                MonthlySalesData = monthlySalesData,
                TopProducts = topProducts
            };

            return View(vm);
        }

        #endregion

        #region User Management

        /// <summary>
        /// Displays a paginated, filterable list of all Identity users.
        /// Supports filtering by role and email search.
        /// </summary>
        /// <param name="search">Optional email filter (substring match, case-insensitive)</param>
        /// <param name="roleFilter">Optional role filter ('Admin', 'Manager', 'Staff', 'Customer', or 'All')</param>
        /// <param name="page">Page number (1-based)</param>
        /// <returns>User list view with <see cref="List{UserListVM}"/> model</returns>
        /// <remarks>
        /// Performance optimization:
        /// - Role filtering is done server-side via UserManager.GetUsersInRoleAsync()
        /// - Email search pushes predicate to database when no role filter is active
        /// - Role lookups (GetRolesAsync) are performed only on paginated slice
        /// </remarks>
        public async Task<IActionResult> ListUsers(string search, string roleFilter, int page = 1)
        {
            const int pageSize = 5;

            // Build candidate set using server-side filtering
            IList<IdentityUser> candidates;
            bool hasRoleFilter = !string.IsNullOrEmpty(roleFilter) && roleFilter != "All";

            if (hasRoleFilter)
            {
                // Single query: returns only users in the specified role
                candidates = await _userManager.GetUsersInRoleAsync(roleFilter);

                // Apply email search in-memory on the (already filtered) role-member list
                if (!string.IsNullOrEmpty(search))
                {
                    candidates = candidates
                        .Where(u => u.Email?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                        .ToList();
                }
            }
            else
            {
                // Push email filter to database to avoid loading all users into memory
                IQueryable<IdentityUser> query = _userManager.Users;
                if (!string.IsNullOrEmpty(search))
                    query = query.Where(u => u.Email != null && u.Email.Contains(search));

                candidates = await query.ToListAsync();
            }

            int totalUsers = candidates.Count;

            // Materialize only the current page before per-user role lookups
            var pageUsers = candidates
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Fetch roles only for the paged users (â‰¤ pageSize lookups)
            var userList = new List<UserListVM>(pageUsers.Count);
            foreach (var user in pageUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userList.Add(new UserListVM
                {
                    Id    = user.Id,
                    Email = user.Email ?? string.Empty,
                    Roles = roles.ToList()
                });
            }

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages  = (int)Math.Ceiling((double)totalUsers / pageSize);
            ViewBag.Search      = search;
            ViewBag.RoleFilter  = roleFilter;

            return View(userList);
        }

        /// <summary>
        /// Displays detailed information for a specific user account including:
        /// - Identity information (email, roles)
        /// - Contact details (if registered)
        /// </summary>
        /// <param name="id">Identity user ID (GUID string)</param>
        /// <returns>Account details view with <see cref="AccountDetailsVM"/> model</returns>
        [HttpGet]
        public async Task<IActionResult> AccountDetails(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            // Resolve the application-side RegisteredUser by email to access contact details
            // Note: Uses email as the join key rather than Identity GUID for consistency
            var registeredUser = await _context.RegisteredUsers
                .FirstOrDefaultAsync(r => r.Email == user.Email);
            var contact = registeredUser is null
                ? null
                : await _context.ContactDetails
                    .FirstOrDefaultAsync(c => c.FkRegisteredUserId == registeredUser.PkRegisteredUserId);

            var vm = new AccountDetailsVM
            {
                User = new UserListVM
                {
                    Id = user.Id,
                    Name = user.UserName ?? "",
                    Email = user.Email ?? "",
                    Roles = roles.ToList()
                },
                Contact = contact == null ? null : new ContactDetailVM
                {
                    ContactId = contact.PkContactId,
                    FirstName = contact.FirstName,
                    LastName = contact.LastName,
                    PhoneNumber = contact.PhoneNumber,
                    Street = contact.Street,
                    City = contact.City,
                    Province = contact.Province,
                    PostCode = contact.PostCode,
                    Country = contact.Country,
                    IsDefault = contact.IsDefault
                }
            };

            return View(vm);
        }

        /// <summary>
        /// Removes a role assignment from a user.
        /// Redirects back to AccountDetails after completion.
        /// </summary>
        /// <param name="userId">Identity user ID</param>
        /// <param name="role">Role name to remove</param>
        /// <returns>Redirect to AccountDetails</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveRole(string userId, string role)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            await _userManager.RemoveFromRoleAsync(user, role);
            return RedirectToAction("AccountDetails", new { id = userId });
        }

        #endregion

        #region Search Index Management

        /// <summary>
        /// Rebuild the full-text search index from the Products table.
        /// Performs three operations:
        /// 1. Rebuilds ProductFTS table from Products
        /// 2. Records audit entry for compliance
        /// 3. Triggers background service immediate reindex
        /// </summary>
        /// <param name="payload">JSON payload with optional 'reason' field for audit trail</param>
        /// <returns>JSON result with success status</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(ELKH.Extensions.RateLimitPolicies.Admin)]
        public async Task<IActionResult> ReindexFTS([FromBody] ReindexPayload? payload)
        {
            string reason = payload?.Reason ?? string.Empty;

            // Step 1: Rebuild ProductFTS from Products table (SQLite FTS5)
            var sql = @"INSERT INTO ProductFTS(rowid, Name, PkProductId)
SELECT PkProductId, Name, PkProductId FROM Products
WHERE PkProductId NOT IN (SELECT rowid FROM ProductFTS);
";
            await _context.Database.ExecuteSqlRawAsync(sql);

            // Step 2: Record audit entry for compliance tracking
            try
            {
                var audit = new AuditEntryModel
                {
                    Action = "ReindexFTS",
                    Actor = User.Identity?.Name ?? "unknown",
                    Timestamp = DateTime.UtcNow,
                    AffectedKeysCount = 0,
                    Details = "Reindexed ProductFTS table",
                    Reason = reason
                };
                _context.Add(audit);
                await _context.SaveChangesAsync();
            }
            catch { /* Ignore audit failures - don't block the operation */ }

            // Step 3: Trigger background service immediate reindex
            try
            {
                if (_reindexService != null)
                {
                    await _reindexService.ReindexOnce();
                }
            }
            catch { /* Background service failures are logged internally */ }

            return Ok(new { success = true });
        }

        /// <summary>
        /// Get health status of the background reindexing service.
        /// Returns diagnostic information:
        /// - Last run timestamp
        /// - Last run duration
        /// - Current suggestion count
        /// - Total run count since service start
        /// </summary>
        /// <returns>JSON with service health metrics</returns>
        [HttpGet]
        public IActionResult ReindexHealth()
        {
            var svc = _reindexService;
            if (svc == null)
                return Json(new { success = false, message = "service unavailable" });

            var suggestionCount = _context.FuzzySuggestions.Count();

            return Json(new
            {
                success = true,
                lastRun = svc.LastRun,
                lastDuration = svc.LastDuration,
                suggestionCount,
                runCount = svc.RunCount
            });
        }

        #endregion

        #region Cache Management

        /// <summary>
        /// Get cache statistics including:
        /// - Number of cached fuzzy search keys
        /// - Last cache clear timestamp
        /// - Background service metrics (if available)
        /// </summary>
        /// <returns>JSON with cache statistics and health data</returns>
        [HttpGet]
        public IActionResult CacheStats()
        {
            try
            {
                var count = _context.CachedFuzzyKeys.Count();
                var lastClear = _context.AuditEntries
                    .Where(a => a.Action == "ClearFuzzyCache")
                    .OrderByDescending(a => a.Timestamp)
                    .Select(a => a.Timestamp)
                    .FirstOrDefault();

                // Include background service metrics if available
                DateTime? lastRun = null;
                TimeSpan? lastDuration = null;
                try
                {
                    if (_reindexService != null)
                    {
                        lastRun = _reindexService.LastRun;
                        lastDuration = _reindexService.LastDuration;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not read reindex service metrics");
                }

                return Json(new
                {
                    success = true,
                    keys = count,
                    lastClear = lastClear == default ? (DateTime?)null : lastClear,
                    lastRun,
                    lastDuration
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve cache statistics");
                return Json(new { success = false });
            }
        }

        /// <summary>
        /// Clear the fuzzy search cache with full audit trail.
        /// Performs the following operations:
        /// 1. Validates that a reason is provided
        /// 2. Removes all cache entries from IMemoryCache
        /// 3. Removes registry entries from CachedFuzzyKeys table
        /// 4. Logs the operation for monitoring
        /// 5. Creates audit entry for compliance
        /// </summary>
        /// <param name="payload">JSON payload containing the required <c>Reason</c> field.</param>
        /// <returns>JSON result with the number of cache entries cleared</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(ELKH.Extensions.RateLimitPolicies.Admin)]
        public async Task<IActionResult> ClearFuzzyCache([FromBody] ClearCachePayload payload)
        {
            // Step 1: Validate reason is provided (required for audit trail)
            if (string.IsNullOrWhiteSpace(payload?.Reason))
            {
                return BadRequest(new { success = false, message = "Reason is required" });
            }

            var reason = payload.Reason;

            // Step 2: Load persisted cache keys and clear them from memory
            var keys = _context.CachedFuzzyKeys.ToList();
            var registryCount = 0;

            if (keys.Count > 0)
            {
                // Remove each key from IMemoryCache
                foreach (var k in keys)
                {
                    try { _cache.Remove(k.CacheKey); } catch { /* Ignore individual removal failures */ }
                }
                registryCount = keys.Count;

                // Step 3: Remove persisted registry entries
                try
                {
                    _context.CachedFuzzyKeys.RemoveRange(keys);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to persist CachedFuzzyKey removal for {Count} keys", keys.Count);
                }

                // Step 4: Log for monitoring and diagnostics
                _logger.LogInformation(
                    "Admin {Admin} cleared {Count} fuzzy cache entries",
                    User.Identity?.Name ?? "unknown",
                    registryCount);

                // Step 5: Persist audit entry for compliance
                try
                {
                    var audit = new AuditEntryModel
                    {
                        Action = "ClearFuzzyCache",
                        Actor = User.Identity?.Name ?? "unknown",
                        Timestamp = DateTime.UtcNow,
                        AffectedKeysCount = registryCount,
                        Details = string.Join(',', keys.Select(k => k.CacheKey)),
                        Reason = reason
                    };
                    _context.Add(audit);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to persist ClearFuzzyCache audit entry");
                }
            }

            return Ok(new { success = true, cleared = registryCount });
        }

        #endregion
    }

    #region Payload Models

    /// <summary>
    /// Request payload for the <see cref="AdminController.ReindexFTS"/> action.
    /// Enables optional reason tracking for audit compliance.
    /// </summary>
    public sealed class ReindexPayload
    {
        /// <summary>Optional reason for triggering the reindex operation.</summary>
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Request payload for the <see cref="AdminController.ClearFuzzyCache"/> action.
    /// Using a concrete type (rather than dynamic) enables:
    /// - Model binding validation
    /// - Explicit API contract
    /// - IntelliSense support in consuming code
    /// </summary>
    public sealed class ClearCachePayload
    {
        /// <summary>Required reason for clearing the cache (audit compliance).</summary>
        public string? Reason { get; set; }
    }

    #endregion
}

