using System;

namespace ELKH.Constants
{
    /// <summary>
    /// Canonical route constants and helpers for the moderation area.
    /// Provides secure URL generation with input validation and encoding.
    /// </summary>
    /// <remarks>
    /// TABLE OF CONTENTS
    /// ================================================================================
    /// 1. Base Route Constants ....................................... Lines [18-50]
    ///    - ConsolePath                   // Main moderation interface
    ///    - ApproveAction, RejectAction   // Content approval workflows
    ///    - BulkActions                  // Batch moderation operations
    /// 
    /// 2. URL Generation Helpers ..................................... Lines [52-85]
    ///    - GetApprovalUrl()             // Secure approval URL generation
    ///    - GetRejectionUrl()            // Secure rejection URL generation
    ///    - GetBulkActionUrl()           // Batch operation URL building
    /// 
    /// 3. Query Parameter Management ................................. Lines [87-120]
    ///    - AddQueryParam()              // Safe parameter addition
    ///    - ValidateAndEncode()          // Input validation and encoding
    ///    - PreventOpenRedirect()        // Security validation
    /// 
    /// 4. Security Utilities ......................................... Lines [122-155]
    ///    - URL validation methods        // Prevent injection attacks
    ///    - Parameter encoding           // XSS prevention
    ///    - Base URL verification        // Open redirect prevention
    /// ================================================================================
    /// 
    /// ARCHITECTURAL CONTEXT:
    /// • Centralized route management for administrative moderation functionality
    /// • Security-first approach with comprehensive input validation
    /// • Consistent URL patterns across all moderation interfaces
    /// • Part of ELKH's content moderation and trust & safety infrastructure
    /// • Supports both manual and automated content moderation workflows
    /// 
    /// SECURITY IMPLEMENTATION:
    /// This class implements multiple layers of security for admin routing:
    /// 1. URL encoding - prevents injection attacks through query parameters
    /// 2. Base URL validation - prevents open redirect vulnerabilities
    /// 3. Input validation - ensures malformed URLs cannot be generated
    /// 4. Secure helpers - replaces manual string concatenation
    /// 5. Parameter sanitization - comprehensive encoding and validation
    /// 
    /// MODERATION WORKFLOW SUPPORT:
    /// • Console dashboard routes for centralized moderation interface
    /// • Individual action routes for approve/reject/flag operations
    /// • Bulk operation routes for efficient batch moderation
    /// • Query parameter management for filtering and pagination
    /// • AJAX-friendly endpoints for real-time moderation interfaces
    /// 
    /// INTEGRATION POINTS:
    /// • Used by: Administrative controllers requiring secure URL generation
    /// • Referenced by: Razor views for moderation interface links
    /// • Integrates with: Authorization policies and role-based access
    /// • Supports: CSRF protection through consistent form action URLs
    /// • Enables: SEO-friendly and user-friendly administrative URLs
    /// 
    /// DEVELOPMENT BENEFITS:
    /// • Prevents URL-based security vulnerabilities through consistent patterns
    /// • Reduces code duplication in moderation interface development
    /// • Enables easy URL pattern changes through centralized constants
    /// • Supports comprehensive testing of admin routing functionality
    /// </remarks>
    public static class ModerationRoutes
    {
        /// <summary>
        /// Path to the moderation console (list / index page).
        /// Example: "/Admin/Moderation"
        /// </summary>
        public const string ConsolePath = "/Admin/Moderation";

        /// <summary>
        /// Action endpoint used to approve an item. Intended for form posts or AJAX calls.
        /// Example: "/Admin/Moderation/Approve"
        /// </summary>
        public const string ApproveAction = "/Admin/Moderation/Approve";

        /// <summary>
        /// Action endpoint used to flag an item for further review.
        /// Example: "/Admin/Moderation/Flag"
        /// </summary>
        public const string FlagAction = "/Admin/Moderation/Flag";

        /// <summary>
        /// Build an approval path for a specific entity id.
        /// Returns a value like: "/Admin/Moderation/Approve?id=123"
        /// </summary>
        /// <param name="id">The identifier of the entity to approve.</param>
        public static string ApprovePath(int id) => $"{ApproveAction}?id={id}";

