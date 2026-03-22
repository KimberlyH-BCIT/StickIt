using Microsoft.AspNetCore.Builder;

namespace ELKH.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="IApplicationBuilder"/> to configure the middleware pipeline.
    /// Centralising middleware order here keeps <c>Program.cs</c> clean and makes it impossible
    /// to accidentally reorder security-critical middleware.
    /// </summary>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Configures the standard middleware pipeline in the correct security-conscious order.
        /// </summary>
        /// <remarks>
        /// Order is critical in ASP.NET Core. This method enforces:
        /// Security Headers → HTTPS → Compression → Output Cache → Routing → Authentication → Authorization
        ///
        /// Note on Response Compression + HTTPS (<c>EnableForHttps = true</c>):
        /// Compressing secret values (e.g. CSRF tokens, session IDs) over HTTPS is theoretically
        /// susceptible to BREACH-style side-channel attacks in adversarial, high-traffic environments.
        /// For a low-traffic application with no attacker-controlled reflected content this risk is
        /// negligible, but it should be re-evaluated if the application ever handles highly sensitive
        /// reflected data (e.g. bearer tokens in response bodies).
        /// </remarks>
        public static IApplicationBuilder UseApplicationMiddleware(this IApplicationBuilder app)
        {
            // 0. Exception handler — must be first so it catches errors from all subsequent middleware.
            //    In Development the developer exception page is used instead (configured in Program.cs).
            app.UseExceptionHandler("/Error");

            // Status-code pages: 404, 403, 429, etc. are re-executed through /Error/{statusCode}
            // so users see a consistent branded error page rather than a blank response.
            app.UseStatusCodePagesWithReExecute("/Error/{0}");

            // 1. Security Headers — set on every response before any content is written.
            //    Added first so headers are present even on error pages and redirects.
            app.UseSecurityHeaders();

            // 2. HTTPS Redirection — redirect plain HTTP requests to HTTPS
            app.UseHttpsRedirection();

            // 3. Response Compression — compress before caching so cached responses are already compressed
            app.UseResponseCompression();

            // 4. Rate Limiting — reject excess requests before they hit the cache or business logic
            app.UseRateLimiter();

            // 5. Output Cache — cache compressed responses with tag-based invalidation
            app.UseOutputCache();

            // 6. Routing — endpoint routing resolution (must precede auth middleware)
            app.UseRouting();

            // 7. Authentication — establish the user's identity from cookies/tokens
            app.UseAuthentication();

            // 8. Authorization — enforce access policies against the established identity
            app.UseAuthorization();

            return app;
        }

        /// <summary>
        /// Adds defensive HTTP security response headers to every outgoing response.
        /// </summary>
        /// <remarks>
        /// Headers applied:
        /// <list type="bullet">
        ///   <item><term>X-Content-Type-Options: nosniff</term><description>Prevents browsers from MIME-sniffing a response away from the declared content type, blocking content-injection attacks.</description></item>
        ///   <item><term>X-Frame-Options: SAMEORIGIN</term><description>Allows the page to be framed only by pages on the same origin, mitigating clickjacking attacks.</description></item>
        ///   <item><term>Referrer-Policy: strict-origin-when-cross-origin</term><description>Sends the full URL as the referrer for same-origin requests but only the origin for cross-origin requests, and nothing for downgrade (HTTPS→HTTP) navigations.</description></item>
        ///   <item><term>Permissions-Policy</term><description>Disables browser features (camera, microphone, geolocation) not required by this application.</description></item>
        /// </list>
        /// </remarks>
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
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

                // Primary browser-side XSS defence: restrict sources for scripts, styles,
                // images, and other resource types to same-origin by default.
                // PayPal SDK is loaded from www.paypal.com; it communicates with the PayPal
                // checkout page (www.paypal.com) and the sandbox/live API (api-m.*.paypal.com).
                // Google reCAPTCHA requires scripts from google.com and gstatic.com, frames from google.com,
                // and 'unsafe-eval' for its internal script execution (required by reCAPTCHA v2).
                // 'unsafe-inline' for style-src is required by Bootstrap/inline styles.
                context.Response.Headers["Content-Security-Policy"] =
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-eval' https://www.paypal.com https://www.sandbox.paypal.com https://www.google.com https://www.gstatic.com; " +
                    "style-src 'self' 'unsafe-inline'; " +
                    "img-src 'self' data: https://www.paypalobjects.com; " +
                    "font-src 'self'; " +
                    "connect-src 'self' https://api-m.paypal.com https://api-m.sandbox.paypal.com https://www.google.com; " +
                    "frame-src https://www.paypal.com https://www.sandbox.paypal.com https://www.google.com; " +
                    "frame-ancestors 'self';";

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
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            // Convention-based Razor Pages routing
            app.MapRazorPages();

            // Unauthenticated health check for uptime monitoring and container readiness probes
            app.MapHealthChecks("/health");

            return app;
        }
    }
}

