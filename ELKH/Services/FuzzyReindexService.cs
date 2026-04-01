using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ELKH.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System;
// using ELKH.Utils; (fallback helper removed)
using Microsoft.Extensions.DependencyInjection;

namespace ELKH.Services
{
    /*
     * ┌────────────────────────────────────────────────────────────────────────────┐
     * │ TABLE OF CONTENTS - FuzzyReindexService.cs                                 │
     * ├────────────────────────────────────────────────────────────────────────────┤
     * │ 1. Constructor & Configuration ............................ Lines  56-72   │
     * │    - Dependency injection                                                  │
     * │    - Configurable reindex interval                                         │
     * │    - Thread-safe metrics initialization                                    │
     * │                                                                            │
     * │ 2. Background Service Execution ....................... Lines  74-99    │
     * │    - ExecuteAsync: Startup + periodic execution loop                       │
     * │    - Cancellation token support                                            │
     * │    - Error handling and logging                                            │
     * │                                                                            │
     * │ 3. Reindexing Logic ................................... Lines 101-177   │
     * │    - ReindexOnce: FTS5 virtual table management                            │
     * │    - Precomputed suggestions with transactions                             │
     * │    - SQLite-specific error handling                                        │
     * │    - Thread-safe metrics tracking                                          │
     * └────────────────────────────────────────────────────────────────────────────┘
     */

    /// <summary>
    /// Background service responsible for periodically rebuilding search-related
    /// artifacts used by the fuzzy search functionality.
    ///
    /// <para><strong>Responsibilities:</strong></para>
    /// <list type="bullet">
    /// <item>Ensure the FTS5 full-text search table contains all products</item>
    /// <item>Precompute normalized fuzzy suggestion records for fast autocomplete</item>
    /// <item>Maintain search index integrity through periodic rebuilds</item>
    /// </list>
    ///
    /// <para><strong>Execution Pattern:</strong></para>
    /// The service executes once on startup and then periodically based on a
    /// configured interval (default 6 hours, override via Search:ReindexIntervalMinutes).
    /// Errors are logged but do not stop the host from running.
    ///
    /// <para><strong>Thread Safety:</strong></para>
    /// Metrics (_lastRun, _lastDuration, _runCount) are protected by a lock since they're
    /// written from the background thread and may be read from HTTP request threads.
    /// </summary>
    /// <remarks>
    /// This service uses scoped DbContext instances to avoid lifetime issues with the singleton
    /// background service. Each reindex operation creates a new scope.
    /// </remarks>
    public class FuzzyReindexService : BackgroundService, IFuzzyReindexService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<FuzzyReindexService> _logger;
        private TimeSpan _interval = TimeSpan.FromHours(6);

        // ┌──────────────────────────────────────────────────────────────────────┐
        // │ Thread-Safe Metrics                                                  │
        // │ Lock guards _lastRun, _lastDuration and _runCount which are written  │
        // │ from the background thread and read from HTTP request threads.       │
        // └──────────────────────────────────────────────────────────────────────┘
        private readonly object _metricsLock = new();
        private DateTime? _lastRun;
        private TimeSpan? _lastDuration;
        private int _runCount;

        #region Constructor & Configuration

        /// <summary>
        /// Initializes a new instance of <see cref="FuzzyReindexService"/>.
        /// </summary>
        /// <param name="services">
        /// Root service provider used to create scoped EF contexts per reindex pass.
        /// Required because background services are singletons but DbContext should be scoped.
        /// </param>
        /// <param name="logger">Logger for reindex progress and error diagnostics.</param>
        /// <param name="configuration">
        /// Application configuration; reads <c>Search:ReindexIntervalMinutes</c> to override
        /// the default 6-hour periodic interval. Useful for testing or high-frequency updates.
        /// </param>
        public FuzzyReindexService(IServiceProvider services, ILogger<FuzzyReindexService> logger, Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _services = services;
            _logger = logger;

            // ── Configuration: Allow override of reindex interval ──────────────────
            var minutes = configuration.GetValue<int?>("Search:ReindexIntervalMinutes");
            if (minutes.HasValue && minutes.Value > 0) _interval = TimeSpan.FromMinutes(minutes.Value);
        }

        #endregion

        #region Background Service Execution

