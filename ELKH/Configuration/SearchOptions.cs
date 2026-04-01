namespace ELKH.Configuration
{
    /// <summary>
    /// Fuzzy search algorithm configuration options.
    /// Controls search behavior, performance tuning, and result quality thresholds.
    /// </summary>
    public class SearchOptions
    {
        /// <summary>
        /// Fuzzy search algorithm tuning parameters
        /// </summary>
        public FuzzyOptions Fuzzy { get; set; } = new FuzzyOptions();

        /// <summary>
        /// Reindexing service configuration options
        /// </summary>
        public ReindexOptions Reindex { get; set; } = new ReindexOptions();

        /// <summary>
        /// Fuzzy search algorithm configuration for tuning performance and quality.
        /// </summary>
        /// <remarks>
        /// Tuning Guide:
        /// - Increase CandidateLimit for better coverage (slower)
        /// - Decrease CandidateLimit for faster searches (may miss results)
        /// - Increase thresholds for stricter matching
        /// - Decrease thresholds for more lenient matching
        /// </remarks>
        public class FuzzyOptions
        {
            /// <summary>
            /// Maximum number of candidate products to consider for fuzzy matching.
            /// Higher values provide better coverage but slower searches.
            /// Default: 200
            /// </summary>
            public int CandidateLimit { get; set; } = 200;

            /// <summary>
            /// Maximum number of top-ranked results to return to the user.
            /// Default: 10
            /// </summary>
            public int TopResults { get; set; } = 10;

            /// <summary>
            /// Minimum delta for sliding window size (relative to token length).
            /// Negative values allow shorter windows for flexibility.
            /// Default: -1
            /// </summary>
            /// <remarks>
            /// Window size = token length + WindowMinDelta to token length + WindowMaxDelta
            /// </remarks>
            public int WindowMinDelta { get; set; } = -1;

            /// <summary>
            /// Maximum delta for sliding window size (relative to token length).
            /// Positive values allow longer windows to catch insertions/typos.
            /// Default: 2
            /// </summary>
            public int WindowMaxDelta { get; set; } = 2;

            /// <summary>
            /// PartialRatio threshold (0-100) to accept a sliding window match.
            /// Higher values require closer matches.
            /// Default: 65
            /// </summary>
            /// <remarks>
            /// PartialRatio compares query substring to candidate substrings.
            /// 65 = reasonably close match, 80 = very close, 90 = nearly exact
            /// </remarks>
            public int PartialRatioThreshold { get; set; } = 65;

            /// <summary>
            /// TokenSetRatio threshold (0-100) to include result in final ranking.
            /// Results below this threshold are filtered out.
            /// Default: 30 (very lenient - catches most relevant results)
            /// </summary>
            /// <remarks>
            /// TokenSetRatio compares full query to full candidate name.
            /// 30 = very lenient, 50 = moderate, 70 = strict
            /// </remarks>
            public int RankingThreshold { get; set; } = 30;
        }

        /// <summary>
        /// Configuration options for the fuzzy search reindexing service.
        /// Controls timeout, retry behavior, and performance settings.
        /// </summary>
        public class ReindexOptions
        {
            /// <summary>
            /// Database operation timeout in seconds.
            /// Default: 300 seconds (5 minutes)
            /// </summary>
            public int DatabaseTimeoutSeconds { get; set; } = 300;

            /// <summary>
            /// Maximum number of retry attempts for failed operations.
            /// Default: 3
            /// </summary>
            public int MaxRetryAttempts { get; set; } = 3;

            /// <summary>
            /// Initial delay between retry attempts in seconds.
            /// Default: 5 seconds
            /// </summary>
            public int RetryDelaySeconds { get; set; } = 5;

            /// <summary>
            /// Whether to use exponential backoff for retry delays.
            /// Default: true
            /// </summary>
            public bool UseExponentialBackoff { get; set; } = true;

            /// <summary>
            /// Maximum delay between retries in seconds when using exponential backoff.
            /// Default: 60 seconds
            /// </summary>
            public int MaxRetryDelaySeconds { get; set; } = 60;

            /// <summary>
            /// Batch size for inserting fuzzy suggestions to avoid large transactions.
            /// Default: 1000
            /// </summary>
            public int BatchSize { get; set; } = 1000;

            /// <summary>
            /// Interval between reindex operations in minutes.
            /// This can also be overridden by Search:ReindexIntervalMinutes in configuration.
            /// Default: 360 minutes (6 hours)
            /// </summary>
            public int IntervalMinutes { get; set; } = 360;
        }
    }
}
