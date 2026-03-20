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
    /// <summary>
    /// Background service responsible for periodically rebuilding search-related
    /// artifacts used by the fuzzy search functionality.
    ///
    /// Responsibilities:
    /// - Ensure the full-text table used by search contains all products.
    /// - Precompute a lightweight set of fuzzy suggestion records for fast lookup.
    ///
    /// The service executes once on startup and then periodically based on a
    /// configured interval (default 6 hours). Errors are logged but do not stop
    /// the host from running.
    /// </summary>
    public class FuzzyReindexService : BackgroundService, IFuzzyReindexService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<FuzzyReindexService> _logger;
        private TimeSpan _interval = TimeSpan.FromHours(6);

        // Lock guards _lastRun, _lastDuration and _runCount which are written
        // from the background thread and read from HTTP request threads.
        private readonly object _metricsLock = new();
        private DateTime? _lastRun;
        private TimeSpan? _lastDuration;
        private int _runCount;

        /// <summary>
        /// Initializes a new instance of <see cref="FuzzyReindexService"/>.
        /// </summary>
        /// <param name="services">Root service provider used to create scoped EF contexts per reindex pass.</param>
        /// <param name="logger">Logger for reindex progress and error diagnostics.</param>
        /// <param name="configuration">
        /// Application configuration; reads <c>Search:ReindexIntervalMinutes</c> to override
        /// the default 6-hour periodic interval.
        /// </param>
        public FuzzyReindexService(IServiceProvider services, ILogger<FuzzyReindexService> logger, Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _services = services;
            _logger = logger;
            var minutes = configuration.GetValue<int?>("Search:ReindexIntervalMinutes");
            if (minutes.HasValue && minutes.Value > 0) _interval = TimeSpan.FromMinutes(minutes.Value);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // run once on startup then periodically
            try
            {
                await ReindexOnce(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Initial fuzzy reindex failed");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_interval, stoppingToken);
                    await ReindexOnce(stoppingToken);
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Periodic fuzzy reindex failed");
                }
            }
        }

        /// <summary>
        /// Perform a single reindex operation. This method is safe to call concurrently
        /// from the host loop but is intended to be called once at a time.
        ///
        /// The method performs two main steps:
        /// 1. Ensure the FTS table contains all products by using the same SQL used in migrations.
        /// 2. Precompute and replace the in-database suggestion records used by the fuzzy search service.
        ///
        /// Any exceptions are logged; callers should handle cancellation tokens to stop promptly.
        /// </summary>
        public async Task ReindexOnce(CancellationToken cancellationToken = default)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Rebuild ProductFTS using migration SQL (run same reindex used elsewhere)
            DateTime start = DateTime.UtcNow;
            try
            {
                // Ensure the FTS virtual table exists before attempting to insert.
                // Using FTS5 with the product name and an unindexed product id column
                // so we can reference the rowid while keeping the integer id available.
                var createFtsSql = @"CREATE VIRTUAL TABLE IF NOT EXISTS ProductFTS USING fts5(Name, PkProductId UNINDEXED);";
                await db.Database.ExecuteSqlRawAsync(createFtsSql, cancellationToken);

                var sql = @"INSERT INTO ProductFTS(rowid, Name, PkProductId)
SELECT PkProductId, Name, PkProductId FROM Products
WHERE PkProductId NOT IN (SELECT rowid FROM ProductFTS);
";
                await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            }
            catch (Microsoft.Data.Sqlite.SqliteException sqlEx)
            {
                // Sqlite-specific errors are logged with details to aid diagnosis.
                _logger.LogError(sqlEx, "SQLite error while ensuring ProductFTS contents");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure ProductFTS contents");
            }

            // Precompute top fuzzy suggestions: simple approach - store normalized name and metadata.
            //
            // The delete and insert are wrapped in a single transaction so concurrent
            // search requests never observe an empty suggestion table between the two steps.
            try
            {
                var suggestions = await db.Product
                    .Select(p => new ELKH.Models.FuzzySuggestionModel
                    {
                        PkProductId    = p.PkProductId,
                        Name           = p.Name,
                        NameNormalized = p.Name.ToLowerInvariant(),
                        Price          = p.Price,
                        Thumbnail      = p.ProductImage!.Select(pi => pi.ProductImageURL).FirstOrDefault() ?? string.Empty,
                        CreatedAt      = DateTime.UtcNow
                    })
                    .ToListAsync(cancellationToken);

                using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

                await db.Database.ExecuteSqlRawAsync("DELETE FROM FuzzySuggestions", cancellationToken);
                db.FuzzySuggestions.AddRange(suggestions);
                await db.SaveChangesAsync(cancellationToken);

                await tx.CommitAsync(cancellationToken);

                _logger.LogInformation("Fuzzy suggestions reindexed: {Count}", suggestions.Count);
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

        public DateTime?  LastRun      { get { lock (_metricsLock) return _lastRun; } }
        public TimeSpan?  LastDuration  { get { lock (_metricsLock) return _lastDuration; } }
        public int        RunCount      { get { lock (_metricsLock) return _runCount; } }
    }
}
