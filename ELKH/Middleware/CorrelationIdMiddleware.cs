using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ELKH.Middleware;

/// <summary>
/// Middleware to add correlation IDs to HTTP requests for distributed tracing
/// Enhances logging by providing unique identifiers for request tracking across services
/// </summary>
/// <remarks>
/// This middleware automatically:
/// - Generates or extracts correlation IDs from request headers
/// - Adds correlation IDs to response headers for client visibility
/// - Creates logging scopes with correlation context
/// - Tracks request timing and performance metrics
/// - Integrates with the StructuredLoggingService for consistent logging patterns
/// </remarks>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public const string CorrelationIdHeader = "X-Correlation-ID";
    public const string CorrelationIdLogKey = "CorrelationId";

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get correlation ID from header or generate a new one
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
                           ?? Guid.NewGuid().ToString();

        // Add correlation ID to response headers for clients
        context.Response.Headers.TryAdd(CorrelationIdHeader, correlationId);

        // Add to HttpContext items for access in controllers/services
        context.Items[CorrelationIdLogKey] = correlationId;

        // Create a logging scope with the correlation ID
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            [CorrelationIdLogKey] = correlationId,
            ["RequestPath"] = context.Request.Path,
            ["RequestMethod"] = context.Request.Method,
            ["UserAgent"] = context.Request.Headers.UserAgent.FirstOrDefault() ?? "Unknown",
            ["RemoteIpAddress"] = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
        });

        var startTime = DateTimeOffset.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Processing request {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await _next(context);

            stopwatch.Stop();

            _logger.LogInformation("Completed request {Method} {Path} with status {StatusCode} in {Duration}ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "Request {Method} {Path} failed after {Duration}ms",
                context.Request.Method,
                context.Request.Path,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}

/// <summary>
/// Extension methods for easily accessing correlation ID from HttpContext
/// </summary>
public static class CorrelationIdExtensions
{
    /// <summary>
    /// Gets the correlation ID from the current HTTP context
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <returns>The correlation ID if present, otherwise null</returns>
    public static string? GetCorrelationId(this HttpContext context)
    {
        return context.Items[CorrelationIdMiddleware.CorrelationIdLogKey]?.ToString();
    }

    /// <summary>
    /// Gets the correlation ID from the HTTP context accessor
    /// </summary>
    /// <param name="accessor">The HTTP context accessor</param>
    /// <returns>The correlation ID if present, otherwise null</returns>
    public static string? GetCorrelationId(this IHttpContextAccessor accessor)
    {
        return accessor.HttpContext?.GetCorrelationId();
    }
}