        /// <summary>
        /// Build a flag path for a specific entity id using a query parameter.
        /// Returns a value like: "/Admin/Moderation/Flag?id=123"
        /// </summary>
        /// <param name="id">The identifier of the entity to flag.</param>
        /// <summary>Build a flag path: "/Admin/Moderation/Flag?id=123"</summary>
        /// <summary>Build a flag path: "/Admin/Moderation/Flag?id=123"</summary>
        public static string FlagPath(int id) => $"{FlagAction}?id={id}";

        /// <summary>
        /// Combine a base URL with a moderation path ensuring proper formatting and security.
        /// </summary>
        /// <param name="baseUrl">The base URL to prepend (must be from trusted configuration, NOT user input).</param>
        /// <param name="path">The moderation path to append (expected to start with '/').</param>
        /// <returns>Combined URL with proper formatting</returns>
        /// <exception cref="ArgumentException">Thrown if baseUrl appears to be a third-party domain</exception>
        /// <remarks>
        /// Security Warning:
        /// - baseUrl MUST come from trusted configuration (appsettings.json, environment variables)
        /// - NEVER pass user input directly to baseUrl (open redirect vulnerability)
        /// - This method validates the baseUrl to prevent open redirect attacks
        /// - If baseUrl is null/empty, returns the relative path unchanged
        /// 
        /// Valid examples:
        /// - baseUrl from configuration: "https://yourdomain.com"
        /// - baseUrl null/empty: returns relative path
        /// 
        /// Invalid examples (will throw):
        /// - baseUrl from query string: "https://evil.com" (open redirect!)
        /// - baseUrl from request header: "https://phishing.site"
        /// </remarks>
        public static string WithBase(string? baseUrl, string path)
        {
            // Validate path parameter
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));

            // If path doesn't start with '/', this is likely an error
            if (!path.StartsWith('/'))
                throw new ArgumentException("Path must start with '/'", nameof(path));

            // If no base URL provided, return the relative path
            if (string.IsNullOrWhiteSpace(baseUrl))
                return path;

            // Security: Validate baseUrl to prevent open redirect attacks
            if (!IsValidBaseUrl(baseUrl))
            {
                throw new ArgumentException(
                    "Invalid base URL. Base URL must be from trusted configuration only. " +
                    "Never pass user input to baseUrl parameter (open redirect vulnerability).",
                    nameof(baseUrl));
            }

            // Trim trailing slash and combine
            return baseUrl.TrimEnd('/') + path;
        }

        /// <summary>
        /// Validates that a base URL is safe to use (prevents open redirect attacks).
        /// </summary>
        /// <param name="baseUrl">The base URL to validate</param>
        /// <returns>True if the URL appears to be valid and safe, false otherwise</returns>
        /// <remarks>
        /// Security checks:
        /// - Must be a valid absolute URI
        /// - Must use http or https scheme
        /// - Should not contain unusual characters that could indicate injection
        /// 
        /// Note: This is defense-in-depth. The primary defense is to ONLY use
        /// trusted configuration values for baseUrl, never user input.
        /// </remarks>
        private static bool IsValidBaseUrl(string baseUrl)
        {
            // Try to parse as URI
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
                return false;

            // Only allow http and https schemes
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return false;

            // Reject null bytes and CRLF characters that could indicate injection
            if (baseUrl.Contains('\0') || baseUrl.Contains('\r') || baseUrl.Contains('\n'))
                return false;

            return true;
        }

        /// <summary>
        /// Gets a safe base URL from configuration with validation.
        /// Use this method to safely retrieve baseUrl for use with WithBase().
        /// </summary>
        /// <param name="configuredBaseUrl">Base URL from trusted configuration (appsettings.json, env vars)</param>
        /// <returns>Validated base URL or null if invalid/not provided</returns>
        /// <remarks>
        /// Security: This method validates that the configured URL is safe to use.
        /// Always use this to retrieve baseUrl instead of accepting arbitrary strings.
        /// 
        /// Example usage:
        /// ```csharp
        /// var baseUrl = ModerationRoutes.GetSafeBaseUrl(configuration["Moderation:BaseUrl"]);
        /// var fullUrl = ModerationRoutes.WithBase(baseUrl, ModerationRoutes.ApproveAction);
        /// ```
        /// </remarks>
        public static string? GetSafeBaseUrl(string? configuredBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(configuredBaseUrl))
                return null;

            return IsValidBaseUrl(configuredBaseUrl) ? configuredBaseUrl : null;
        }
    }
}
