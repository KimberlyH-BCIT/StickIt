using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ELKH.Data;
using ELKH.Models;
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
    /// 3. Search Index Management
    ///    - ReindexFTS()                          // POST: Rebuild full-text search index
    ///    - ReindexHealth()                       // GET: Check reindex service status
    /// 4. Cache Management
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
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AdminController> _logger;
        private readonly ELKH.Services.IFuzzyReindexService _reindexService;

        public AdminController(
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<AdminController> logger,
            ELKH.Services.IFuzzyReindexService reindexService)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _reindexService = reindexService;
        }

        /// <summary>
        /// Admin dashboard with cache management and reindex tools.
        /// View: Views/Admin/Index.cshtml
        /// </summary>
        public ActionResult Index()
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
        /// <param name="payload">JSON payload with required 'reason' field</param>
        /// <returns>JSON result with number of cache entries cleared</returns>
        [HttpPost]
        public IActionResult ClearFuzzyCache([FromBody] dynamic payload)
        {
            // Step 1: Validate reason is provided (required for audit trail)
            string reason = string.Empty;
            try { reason = (string)payload?.reason ?? string.Empty; } catch { }

            if (string.IsNullOrWhiteSpace(reason))
            {
                return BadRequest(new { success = false, message = "Reason is required" });
            }

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
}
