namespace ELKH.Constants
{
    /// <summary>
    /// Constants for ASP.NET Core rate limiting policy names.
    /// Used to reference rate limiting policies consistently across the application.
    /// </summary>
    public static class RateLimitPolicies
    {
        /// <summary>
        /// Rate limit policy for authentication endpoints (login, register).
        /// Strict: 5 attempts per 60 seconds to protect against brute force attacks.
        /// </summary>
        public const string Auth = "AuthPolicy";

        /// <summary>
        /// Rate limit policy for checkout and payment endpoints.
        /// 3 payment attempts per 60 seconds per IP to prevent payment fraud.
        /// </summary>
        public const string Checkout = "CheckoutPolicy";

        /// <summary>
        /// Rate limit policy for search autocomplete endpoints.
        /// 30 requests per 10 seconds with sliding window for responsive live typing.
        /// </summary>
        public const string Search = "SearchPolicy";

        /// <summary>
        /// Rate limit policy for admin operations.
        /// 10 requests per 60 seconds to protect resource-intensive admin actions
        /// such as reindexing, cache clearing, and bulk operations.
        /// </summary>
        public const string Admin = "AdminPolicy";

        /// <summary>
        /// Rate limit policy for cart operations.
        /// 20 requests per 60 seconds to prevent inventory enumeration attacks
        /// while allowing normal shopping behavior.
        /// </summary>
        public const string Cart = "CartPolicy";
    }
}
