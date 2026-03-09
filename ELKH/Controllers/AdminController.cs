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

namespace ELKH.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IRole_repo _roleRepo;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly Data.ApplicationDbContext _context;

        public AdminController(IRole_repo roleRepo, UserManager<IdentityUser> userManager, ApplicationDbContext context)
        public AdminController(
            IRole_repo roleRepo,
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<AdminController> logger,
            IFuzzyReindexService reindexService,
            UserManager<IdentityUser> userManager)
        {
            _roleRepo = roleRepo;
        }

        /// <summary>Renders the admin dashboard with live order counts and stock-level statistics.</summary>
        public async Task<IActionResult> Index()
        {
            // Define rolling time windows used by the order-volume KPI cards.
            var now      = DateTime.UtcNow;
            var weekAgo  = now.AddDays(-7);
            var monthAgo = now.AddDays(-30);

            var vm = new SalesVM
            {
                // Count orders placed within each rolling window.
                WeeklyTotalOrders  = await _context.Orders.CountAsync(o => o.CreatedAt >= weekAgo),
                MonthlyTotalOrders = await _context.Orders.CountAsync(o => o.CreatedAt >= monthAgo),

                // Split inventory into well-stocked (> 100 units) vs low-stock (≤ 100 units) buckets.
                StockUpCount       = await _context.Products.CountAsync(p => p.StockQuantity > 100),
                StockDownCount     = await _context.Products.CountAsync(p => p.StockQuantity <= 100),
            };
            return View(vm);
        }

        /*============================== List Of All Users ==============================*/
        public async Task<IActionResult> ListUsers(string search, string roleFilter, int page = 1)
        {
            int pageSize = 5;
            var users = _userManager.Users.ToList();
            var userList = new List<UserListVM>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userList.Add(new UserListVM
                {
                    Id = user.Id,
                    Email = user.Email,
                    Roles = roles.ToList()
                });
            }

            if (!string.IsNullOrEmpty(search))
            {
                userList = userList
                    .Where(u => u.Email.Contains(search, StringComparison.OrdinalIgnoreCase)
                             || u.Roles.Any(r => r.Contains(search, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            if (!string.IsNullOrEmpty(roleFilter) && roleFilter != "All")
            {
                userList = userList.Where(u => u.Roles.Contains(roleFilter)).ToList();
            }

            int totalUsers = userList.Count;
            var pagedUsers = userList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages  = (int)Math.Ceiling((double)totalUsers / pageSize);
            ViewBag.Search      = search;
            ViewBag.RoleFilter  = roleFilter;

            return View(pagedUsers);
        }

        /*============================== Account Details ==============================*/
        [HttpGet]
        public async Task<IActionResult> AccountDetails(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles   = await _userManager.GetRolesAsync(user);
            var contact = await _context.ContactDetails.FirstOrDefaultAsync(c => c.UserId == user.Id);

            var vm = new AccountDetailsVM
            {
                User = new UserListVM
                {
                    Id    = user.Id,
                    Name  = user.UserName ?? "",
                    Email = user.Email ?? "",
                    Roles = roles.ToList()
                },
                Contact = contact == null ? null : new ContactDetailVM
                {
                    ContactId   = contact.PkContactId,
                    FirstName   = contact.FirstName,
                    LastName    = contact.LastName,
                    PhoneNumber = contact.PhoneNumber,
                    Street      = contact.Street,
                    City        = contact.City,
                    Province    = contact.Province,
                    PostCode    = contact.PostCode,
                    Country     = contact.Country,
                    IsDefault   = contact.IsDefault
                }
            };

            return View(vm);
        }

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

        /*============================== Manage Sales ==============================*/
        public async Task<IActionResult> ManageSales()
        {
            var now = DateTime.Now;
            var weekStart = now.AddDays(-6).Date;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var yearStart = now.AddMonths(-11).Date;

            // ── Fetch into memory first so decimal Sum() works with SQLite ──
            var allTransactions = await _context.Transactions
                .Where(t => t.TransactionDate >= yearStart)
                .Select(t => new { t.TransactionDate, t.Amount })
                .ToListAsync();

            var weeklyTx = allTransactions.Where(t => t.TransactionDate.Date >= weekStart).ToList();
            var monthlyTx = allTransactions.Where(t => t.TransactionDate >= monthStart).ToList();

            // ── Summary cards ─────────────────────────────────────────────
            decimal weeklyGross = weeklyTx.Any() ? weeklyTx.Sum(t => t.Amount) : 0m;
            decimal monthlyGross = monthlyTx.Any() ? monthlyTx.Sum(t => t.Amount) : 0m;
            int weeklyOrders = weeklyTx.Count;
            int monthlyOrders = monthlyTx.Count;
            int totalOrders = await _context.Orders.CountAsync();

            // ── Weekly chart: last 7 days ─────────────────────────────────
            var weeklyLabels = new List<string>();
            var weeklySalesData = new List<decimal>();

            for (int d = 6; d >= 0; d--)
            {
                var day = now.AddDays(-d).Date;
                var dayTx = allTransactions.Where(t => t.TransactionDate.Date == day).ToList();
                weeklyLabels.Add(day.ToString("ddd dd"));
                weeklySalesData.Add(dayTx.Any() ? dayTx.Sum(t => t.Amount) : 0m);
            }

            // ── Monthly chart: last 12 months ─────────────────────────────
            var monthlyLabels = new List<string>();
            var monthlySalesData = new List<decimal>();

            for (int m = 11; m >= 0; m--)
            {
                var month = now.AddMonths(-m);
                var monthTx = allTransactions
                    .Where(t => t.TransactionDate.Year == month.Year
                             && t.TransactionDate.Month == month.Month)
                    .ToList();
                monthlyLabels.Add(month.ToString("MMM yyyy"));
                monthlySalesData.Add(monthTx.Any() ? monthTx.Sum(t => t.Amount) : 0m);
            }

            // ── Top 5 products ────────────────────────────────────────────
            var orderItems = await _context.OrderItems
                .Include(oi => oi.Product) // use correct navigation property
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
        /// <summary>Renders the sales management page (shell view).</summary>
        public IActionResult ManageSales()
        {
            return View();
        }

        // =====================================================================
        // Search Index Management
        // =====================================================================

        /// <summary>
        /// Rebuild the full-text search index from the Products table.
        /// Triggers both ProductFTS rebuild and background service reindex.
        /// </summary>
        /// <param name="payload">JSON payload with optional 'reason' field for audit trail</param>
        /// <returns>JSON result with success status</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReindexFTS([FromBody] ReindexPayload? payload)
        {
            string reason = payload?.Reason ?? string.Empty;

            // Step 1: Rebuild ProductFTS from Products table
            var sql = @"INSERT INTO ProductFTS(rowid, Name, PkProductId)
SELECT PkProductId, Name, PkProductId FROM Products
WHERE PkProductId NOT IN (SELECT rowid FROM ProductFTS);
";
            await _context.Database.ExecuteSqlRawAsync(sql);

            // Step 2: Record audit entry for compliance
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
            catch { /* ignore audit failures - don't block the operation */ }

            // Step 3: Trigger background service immediate reindex
            try
            {
                if (_reindexService != null)
                {
                    await _reindexService.ReindexOnce();
                }
            }
            catch { /* background service failures are logged internally */ }

            return Ok(new { success = true });
        }

        /// <summary>
        /// Get health status of the background reindexing service.
        /// Returns last run time, duration, suggestion count, and run count.
        /// </summary>
        /// <returns>JSON with service health metrics</returns>
        [HttpGet]
        public IActionResult ReindexHealth()
        {
            var svc = _reindexService;
            if (svc == null)
                return Json(new { success = false, message = "service unavailable" });

            var suggestionCount = _context.Set<FuzzySuggestionModel>().Count();

            return Json(new
            {
                success = true,
                lastRun = svc.LastRun,
                lastDuration = svc.LastDuration,
                suggestionCount,
                runCount = svc.RunCount
            });
        }

        // =====================================================================
        // Cache Management
        // =====================================================================

        /// <summary>
        /// Get cache statistics including fuzzy key count and last clear timestamp.
        /// Also includes background service metrics if available.
        /// </summary>
        /// <returns>JSON with cache statistics</returns>
        [HttpGet]
        public IActionResult CacheStats()
        {
            try
            {
                var count = _context.Set<CachedFuzzyKeyModel>().Count();
                var lastClear = _context.Set<AuditEntryModel>()
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
                catch { }

                return Json(new
                {
                    success = true,
                    keys = count,
                    lastClear = lastClear == default ? (DateTime?)null : lastClear,
                    lastRun,
                    lastDuration
                });
            }
            catch
            {
                return Json(new { success = false });
            }
        }

        /// <summary>
        /// Clear the fuzzy search cache with audit trail.
        /// Requires a reason for auditability and compliance.
        /// </summary>
        /// <param name="payload">JSON payload containing the required <c>Reason</c> field.</param>
        /// <returns>JSON result with the number of cache entries cleared.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClearFuzzyCache([FromBody] ClearCachePayload payload)
        {
            // Step 1: Validate reason is provided (required for audit trail)
            if (string.IsNullOrWhiteSpace(payload?.Reason))
            {
                return BadRequest(new { success = false, message = "Reason is required" });
            }

            var reason = payload.Reason;

            // Step 2: Load persisted cache keys and clear them
            var keys = _context.Set<CachedFuzzyKeyModel>().ToList();
            var registryCount = 0;

            if (keys.Any())
            {
                // Remove from memory cache
                foreach (var k in keys)
                {
                    try { _cache.Remove(k.CacheKey); } catch { }
                }
                registryCount = keys.Count;

                // Remove persisted registry
                try
                {
                    _context.Set<CachedFuzzyKeyModel>().RemoveRange(keys);
                    _context.SaveChanges();
                }
                catch { }

                // Step 3: Log for monitoring
                _logger.LogInformation(
                    "Admin {Admin} cleared {Count} fuzzy cache entries",
                    User.Identity?.Name ?? "unknown",
                    registryCount);

                // Step 4: Persist audit entry for compliance
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
                    _context.SaveChanges();
                }
                catch { /* swallow to not fail admin action */ }
            }

            return Ok(new { success = true, cleared = registryCount });
        }
    }

    /// <summary>
    /// Typed payload for the ReindexFTS action.
    /// </summary>
    public sealed class ReindexPayload
    {
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Typed payload for the ClearFuzzyCache action.
    /// Using a concrete type rather than <c>dynamic</c> enables model-binding validation
    /// and makes the expected JSON shape explicit in the API contract.
    /// </summary>
    public sealed class ClearCachePayload
    {
        public string? Reason { get; set; }
    }
}
