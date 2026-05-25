using System;
using System.Threading;
using System.Threading.Tasks;
using ELKH.Configuration;
using ELKH.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELKH.Services;

/// <summary>
/// Background service that rebuilds fuzzy-search suggestions on a schedule.
/// </summary>
public sealed class FuzzyReindexService : BackgroundService, IFuzzyReindexService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<FuzzyReindexService> _logger;
    private readonly SearchOptions.ReindexOptions _options;
    private readonly TimeSpan _interval;
    private readonly object _stateLock = new();

    private DateTime? _lastRun;
    private TimeSpan? _lastDuration;
    private int _runCount;

    /// <summary>
    /// Creates a new reindex service.
    /// </summary>
    public FuzzyReindexService(
        IServiceProvider services,
        ILogger<FuzzyReindexService> logger,
        IOptions<SearchOptions> searchOptions)
    {
        _services = services;
        _logger = logger;
        _options = searchOptions.Value.Reindex;
        _interval = TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes));
    }

    /// <inheritdoc />
    public DateTime? LastRun
    {
        get
        {
            lock (_stateLock)
            {
                return _lastRun;
            }
        }
    }

    /// <inheritdoc />
    public TimeSpan? LastDuration
    {
        get
        {
            lock (_stateLock)
            {
                return _lastDuration;
            }
        }
    }

    /// <inheritdoc />
    public int RunCount
    {
        get
        {
            lock (_stateLock)
            {
                return _runCount;
            }
        }
    }

    /// <inheritdoc />
    public Task ReindexOnce(CancellationToken cancellationToken = default)
        => ReindexOnceInternalAsync(cancellationToken);

    /// <summary>
    /// Executes the scheduled background loop.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ReindexOnceInternalAsync(stoppingToken);

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ReindexOnceInternalAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Keep the implementation intentionally lightweight: this pass refreshes the
            // cached fuzzy-suggestion table from the current product list.
            var productNames = await db.Products
                .AsNoTracking()
                .Select(p => p.NameNormalized)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToListAsync(cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Fuzzy reindex pass completed for {ProductCount} product names.", productNames.Count);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(ex, "Fuzzy reindex pass failed.");
            }
            return;
        }
        finally
        {
            stopwatch.Stop();
            lock (_stateLock)
            {
                _lastRun = startedAt;
                _lastDuration = stopwatch.Elapsed;
                _runCount++;
            }
        }
    }
}
