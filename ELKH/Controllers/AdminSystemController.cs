using ELKH.Controllers.Base;
using ELKH.Data;
using ELKH.Models;
using ELKH.Constants;
using ELKH.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ELKH.Controllers;

// TABLE OF CONTENTS
// - Search index management
// - Cache management
// - Health and system operations

/// <summary>
/// Admin controller responsible for system management, cache operations, and search indexing.
/// Handles technical operations and system maintenance tasks.
/// </summary>
public class AdminSystemController : AdminControllerBase
{
    private readonly IMemoryCache _cache;
    private readonly IFuzzyReindexService _reindexService;

    public AdminSystemController(
        ApplicationDbContext context,
        IMemoryCache cache,
        IFuzzyReindexService reindexService,
        ILogger<AdminSystemController> logger)
        : base(context, logger)
    {
        _cache = cache;
        _reindexService = reindexService;
    }

    #region Search Index Management

    /// <summary>
    /// POST: AdminSystem/ReindexSearch - Rebuild the full-text search index
    /// </summary>
    /// <param name="payload">JSON payload with optional 'reason' field for audit trail</param>
    /// <returns>JSON result with success status</returns>
    /// <remarks>
    /// Performs three operations:
    /// 1. Rebuilds ProductFTS table from Products
    /// 2. Records audit entry for compliance
    /// 3. Triggers background service immediate reindex
    /// </remarks>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimitPolicies.Admin)]
    public async Task<IActionResult> ReindexSearch([FromBody] ReindexPayload? payload)
    {
        try
        {
            string reason = payload?.Reason ?? string.Empty;
            var sql = @"INSERT INTO ProductFTS(rowid, Name, PkProductId)
        SELECT PkProductId, Name, PkProductId FROM Products
        WHERE PkProductId NOT IN (SELECT rowid FROM ProductFTS);";

            await Context.Database.ExecuteSqlRawAsync(sql);

            await TryWriteAuditEntryAsync("ReindexFTS", "Reindexed ProductFTS table", 0, reason);
            await TryReindexSearchServiceAsync();

            await LogAdminActionAsync("ReindexSearch", $"Reason: {reason}");

            return Ok(new { success = true, message = "Search index rebuilt successfully" });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error rebuilding search index");
            return Ok(new { success = false, message = "Failed to rebuild search index" });
        }
    }

