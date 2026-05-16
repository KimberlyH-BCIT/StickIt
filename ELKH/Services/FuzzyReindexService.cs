using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ELKH.Configuration;
using ELKH.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELKH.Services
{

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
        private readonly SearchOptions.ReindexOptions _options;
        private TimeSpan _interval = TimeSpan.FromHours(6);

        // â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
        // â”‚ Thread-Safe Metrics                                                  â”‚
        // â”‚ Lock guards _lastRun, _lastDuration and _runCount which are written  â”‚
        // â”‚ from the background thread and read from HTTP request threads.       â”‚
        // â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
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
        /// <param name="searchOptions">Typed search configuration options containing reindex interval and retry settings.</param>
        public FuzzyReindexService(IServiceProvider services, ILogger<FuzzyReindexService> logger, Microsoft.Extensions.Configuration.IConfiguration configuration, IOptions<SearchOptions> searchOptions)
        {
            _services = services;
            _logger = logger;
            _options = searchOptions.Value.Reindex;

            // â”€â”€ Configuration: Prioritize new structured config, fallback to legacy â”€â”€â”€â”€â”€â”€
            // Priority: 1) Search:Reindex:IntervalMinutes, 2) Search:ReindexIntervalMinutes (legacy), 3) Default from options
            var minutes = _options.IntervalMinutes;
            var legacyMinutes = configuration.GetValue<int?>("Search:ReindexIntervalMinutes");
            if (legacyMinutes.HasValue && legacyMinutes.Value > 0)
            {
                minutes = legacyMinutes.Value;
                _logger.LogWarning("Using legacy Search:ReindexIntervalMinutes setting. Consider migrating to Search:Reindex:IntervalMinutes.");
            }
            if (minutes > 0) _interval = TimeSpan.FromMinutes(minutes);
        }

        #endregion

        #region Retry Logic

        /// <summary>
        /// Executes an operation with retry logic and exponential backoff.
        /// </summary>
        /// <param name="operation">The operation to execute</param>
        /// <param name="operationName">Name of the operation for logging</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        private async Task ExecuteWithRetryAsync(Func<Task> operation, string operationName, CancellationToken cancellationToken)
        {
            var attempt = 0;
            var delay = TimeSpan.FromSeconds(_options.RetryDelaySeconds);

            while (attempt <= _options.MaxRetryAttempts)
            {
                try
                {
                    await operation();
                    return; // Success, exit retry loop
                }
                catch (Exception ex) when (attempt < _options.MaxRetryAttempts && !cancellationToken.IsCancellationRequested)
                {
                    attempt++;

                    _logger.LogWarning(ex, "{OperationName} failed on attempt {Attempt}/{MaxAttempts}. Retrying in {Delay} seconds.",
                        operationName, attempt, _options.MaxRetryAttempts, delay.TotalSeconds);

                    try
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        _logger.LogInformation("{OperationName} was cancelled during retry delay", operationName);
                        throw;
                    }

                    // Calculate next delay with exponential backoff
                    if (_options.UseExponentialBackoff)
                    {
                        delay = TimeSpan.FromSeconds(Math.Min(
                            delay.TotalSeconds * 2,
                            _options.MaxRetryDelaySeconds));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{OperationName} failed after {Attempts} attempts", operationName, attempt);
                    throw;
                }
            }
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
            _logger.LogInformation("FuzzyReindexService starting up with interval: {Interval} minutes", _interval.TotalMinutes);

            // â”€â”€ Startup Reindex â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Run once immediately on application startup to ensure search index is current.
            try
            {
                _logger.LogInformation("Executing initial reindex on startup");
                await ReindexOnce(stoppingToken);
                _logger.LogInformation("Initial reindex completed successfully");
            }
            catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Initial reindex was cancelled due to application shutdown");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Initial fuzzy reindex failed. Service will continue with periodic reindexing.");
            }

            // â”€â”€ Periodic Reindex Loop â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Continue running until application shutdown (stoppingToken canceled).
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogDebug("Waiting {Interval} minutes until next reindex", _interval.TotalMinutes);
                    await Task.Delay(_interval, stoppingToken);

                    _logger.LogInformation("Starting periodic reindex");
                    await ReindexOnce(stoppingToken);
                    _logger.LogInformation("Periodic reindex completed successfully");
                }
                catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Periodic reindex cancelled due to application shutdown");
                    break; // Expected when application is shutting down - exit gracefully.
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Periodic reindex cancelled due to application shutdown");
                    break; // Handle both TaskCanceledException and OperationCanceledException
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Periodic fuzzy reindex failed. Will retry on next interval.");
                    // Continue running despite errors - next iteration may succeed.
                }
            }

            _logger.LogInformation("FuzzyReindexService stopped");
        }

        #endregion

        #region Reindexing Logic

        /// <summary>
        /// Returns <see langword="true"/> when the <c>Products</c> table is present in the
        /// SQLite schema.  Uses a direct <c>sqlite_master</c> query through raw ADO.NET so
        /// that the check never reaches EF Core's query pipeline â€” EF Core logs every failed
        /// SQL command at <c>fail</c> level before the exception propagates, which produces
        /// alarming noise even when the code handles the exception gracefully.
        /// </summary>
        private static async Task<bool> IsProductsTableReadyAsync(
            ApplicationDbContext db, CancellationToken cancellationToken)
        {
            var connection = db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Products'";
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result) > 0;
        }

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
            DateTime start = DateTime.UtcNow;

            await ExecuteWithRetryAsync(async () =>
            {
                // â”€â”€ Scoped DbContext Creation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                // Create a new scope to get a fresh DbContext (background services are singletons).
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Configure database timeout
                db.Database.SetCommandTimeout(TimeSpan.FromSeconds(_options.DatabaseTimeoutSeconds));

                // â”€â”€ Migration guard â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                // Query sqlite_master via raw ADO.NET rather than EF Core so that a
                // missing table never reaches the EF Core query pipeline (which logs
                // every failed command at fail level before the exception can be caught).
                if (!await IsProductsTableReadyAsync(db, cancellationToken))
                {
                    _logger.LogDebug("Products table not yet present; skipping reindex until migrations have run.");
                    return;
                }

                // â•”======================================================================â•—
                // â•‘ PHASE 1: FTS5 Virtual Table Rebuild                                  â•‘
                // â•š======================================================================â•
                await ExecuteFTS5RebuildAsync(db, cancellationToken);

                // â•”======================================================================â•—
                // â•‘ PHASE 2: Precomputed Fuzzy Suggestions Rebuild                       â•‘
                // â•š======================================================================â•
                await ExecuteFuzzySuggestionsRebuildAsync(db, cancellationToken);

                // â”€â”€ Update Thread-Safe Metrics â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                lock (_metricsLock)
                {
                    _lastRun = DateTime.UtcNow;
                    _lastDuration = _lastRun.Value - start;
                    _runCount++;
                }

            }, "ReindexOnce", cancellationToken);
        }

        /// <summary>
        /// Executes the FTS5 virtual table rebuild operation.
        /// </summary>
        private async Task ExecuteFTS5RebuildAsync(ApplicationDbContext db, CancellationToken cancellationToken)
        {
            try
            {
                // â”€â”€ Create FTS5 Virtual Table (Idempotent) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                // Using FTS5 with the product Name and an unindexed PkProductId column.
                // The rowid is set to PkProductId for consistent references.
                var createFtsSql = @"CREATE VIRTUAL TABLE IF NOT EXISTS ProductFTS USING fts5(Name, PkProductId UNINDEXED);";
                await db.Database.ExecuteSqlRawAsync(createFtsSql, cancellationToken);

                // â”€â”€ Insert New Products into FTS5 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                // Only insert products not already in the FTS table.
                // This is incremental - only new products are added.
                var sql = @"INSERT INTO ProductFTS(rowid, Name, PkProductId)
SELECT PkProductId, Name, PkProductId FROM Products
WHERE PkProductId NOT IN (SELECT rowid FROM ProductFTS);
";
                await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);

                _logger.LogInformation("FTS5 virtual table rebuild completed successfully");
            }
            catch (Microsoft.Data.Sqlite.SqliteException sqlEx)
            {
                // SQLite-specific errors (e.g., FTS5 module issues, table corruption)
                _logger.LogError(sqlEx, "SQLite error while ensuring ProductFTS contents");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure ProductFTS contents");
                throw;
            }
        }

        /// <summary>
        /// Executes the fuzzy suggestions rebuild operation with batching.
        /// </summary>
        private async Task ExecuteFuzzySuggestionsRebuildAsync(ApplicationDbContext db, CancellationToken cancellationToken)
        {
            try
            {
                // â”€â”€ Query All Products with Metadata â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                var suggestions = await db.Products
                    .Select(p => new ELKH.Models.FuzzySuggestionModel
                    {
                        PkProductId = p.PkProductId,
                        Name = p.Name,
                        NameNormalized = p.Name.ToLowerInvariant(), // For case-insensitive prefix matching
                        Price = p.Price,
                        Thumbnail = p.ProductImage!.Select(pi => pi.ProductImageURL).FirstOrDefault() ?? string.Empty,
                        CreatedAt = DateTime.UtcNow
                    })
                    .ToListAsync(cancellationToken);

                _logger.LogInformation("Queried {Count} products for fuzzy suggestions rebuild", suggestions.Count);

                // â”€â”€ Atomic Replacement with Transaction and Batching â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    // Clear existing suggestions
                    await db.Database.ExecuteSqlRawAsync("DELETE FROM FuzzySuggestions", cancellationToken);

                    // Insert new suggestions in batches to avoid large transaction blocks
                    var totalInserted = 0;
                    for (int i = 0; i < suggestions.Count; i += _options.BatchSize)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var batch = suggestions.Skip(i).Take(_options.BatchSize).ToList();
                        db.FuzzySuggestions.AddRange(batch);
                        await db.SaveChangesAsync(cancellationToken);

                        totalInserted += batch.Count;
                        _logger.LogDebug("Inserted batch {BatchNumber}: {BatchSize} suggestions ({TotalInserted}/{TotalCount})",
                            (i / _options.BatchSize) + 1, batch.Count, totalInserted, suggestions.Count);
                    }

                    await tx.CommitAsync(cancellationToken);

                    _logger.LogInformation("Fuzzy suggestions reindexed successfully: {Count} suggestions inserted in {BatchCount} batches",
                        suggestions.Count, Math.Ceiling((double)suggestions.Count / _options.BatchSize));
                }
                catch (Exception)
                {
                    await tx.RollbackAsync(cancellationToken);
                    _logger.LogError("Fuzzy suggestions rebuild transaction rolled back due to error");
                    throw;
                }
            }
            catch (Microsoft.Data.Sqlite.SqliteException sqlEx)
            {
                _logger.LogError(sqlEx, "SQLite error while rebuilding fuzzy suggestions");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rebuild fuzzy suggestions");
                throw;
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
