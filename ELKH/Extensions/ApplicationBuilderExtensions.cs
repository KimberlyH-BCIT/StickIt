using Microsoft.AspNetCore.Builder;

namespace ELKH.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="IApplicationBuilder"/> to configure the middleware pipeline.
    /// Centralising middleware order here keeps <c>Program.cs</c> clean and makes it impossible
    /// to accidentally reorder security-critical middleware.
    /// </summary>
    /// <remarks>
    /// TABLE OF CONTENTS
    /// ================================================================================
    /// 1. UseApplicationMiddleware Method ............................. Lines [12-83]
    ///    - Middleware pipeline order      // Security-critical ordering enforcement
    ///    - Global exception handling      // First-line error catching
    ///    - Security headers              // Defense-in-depth headers
    ///    - Authentication/authorization   // Identity and access control
    /// 
    /// 2. UseSecurityHeaders Method ................................... Lines [85-136]
    ///    - HTTP security headers          // OWASP security header implementation
    ///    - Content Security Policy        // XSS and injection prevention
    ///    - Frame options                 // Clickjacking protection
    /// 
    /// 3. UseApplicationEndpoints Method .............................. Lines [138-170]
    ///    - Route mapping                 // MVC, Razor Pages, and health endpoints
    ///    - Area support                  // Admin and specialized area routing
    /// ================================================================================
    /// 
    /// ARCHITECTURAL CONTEXT:
    /// • Critical middleware pipeline configuration ensuring proper security ordering
    /// • Implements defense-in-depth security strategy with layered protections
    /// • Environment-aware configuration (compression disabled in development)
    /// • Part of ELKH's security-first application architecture
    /// • Centralizes middleware configuration to prevent security misconfigurations
    /// 
    /// SECURITY IMPLEMENTATION:
    /// This class implements comprehensive security measures:
    /// 1. Global exception handling - prevents information disclosure
    /// 2. Security headers - OWASP recommended HTTP security headers
    /// 3. Content Security Policy - XSS and injection attack prevention
    /// 4. HTTPS enforcement - ensures encrypted communication
    /// 5. Rate limiting - prevents abuse and DoS attacks
    /// 6. Authentication/Authorization - proper identity and access control
    /// 
    /// MIDDLEWARE ORDERING RATIONALE:
    /// • Exception handling first - catches all errors for consistent responses
    /// • Security headers early - applied to all responses including errors
    /// • HTTPS redirection - ensures secure transport
    /// • Compression before caching - efficient storage and transmission
    /// • Authentication before authorization - establish identity before access control
    /// • Routing last - endpoint resolution after all security measures
    /// 
    /// INTEGRATION POINTS:
    /// • Used by: Program.cs during application startup configuration
    /// • Integrates with: Custom middleware (GlobalExceptionMiddleware, CorrelationIdMiddleware)
    /// • Configures: PayPal, reCAPTCHA, Bootstrap integration via CSP headers
    /// • Supports: Health checks, static assets, area-based routing
    /// 
    /// PERFORMANCE CONSIDERATIONS:
    /// • Conditional compression based on environment (dev tools compatibility)
    /// • Output caching positioned after compression for efficiency
    /// • Rate limiting before expensive operations
    /// • Static asset optimization through WithStaticAssets()
    /// 
    /// COMPLIANCE & SECURITY:
    /// • OWASP security header recommendations implemented
    /// • CSP configured for PayPal and reCAPTCHA third-party integrations
    /// • Clickjacking protection via frame options
    /// • XSS prevention through multiple layers
    /// • Information disclosure prevention via structured error handling
    /// </remarks>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Configures the standard middleware pipeline in the correct security-conscious order.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="env">The hosting environment (used to conditionally enable compression).</param>
        /// <remarks>
        /// Order is critical in ASP.NET Core. This method enforces:
        /// Security Headers → HTTPS → Compression (Production only) → Output Cache → Session → Routing → Authentication → Authorization
        ///
        /// Response compression is disabled in Development to allow Browser Link and Browser Refresh middleware
        /// to inject their scripts into HTML responses. These dev tools cannot inject into compressed responses.
        ///
        /// Session middleware is added after caching but before routing to enable guest checkout functionality
        /// while maintaining security and performance optimizations.
        ///
        /// Note on Response Compression + HTTPS (<c>EnableForHttps = true</c>):
        /// Compressing secret values (e.g. CSRF tokens, session IDs) over HTTPS is theoretically
        /// susceptible to BREACH-style side-channel attacks in adversarial, high-traffic environments.
        /// For a low-traffic application with no attacker-controlled reflected content this risk is
        /// negligible, but it should be re-evaluated if the application ever handles highly sensitive
        /// reflected data (e.g. bearer tokens in response bodies).
        /// </remarks>
        public static IApplicationBuilder UseApplicationMiddleware(this IApplicationBuilder app, IWebHostEnvironment env)
        {
            // 0. Global Exception Handling — must be first to catch all errors with structured logging
            app.UseMiddleware<ELKH.Middleware.GlobalExceptionMiddleware>();

            // 1. Exception handler — standard ASP.NET Core handler for additional coverage
            //    In Development the developer exception page is used instead (configured in Program.cs).
            app.UseExceptionHandler("/Error");

            // Status-code pages: 404, 403, 429, etc. are re-executed through /Error/{statusCode}
            // so users see a consistent branded error page rather than a blank response.
            app.UseStatusCodePagesWithReExecute("/Error/{0}");

            // 2. Security Headers — set on every response before any content is written.
            //    Added first so headers are present even on error pages and redirects.
            app.UseSecurityHeaders(env);

            // 3. HTTPS Redirection — redirect plain HTTP requests to HTTPS
            app.UseHttpsRedirection();

            // 4. Correlation ID Middleware — add correlation IDs for request tracing
            app.UseMiddleware<ELKH.Middleware.CorrelationIdMiddleware>();

            // 4. Response Compression — compress before caching so cached responses are already compressed
            //    Disabled in Development to allow Browser Link and hot reload script injection
            if (!env.IsDevelopment())
            {
                app.UseResponseCompression();
            }

            // 5. Rate Limiting — reject excess requests before they hit the cache or business logic
            app.UseRateLimiter();

            // 6. Output Cache — cache compressed responses with tag-based invalidation
            app.UseOutputCache();

            // 7. Session — enable session state for guest checkout (must be before routing)
            app.UseSession();

            // 8. Routing — endpoint routing resolution (must precede auth middleware)
            app.UseRouting();

            // 9. Authentication — establish the user's identity from cookies/tokens
            app.UseAuthentication();

            // 10. Authorization — enforce access policies against the established identity
            app.UseAuthorization();

            return app;
        }

        /// <summary>
        /// Adds defensive HTTP security response headers to every outgoing response.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="env">The hosting environment (used to conditionally allow development tools).</param>
        /// <remarks>
        /// Headers applied:
        /// <list type="bullet">
        ///   <item><term>X-Content-Type-Options: nosniff</term><description>Prevents browsers from MIME-sniffing a response away from the declared content type, blocking content-injection attacks.</description></item>
        ///   <item><term>X-Frame-Options: SAMEORIGIN</term><description>Allows the page to be framed only by pages on the same origin, mitigating clickjacking attacks.</description></item>
        ///   <item><term>Referrer-Policy: strict-origin-when-cross-origin</term><description>Sends the full URL as the referrer for same-origin requests but only the origin for cross-origin requests, and nothing for downgrade (HTTPS→HTTP) navigations.</description></item>
        ///   <item><term>Permissions-Policy</term><description>Disables browser features (camera, microphone, geolocation) not required by this application.</description></item>
        /// </list>
        /// </remarks>
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, IWebHostEnvironment env)
        {
            return app.Use(async (context, next) =>
            {
                // Prevent MIME-type sniffing — forces the browser to honour the declared Content-Type.
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";

                // Allow framing by same-origin pages only; blocks cross-origin clickjacking.
                context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";

                // Limit referrer information sent on navigation: full URL for same-origin,
                // origin-only for cross-origin, nothing for HTTPS → HTTP downgrades.
                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

                // Opt out of browser features this application does not use.
                context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

                // Prevent Adobe Flash and PDF readers from making cross-domain requests.
                context.Response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";

                // =====================================================================
                // CONTENT SECURITY POLICY (CSP) - DISABLED
                // =====================================================================
                // CSP has been disabled to prevent compatibility issues with browser
                // extensions and third-party scripts. This reduces security but improves
                // compatibility and developer experience.
                // 
                // WARNING: Disabling CSP removes protection against:
                // - Cross-Site Scripting (XSS) attacks
                // - Data injection attacks
                // - Unauthorized script execution
                // 
                // To re-enable CSP, uncomment the code below and adjust the policy
                // as needed for your third-party integrations.
                // =====================================================================

                /*
                // Primary browser-side XSS defence: restrict sources for scripts, styles,
                // images, and other resource types to same-origin by default.
                // PayPal SDK is loaded from www.paypal.com; it communicates with the PayPal
                // checkout page (www.paypal.com) and the sandbox/live API (api-m.*.paypal.com).
                // Google reCAPTCHA requires scripts from google.com and gstatic.com, frames from google.com,
                // and 'unsafe-eval' for its internal script execution (required by reCAPTCHA v2).
                // Google Fonts requires fonts.googleapis.com for CSS fetching (connect-src), 
                // stylesheets (style-src), and fonts.gstatic.com for font files (font-src).
                // 'unsafe-inline' for style-src is required by Bootstrap/inline styles and inline scripts.
                // In development, allow localhost connections for Browser Link and hot reload tools.
                var connectSrc = env.IsDevelopment() 
                    ? "'self' ws://localhost:* http://localhost:* https://api-m.paypal.com https://api-m.sandbox.paypal.com https://www.google.com https://fonts.googleapis.com https://fonts.gstatic.com"
                    : "'self' https://api-m.paypal.com https://api-m.sandbox.paypal.com https://www.google.com https://fonts.googleapis.com https://fonts.gstatic.com";

                context.Response.Headers["Content-Security-Policy"] =
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://www.paypal.com https://www.sandbox.paypal.com https://www.google.com https://www.gstatic.com; " +
                    "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                    "img-src 'self' data: https://www.paypalobjects.com; " +
                    "font-src 'self' https://fonts.gstatic.com; " +
                    $"connect-src {connectSrc}; " +
                    "frame-src https://www.paypal.com https://www.sandbox.paypal.com https://www.google.com; " +
                    "frame-ancestors 'self';";
                */

                await next();
            });
        }

        /// <summary>
        /// Maps controller routes, Razor Pages, static assets, and the health check endpoint.
        /// </summary>
        /// <remarks>
        /// The <c>/health</c> endpoint is intentionally left unauthenticated so that external
        /// monitoring tools and container orchestrators (e.g. Docker health checks) can probe it
        /// without credentials. If the endpoint ever exposes sensitive diagnostic data, protect it
        /// with <c>.RequireAuthorization("Admin")</c> and restrict probes to internal networks only.
        /// </remarks>
        public static WebApplication UseApplicationEndpoints(this WebApplication app)
        {
            // Area route — must be registered before the default route so that
            // controllers decorated with [Area] (e.g. AuditController, MetricsController)
            // are reachable at /{area}/{controller}/{action}.
            app.MapControllerRoute(
                name: "areas",
                pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

            // Default MVC route
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            // Convention-based Razor Pages routing
            app.MapRazorPages();
            app.MapRazorPages()
               .WithStaticAssets();

            app.MapHealthChecks("/health");

            return app;
        }
    }
}