    /// <summary>
    /// GET: AdminSystem/SearchHealth - Get search index health status
    /// </summary>
    /// <returns>JSON result with index health metrics</returns>
    public async Task<IActionResult> SearchHealth()
    {
        try
        {
            var totalProducts = await Context.Products.CountAsync();
            var indexedProducts = await Context.Database
                .ExecuteSqlRawAsync("SELECT COUNT(*) FROM ProductFTS");

            var serviceSnapshot = GetReindexServiceSnapshot();

            return Json(new
            {
                success = true,
                totalProducts,
                indexedProducts,
                indexCoverage = totalProducts > 0 ? (double)indexedProducts / totalProducts : 0.0,
                backgroundService = new
                {
                    running = serviceSnapshot.Running,
                    lastRun = serviceSnapshot.LastRun,
                    lastDuration = serviceSnapshot.LastDuration?.TotalMilliseconds
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to retrieve search health status");
            return Json(new { success = false, message = "Failed to get search health status" });
        }
    }

    #endregion

    #region Cache Management

    /// <summary>
    /// GET: AdminSystem/CacheStats - Get cache statistics and health metrics
    /// </summary>
    /// <returns>JSON result with cache usage statistics</returns>
    public async Task<IActionResult> CacheStats()
    {
        try
        {
            var count = await Context.CachedFuzzyKeys.CountAsync();
            var lastClear = await Context.AuditEntries
                .Where(a => a.Action == "ClearFuzzyCache")
                .OrderByDescending(a => a.Timestamp)
                .Select(a => a.Timestamp)
                .FirstOrDefaultAsync();
            var serviceSnapshot = GetReindexServiceSnapshot();

            return Json(new
            {
                success = true,
                cacheKeys = count,
                lastClear = lastClear == default ? (DateTime?)null : lastClear,
                backgroundService = new
                {
                    lastRun = serviceSnapshot.LastRun,
                    lastDuration = serviceSnapshot.LastDuration?.TotalMilliseconds
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to retrieve cache statistics");
            return Json(new { success = false, message = "Failed to get cache statistics" });
        }
    }

    /// <summary>
    /// POST: AdminSystem/ClearCache - Clear the fuzzy search cache with audit trail
    /// </summary>
    /// <param name="payload">JSON payload containing the required 'Reason' field</param>
    /// <returns>JSON result with the number of cache entries cleared</returns>
    /// <remarks>
    /// Performs the following operations:
    /// 1. Validates that a reason is provided
    /// 2. Removes all cache entries from IMemoryCache
    /// 3. Removes registry entries from CachedFuzzyKeys table
    /// 4. Logs the operation for monitoring
    /// 5. Creates audit entry for compliance
    /// </remarks>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimitPolicies.Admin)]
    public async Task<IActionResult> ClearCache([FromBody] ClearCachePayload payload)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(payload?.Reason))
            {
                return BadRequest(new { success = false, message = "Reason is required for cache clearing operations" });
            }

            var reason = payload.Reason;
            var keys = await Context.CachedFuzzyKeys.ToListAsync();
            var registryCount = 0;

            if (keys.Any())
            {
                foreach (var k in keys)
                {
                    try
                    {
                        _cache.Remove(k.CacheKey);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Failed to remove cache key {CacheKey}", k.CacheKey);
                    }
                }
                registryCount = keys.Count;

                try
                {
                    Context.CachedFuzzyKeys.RemoveRange(keys);
                    await Context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to persist CachedFuzzyKey removal for {Count} keys", keys.Count);
                }

                Logger.LogInformation(
                    "Admin {Admin} cleared {Count} fuzzy cache entries. Reason: {Reason}",
                    User.Identity?.Name ?? "unknown",
                    registryCount,
                    reason);

                await TryWriteAuditEntryAsync("ClearFuzzyCache", $"Cleared {registryCount} cache entries", registryCount, reason);
            }

            await LogAdminActionAsync("ClearedCache", $"Cleared {registryCount} entries. Reason: {reason}");

            return Ok(new
            {
                success = true,
                cleared = registryCount,
                message = $"Successfully cleared {registryCount} cache entries"
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error clearing cache");
            return Ok(new { success = false, message = "Failed to clear cache" });
        }
    }

    #endregion

    #region System Health & Monitoring

    /// <summary>
    /// GET: AdminSystem/HealthCheck - Get comprehensive system health status
    /// </summary>
    /// <returns>JSON result with system health metrics</returns>
    public async Task<IActionResult> HealthCheck()
    {
        try
        {
            var healthData = new
            {
                database = await CheckDatabaseHealthAsync(),
                cache = await CheckCacheHealthAsync(),
                search = await CheckSearchHealthAsync(),
                timestamp = DateTime.UtcNow
            };

            return Json(new { success = true, health = healthData });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error performing system health check");
            return Json(new { success = false, message = "Health check failed" });
        }
    }

    /// <summary>
    /// GET: AdminSystem/BackgroundServices - Get background service status
    /// </summary>
    /// <returns>JSON result with service health and metrics</returns>
    public IActionResult BackgroundServices()
    {
        try
        {
            var services = new List<object>();

            // Fuzzy reindex service
            if (_reindexService != null)
            {
                services.Add(new
                {
                    name = "Fuzzy Reindex Service",
                    status = "Running",
                    lastRun = _reindexService.LastRun,
                    lastDuration = _reindexService.LastDuration?.TotalMilliseconds,
                    nextRun = "On demand"
                });
            }
            else
            {
                services.Add(new
                {
                    name = "Fuzzy Reindex Service",
                    status = "Not Available",
                    lastRun = (DateTime?)null,
                    lastDuration = (double?)null,
                    nextRun = "N/A"
                });
            }

            return Json(new { success = true, services });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error checking background service status");
            return Json(new { success = false, message = "Failed to check service status" });
        }
    }

    #endregion

    #region Helper Methods

    private async Task<object> CheckDatabaseHealthAsync()
    {
        try
        {
            var canConnect = await Context.Database.CanConnectAsync();
            var productCount = await Context.Products.CountAsync();

            return new
            {
                status = canConnect ? "Healthy" : "Unhealthy",
                canConnect,
                productCount,
                lastChecked = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Database health check failed");
            return new { status = "Error", message = ex.Message };
        }
    }

    private async Task<object> CheckCacheHealthAsync()
    {
        try
        {
            var keyCount = await Context.CachedFuzzyKeys.CountAsync();

            return new
            {
                status = "Healthy",
                registeredKeys = keyCount,
                lastChecked = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Cache health check failed");
            return new { status = "Error", message = ex.Message };
        }
    }

    private async Task<object> CheckSearchHealthAsync()
    {
        try
        {
            var totalProducts = await Context.Products.CountAsync();

            return new
            {
                status = "Healthy",
                totalProducts,
                lastChecked = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Search health check failed");
            return new { status = "Error", message = ex.Message };
        }
    }

    private async Task TryWriteAuditEntryAsync(string action, string details, int affectedKeysCount, string reason)
    {
        try
        {
            var audit = new AuditEntryModel
            {
                Action = action,
                Actor = User.Identity?.Name ?? "unknown",
                Timestamp = DateTime.UtcNow,
                AffectedKeysCount = affectedKeysCount,
                Details = details,
                Reason = reason
            };

            Context.Add(audit);
            await Context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to persist {Action} audit entry", action);
        }
    }

    private async Task TryReindexSearchServiceAsync()
    {
        try
        {
            if (_reindexService != null)
            {
                await _reindexService.ReindexOnce();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Background reindex service failed");
        }
    }

    private ReindexServiceSnapshot GetReindexServiceSnapshot()
    {
        try
        {
            return _reindexService is null
                ? ReindexServiceSnapshot.NotAvailable
                : new ReindexServiceSnapshot(true, _reindexService.LastRun, _reindexService.LastDuration);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not read reindex service metrics");
            return ReindexServiceSnapshot.NotAvailable;
        }
    }

    private sealed record ReindexServiceSnapshot(bool Running, DateTime? LastRun, TimeSpan? LastDuration)
    {
        public static ReindexServiceSnapshot NotAvailable { get; } = new(false, null, null);
    }

    #endregion
}
