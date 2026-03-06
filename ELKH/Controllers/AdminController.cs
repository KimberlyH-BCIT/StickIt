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
    /// <summary>
    /// Admin console controller providing administrative tools and utilities.
    /// Handles system maintenance, cache management, and search indexing.
    /// </summary>
    /// <remarks>
    /// TABLE OF CONTENTS
    /// ================================================================================
    /// 1. Fields & Constructor
    /// 2. Dashboard
    ///    - Index()                               // GET: Admin dashboard
    /// 3. User Management
    ///    - ListUsers()                           // GET: List all users
    ///    - ManageUserRole()                      // GET: Manage user roles
    ///    - CustomerAccountDetails()              // GET: Customer account details
    ///    - StaffAccountDetails()                 // GET: Staff account details
    ///    - ManageSales()                         // GET: Manage sales
    /// 4. Search Index Management
    ///    - ReindexFTS()                          // POST: Rebuild full-text search index
    ///    - ReindexHealth()                       // GET: Check reindex service status
    /// 5. Cache Management
    ///    - CacheStats()                          // GET: Cache statistics
    ///    - ClearFuzzyCache()                     // POST: Clear fuzzy search cache
    /// ================================================================================
    ///
    /// All endpoints require Admin role authorization.
    /// Routes: /Admin/{action}
    /// </remarks>
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IRole_repo _roleRepo;
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AdminController> _logger;
        private readonly IFuzzyReindexService _reindexService;
        private readonly UserManager<IdentityUser> _userManager;

        public AdminController(
            IRole_repo roleRepo,
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

        /// <summary>
        /// Renders the user listing page with every Identity user and their assigned roles.
        /// Roles are fetched per-user via UserManager because ASP.NET Core Identity does not
        /// expose a single query that returns both users and roles; the list is small enough
        /// that the extra round-trips are acceptable.
        /// </summary>
        public async Task<IActionResult> ListUsers()
        {
            var users  = _userManager.Users.ToList();
            var vmList = new List<UserListVM>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                vmList.Add(new UserListVM
                {
                    Id    = u.Id,
                    Email = u.Email ?? string.Empty,
                    Roles = roles.ToList()
                });
            }
            return View(vmList);
        }

        /// <summary>
        /// Renders the role management page, pre-populating the view model
        /// with all roles retrieved from <see cref="IRole_repo"/>.
        /// </summary>
        public IActionResult ManageUserRole()
        {
            ManageRoleVM manageRoleVM = new ManageRoleVM();

            manageRoleVM.Roles = _roleRepo.GetAllRoles();

            return View(manageRoleVM);
        }

        /// <summary>Renders the customer account details page (shell view).</summary>
        public IActionResult CustomerAccountDetails()
        {
            return View();
        }

        /// <summary>Renders the staff account details page (shell view).</summary>
        public IActionResult StaffAccountDetails()
        {
            return View();
        }

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
