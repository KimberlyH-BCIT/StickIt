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
    }
}