        /// <summary>
        /// Background service execution method called by the hosting infrastructure.
        /// Runs once on application startup, then periodically based on the configured interval.
        /// </summary>
        /// <param name="stoppingToken">
        /// Cancellation token signaled when the application is shutting down.
        /// The service will stop gracefully when this token is canceled.
        /// </param>
        /// <remarks>
        /// <para><strong>Execution Flow:</strong></para>
        /// <list type="number">
        /// <item>Run initial reindex on startup (errors logged, not thrown)</item>
        /// <item>Enter periodic loop with Task.Delay between iterations</item>
        /// <item>Handle TaskCanceledException silently (expected on shutdown)</item>
        /// <item>Log other exceptions but continue running</item>
        /// </list>
        /// This pattern ensures the service is resilient and doesn't crash the application.
        /// </remarks>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // ── Startup Reindex ────────────────────────────────────────────────────
            // Run once immediately on application startup to ensure search index is current.
            try
            {
                await ReindexOnce(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Initial fuzzy reindex failed");
            }

            // ── Periodic Reindex Loop ──────────────────────────────────────────────
            // Continue running until application shutdown (stoppingToken canceled).
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_interval, stoppingToken);
                    await ReindexOnce(stoppingToken);
                }
                catch (TaskCanceledException) 
                { 
                    // Expected when application is shutting down - exit gracefully.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Periodic fuzzy reindex failed");
                    // Continue running despite errors - next iteration may succeed.
                }
            }
        }

        #endregion

        #region Reindexing Logic

        /// <summary>
        /// Perform a single reindex operation. This method is safe to call concurrently
        /// from the host loop but is intended to be called once at a time.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to stop the operation gracefully.</param>
        /// <remarks>
        /// <para><strong>Two-Phase Reindex Process:</strong></para>
        /// <list type="number">
        /// <item><strong>FTS5 Virtual Table Update</strong>
        ///   <list type="bullet">
        ///   <item>Create FTS5 virtual table if not exists (idempotent)</item>
        ///   <item>Insert any new products not yet in the FTS index</item>
        ///   <item>Uses rowid = PkProductId for consistent reference</item>
        ///   </list>
        /// </item>
        /// <item><strong>Precomputed Suggestions Rebuild</strong>
        ///   <list type="bullet">
        ///   <item>Query all products with normalized names and metadata</item>
        ///   <item>DELETE + INSERT wrapped in transaction for atomicity</item>
        ///   <item>Ensures search never sees empty suggestion table</item>
        ///   </list>
        /// </item>
        /// </list>
        ///
        /// <para><strong>Error Handling:</strong></para>
        /// SQLite-specific exceptions are logged separately for better diagnostics.
        /// All exceptions are logged but not rethrown - allows service to continue.
        /// </remarks>
        public async Task ReindexOnce(CancellationToken cancellationToken = default)
        {
            // ── Scoped DbContext Creation ──────────────────────────────────────────
            // Create a new scope to get a fresh DbContext (background services are singletons).
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            DateTime start = DateTime.UtcNow;

            // ╔══════════════════════════════════════════════════════════════════════╗
            // ║ PHASE 1: FTS5 Virtual Table Rebuild                                  ║
            // ╚══════════════════════════════════════════════════════════════════════╝
            try
            {
                // ── Create FTS5 Virtual Table (Idempotent) ─────────────────────────
                // Using FTS5 with the product Name and an unindexed PkProductId column.
                // The rowid is set to PkProductId for consistent references.
                var createFtsSql = @"CREATE VIRTUAL TABLE IF NOT EXISTS ProductFTS USING fts5(Name, PkProductId UNINDEXED);";
                await db.Database.ExecuteSqlRawAsync(createFtsSql, cancellationToken);

                // ── Insert New Products into FTS5 ──────────────────────────────────
                // Only insert products not already in the FTS table.
                // This is incremental - only new products are added.
                var sql = @"INSERT INTO ProductFTS(rowid, Name, PkProductId)
SELECT PkProductId, Name, PkProductId FROM Products
WHERE PkProductId NOT IN (SELECT rowid FROM ProductFTS);
";
                await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            }
            catch (Microsoft.Data.Sqlite.SqliteException sqlEx)
            {
                // SQLite-specific errors (e.g., FTS5 module issues, table corruption)
                _logger.LogError(sqlEx, "SQLite error while ensuring ProductFTS contents");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure ProductFTS contents");
            }

            // ╔══════════════════════════════════════════════════════════════════════╗
            // ║ PHASE 2: Precomputed Fuzzy Suggestions Rebuild                       ║
            // ╚══════════════════════════════════════════════════════════════════════╝
            // Precompute fuzzy suggestion records with normalized names for fast autocomplete.
            // The DELETE + INSERT is wrapped in a transaction so search requests never see
            // an empty FuzzySuggestions table during the rebuild.
            try
            {
                // ── Query All Products with Metadata ───────────────────────────────
                var suggestions = await db.Products
                    .Select(p => new ELKH.Models.FuzzySuggestionModel
                    {
                        PkProductId    = p.PkProductId,
                        Name           = p.Name,
                        NameNormalized = p.Name.ToLowerInvariant(), // For case-insensitive prefix matching
                        Price          = p.Price,
                        Thumbnail      = p.ProductImage!.Select(pi => pi.ProductImageURL).FirstOrDefault() ?? string.Empty,
                        CreatedAt      = DateTime.UtcNow
                    })
                    .ToListAsync(cancellationToken);

                // ── Atomic Replacement with Transaction ────────────────────────────
                // Transaction ensures search never sees empty table between DELETE and INSERT.
                using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

                await db.Database.ExecuteSqlRawAsync("DELETE FROM FuzzySuggestions", cancellationToken);
                db.FuzzySuggestions.AddRange(suggestions);
                await db.SaveChangesAsync(cancellationToken);

                await tx.CommitAsync(cancellationToken);

                _logger.LogInformation("Fuzzy suggestions reindexed: {Count}", suggestions.Count);

                // ── Update Thread-Safe Metrics ─────────────────────────────────────
                lock (_metricsLock)
                {
                    _lastRun      = DateTime.UtcNow;
                    _lastDuration = _lastRun.Value - start;
                    _runCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to precompute fuzzy suggestions");
            }
        }

        /// <summary>
        /// Gets the UTC timestamp of the last successful reindex operation.
        /// Thread-safe for access from HTTP request threads.
        /// </summary>
        public DateTime? LastRun { get { lock (_metricsLock) return _lastRun; } }

        /// <summary>
        /// Gets the duration of the last reindex operation.
        /// Thread-safe for access from HTTP request threads.
        /// </summary>
        public TimeSpan? LastDuration { get { lock (_metricsLock) return _lastDuration; } }

        /// <summary>
        /// Gets the total number of reindex operations performed since application startup.
        /// Thread-safe for access from HTTP request threads.
        /// </summary>
        public int RunCount { get { lock (_metricsLock) return _runCount; } }

        #endregion
    }
}
