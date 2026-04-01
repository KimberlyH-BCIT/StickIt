using System.Globalization;
using System.Text;
using ELKH.Data;
using ELKH.Models;
using ELKH.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Controllers
{
    /// <summary>
    /// Prometheus-style metrics endpoint for system monitoring.
    /// Exposes operational metrics for fuzzy search reindexing service health and performance.
    /// </summary>
    /// <remarks>
    /// TABLE OF CONTENTS
    /// ================================================================================
    /// 1. Constructor & Dependencies
    /// 2. Metrics Endpoint
    ///    - Get()                                 // GET: Prometheus text format metrics
    /// ================================================================================
    /// 
    /// Exposed Metrics:
    /// - fuzzy_reindex_last_run_seconds         // Unix timestamp of last reindex
    /// - fuzzy_reindex_last_duration_seconds    // Duration of last reindex
    /// - fuzzy_reindex_run_count                // Total reindex runs since startup
    /// - fuzzy_suggestion_count                 // Number of precomputed suggestions
    /// 
    /// Routes: /Admin/metrics
    /// Authorization: Admin role required (prevents exposure of internal metrics)
    /// Format: Prometheus text exposition format v0.0.4
    /// 
    /// Implementation Notes:
    /// - Metrics are composed in-memory and served synchronously
    /// - Database access is fault-tolerant (silently omits failing metrics)
    /// - Returns plain text, not JSON, per Prometheus specification
    /// - Background service instance injected for real-time status
    /// </remarks>
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    [ApiController]
    public class MetricsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFuzzyReindexService _reindex;

        public MetricsController(ApplicationDbContext db, IFuzzyReindexService reindex)
        {
            _db = db;
            _reindex = reindex;
        }

        /// <summary>
        /// GET /Admin/metrics
        /// Returns Prometheus-formatted metrics for monitoring system health.
        /// </summary>
        /// <returns>Plain text metrics in Prometheus exposition format</returns>
        /// <remarks>
        /// Metrics Format:
        /// ```
        /// # HELP metric_name Description
        /// # TYPE metric_name gauge|counter
        /// metric_name value
        /// ```
        /// 
        /// Security:
        /// - Requires Admin role to prevent exposure of internal metrics
        /// - Previously public endpoint, now secured
        /// 
        /// Fault Tolerance:
        /// - Database failures are silently caught and logged
        /// - Metrics endpoint always returns 200 OK with available data
        /// - Missing metrics are omitted rather than causing errors
        /// </remarks>
        [HttpGet]
        [Route("/Admin/metrics")]
        public async Task<IActionResult> Get()
        {
            var sb = new StringBuilder();

            // =====================================================================
            // Fuzzy Reindex Timing and Run Count Metrics
            // =====================================================================
            // Reports last run timestamp (Unix seconds) and duration
            // Uses 'gauge' for timing values and 'counter' for run count
            
            if (_reindex.LastRun.HasValue)
            {
                var lastSec = new DateTimeOffset(_reindex.LastRun.Value).ToUnixTimeSeconds();
                sb.AppendLine("# HELP fuzzy_reindex_last_run_seconds Unix timestamp of last fuzzy reindex completion");
                sb.AppendLine("# TYPE fuzzy_reindex_last_run_seconds gauge");
                sb.AppendLine(CultureInfo.InvariantCulture, $"fuzzy_reindex_last_run_seconds {lastSec}");
            }

            if (_reindex.LastDuration.HasValue)
            {
                sb.AppendLine("# HELP fuzzy_reindex_last_duration_seconds Duration in seconds of last fuzzy reindex");
                sb.AppendLine("# TYPE fuzzy_reindex_last_duration_seconds gauge");
                sb.AppendLine(CultureInfo.InvariantCulture, $"fuzzy_reindex_last_duration_seconds {_reindex.LastDuration.Value.TotalSeconds:F2}");
            }

            sb.AppendLine("# HELP fuzzy_reindex_run_count Number of times fuzzy reindex has run since startup");
            sb.AppendLine("# TYPE fuzzy_reindex_run_count counter");
            sb.AppendLine(CultureInfo.InvariantCulture, $"fuzzy_reindex_run_count {_reindex.RunCount}");

            // =====================================================================
            // Database-Backed Counts
            // =====================================================================
            // Access database in fault-tolerant manner
            // Metrics endpoints should not propagate DB exceptions
            try
            {
                var suggestionCount = await _db.FuzzySuggestions.CountAsync();
                sb.AppendLine("# HELP fuzzy_suggestion_count Number of precomputed fuzzy suggestion rows");
                sb.AppendLine("# TYPE fuzzy_suggestion_count gauge");
                sb.AppendLine(CultureInfo.InvariantCulture, $"fuzzy_suggestion_count {suggestionCount}");
            }
            catch
            {
                // Intentionally swallow exceptions: metrics should be best-effort
                // Prometheus scraper will detect missing metrics via staleness
            }

            // Return as text/plain with Prometheus exposition format version header
            return Content(sb.ToString(), "text/plain; version=0.0.4");
        }
    }
}
