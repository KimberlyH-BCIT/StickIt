namespace ELKH.Configuration
{
    /// <summary>
    /// Cache expiration and rate limiting configuration options.
    /// Controls in-memory cache behavior for fuzzy search and moderation operations.
    /// </summary>
    public class CacheOptions
    {
        /// <summary>
        /// Fuzzy search cache expiration settings
        /// </summary>
        public FuzzyOptions Fuzzy { get; set; } = new FuzzyOptions();

        /// <summary>
        /// Rate limit window in seconds for moderator flag operations.
        /// Prevents abuse by limiting how frequently a moderator can flag items.
        /// Default: 5 seconds
        /// </summary>
        public int FlagRateLimitSeconds { get; set; } = 5;

        /// <summary>
        /// Absolute expiration in minutes for the user-by-email cache entries in UserService.
        /// Default: 10 minutes
        /// </summary>
        public int UserLookupExpirationMinutes { get; set; } = 10;

        /// <summary>
        /// Fuzzy search cache expiration configuration
        /// </summary>
        public class FuzzyOptions
        {
            /// <summary>
            /// Sliding expiration time in minutes for fuzzy search cache entries.
            /// Cache entry lifetime is extended each time it's accessed.
            /// Default: 5 minutes
            /// </summary>
            public int SlidingExpirationMinutes { get; set; } = 5;

            /// <summary>
            /// Absolute expiration time in minutes for fuzzy search cache entries.
            /// Cache entry is removed after this time regardless of access.
            /// Default: 60 minutes (1 hour)
            /// </summary>
            public int AbsoluteExpirationMinutes { get; set; } = 60;
        }
    }
}
