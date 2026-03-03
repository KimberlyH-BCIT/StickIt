namespace ELKH.Services
{
    /// <summary>
    /// Abstraction over the background fuzzy-search reindex service.
    /// Exposing an interface lets controllers and tests depend on the contract
    /// rather than the concrete BackgroundService implementation.
    /// </summary>
    public interface IFuzzyReindexService
    {
        /// <summary>Trigger a single reindex pass immediately.</summary>
        Task ReindexOnce(CancellationToken cancellationToken = default);

        /// <summary>UTC timestamp of the most recent successful reindex, or null if not yet run.</summary>
        DateTime? LastRun { get; }

        /// <summary>Wall-clock duration of the most recent reindex pass, or null if not yet run.</summary>
        TimeSpan? LastDuration { get; }

        /// <summary>Total number of reindex passes completed since startup.</summary>
        int RunCount { get; }
    }
}
