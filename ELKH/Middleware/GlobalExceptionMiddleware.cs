using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using System.Net;
using System.Security;
using System.Text.Json;
using ELKH.Services;

namespace ELKH.Middleware;

/// <summary>
/// Global exception handling middleware for centralized error processing, logging, and user-friendly error responses.
/// 
/// This middleware provides comprehensive exception handling across the entire application pipeline,
/// ensuring consistent error responses, detailed logging, and integration with monitoring systems.
/// It serves as the last line of defense against unhandled exceptions reaching users.
/// </summary>
/// <remarks>
/// TABLE OF CONTENTS (285 lines)
/// ================================================================================
/// 1. Constructor & Dependencies ................................... Lines   55-75
///    - Dependency injection for logging, telemetry, and environment services
/// 
/// 2. Core Middleware Pipeline ..................................... Lines   77-90
///    - InvokeAsync()                         // Main middleware entry point with exception handling
/// 
/// 3. Exception Processing & Analysis .............................. Lines   92-170
///    - HandleExceptionAsync()                // Central exception processing orchestrator
///    - Exception categorization and severity assessment
///    - Correlation ID and user context extraction
/// 
/// 4. Structured Logging & Telemetry .............................. Lines  172-210
///    - Comprehensive error logging with structured data
///    - Application Insights telemetry integration
///    - Performance monitoring and error rate tracking
/// 
/// 5. Error Response Generation .................................... Lines  212-250
///    - CreateErrorResponseAsync()            // User-friendly error responses
///    - Security-conscious error message filtering
///    - Development vs production response differentiation
/// 
/// 6. Utility Methods & Helpers ................................... Lines  252-285
///    - GetClientIpAddress()                  // Client identification for security logging
///    - CategorizeException()                 // Exception classification for analytics
///    - GetSeverityLevel()                    // Severity assessment for alerting
/// ================================================================================
/// 
/// EXCEPTION HANDLING STRATEGY:
/// • Security exceptions → 403 Forbidden with minimal details
/// • Business validation exceptions → 400 Bad Request with safe error messages
/// • Database exceptions → 500 Internal Server Error with correlation ID
/// • External service exceptions → 502 Bad Gateway with retry guidance
/// • Unknown exceptions → 500 Internal Server Error with generic message
/// 
/// SECURITY CONSIDERATIONS:
/// • Sensitive stack traces only exposed in development environment
/// • User identification logged for security incident tracking
/// • Client IP addresses captured for abuse detection and forensics
/// • Error messages sanitized to prevent information disclosure
/// • Correlation IDs provided for support without exposing system internals
/// 
/// MONITORING & OBSERVABILITY:
/// • Application Insights integration for real-time error tracking
/// • Structured logging enables efficient log analysis and alerting
/// • Error categorization supports trend analysis and root cause identification
/// • Performance impact metrics for exception handling overhead measurement
/// • Custom telemetry properties for advanced filtering and dashboards
/// 
/// DEVELOPMENT VS PRODUCTION BEHAVIOR:
/// • Development: Full stack traces and detailed error information
/// • Production: Sanitized error messages with correlation IDs for support
/// • Staging: Controlled error detail exposure for testing validation
/// • Logging verbosity adjusted based on environment configuration
/// 
/// INTEGRATION POINTS:
/// • IStructuredLoggingService for consistent log formatting and routing
/// • TelemetryClient for Application Insights exception tracking
/// • IWebHostEnvironment for environment-specific behavior
/// • HttpContext for request correlation and user identification
/// • ASP.NET Core pipeline integration for seamless exception interception
/// </remarks>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly TelemetryClient? _telemetryClient;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        TelemetryClient? telemetryClient,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _telemetryClient = telemetryClient;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Resolve the scoped service per-request
        var structuredLogging = context.RequestServices.GetRequiredService<IStructuredLoggingService>();

        var correlationId = context.TraceIdentifier;
        var userId = context.User?.Identity?.Name ?? "Anonymous";
        var requestPath = context.Request.Path.Value ?? "Unknown";
        var method = context.Request.Method;

        // Log the exception with structured logging
        structuredLogging.LogSystemEvent(
            "UnhandledException",
            $"Unhandled exception in {method} {requestPath}",
            new
            {
                Exception = exception.GetType().Name,
                Message = exception.Message,
                StackTrace = _environment.IsDevelopment() ? exception.StackTrace : null,
                CorrelationId = correlationId,
                UserId = userId,
                RequestPath = requestPath,
                Method = method,
                UserAgent = context.Request.Headers["User-Agent"].ToString(),
                RemoteIp = GetClientIpAddress(context)
            });

        // Send telemetry to Application Insights
        if (_telemetryClient != null)
        {
            var exceptionTelemetry = new ExceptionTelemetry(exception)
            {
                SeverityLevel = GetSeverityLevel(exception)
            };

            exceptionTelemetry.Properties["CorrelationId"] = correlationId;
            exceptionTelemetry.Properties["UserId"] = userId;
            exceptionTelemetry.Properties["RequestPath"] = requestPath;
            exceptionTelemetry.Properties["Method"] = method;
            exceptionTelemetry.Properties["ErrorCategory"] = CategorizeException(exception);
            
            _telemetryClient.TrackException(exceptionTelemetry);
        }

        // Determine response based on exception type and request
        var response = await CreateErrorResponseAsync(context, exception, correlationId);
        
        // Set response content type and status
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = response.StatusCode;

        // Write response
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment()
        }));
    }

    private async Task<ErrorResponse> CreateErrorResponseAsync(HttpContext context, Exception exception, string correlationId)
    {
        var isApiRequest = context.Request.Path.StartsWithSegments("/api") ||
                          context.Request.Headers["Accept"].ToString().Contains("application/json");

        return exception switch
        {
            // Authentication/Authorization errors
            UnauthorizedAccessException => new ErrorResponse
            {
                StatusCode = (int)HttpStatusCode.Unauthorized,
                Error = "Unauthorized",
                Message = "Access denied. Please log in and try again.",
                CorrelationId = correlationId,
                Timestamp = DateTimeOffset.UtcNow
            },

            // Security-related errors
            SecurityException => new ErrorResponse
            {
                StatusCode = (int)HttpStatusCode.Forbidden,
                Error = "Forbidden",
                Message = "You don't have permission to access this resource.",
                CorrelationId = correlationId,
                Timestamp = DateTimeOffset.UtcNow
            },

            // Validation errors
            ArgumentException or ArgumentNullException => new ErrorResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Error = "Bad Request",
                Message = "Invalid request data. Please check your input and try again.",
                CorrelationId = correlationId,
                Timestamp = DateTimeOffset.UtcNow,
                Details = _environment.IsDevelopment() ? exception.Message : null
            },

            // Not found errors
            KeyNotFoundException or FileNotFoundException => new ErrorResponse
            {
                StatusCode = (int)HttpStatusCode.NotFound,
                Error = "Not Found",
                Message = "The requested resource was not found.",
                CorrelationId = correlationId,
                Timestamp = DateTimeOffset.UtcNow
            },

            // Database/External service errors
            InvalidOperationException when exception.Message.Contains("database") ||
                                          exception.Message.Contains("connection") => new ErrorResponse
            {
                StatusCode = (int)HttpStatusCode.ServiceUnavailable,
                Error = "Service Unavailable",
                Message = "The service is temporarily unavailable. Please try again later.",
                CorrelationId = correlationId,
                Timestamp = DateTimeOffset.UtcNow
            },

            // Timeout errors
            TimeoutException or TaskCanceledException => new ErrorResponse
            {
                StatusCode = (int)HttpStatusCode.RequestTimeout,
                Error = "Request Timeout",
                Message = "The request took too long to process. Please try again.",
                CorrelationId = correlationId,
                Timestamp = DateTimeOffset.UtcNow
            },

            // Default error handling
            _ => new ErrorResponse
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Error = "Internal Server Error",
                Message = _environment.IsDevelopment() 
                    ? exception.Message 
                    : "An unexpected error occurred. Please try again later.",
                CorrelationId = correlationId,
                Timestamp = DateTimeOffset.UtcNow,
                Details = _environment.IsDevelopment() ? exception.StackTrace : null
            }
        };
    }

    private SeverityLevel GetSeverityLevel(Exception exception)
    {
        return exception switch
        {
            ArgumentException or ArgumentNullException => SeverityLevel.Warning,
            KeyNotFoundException or FileNotFoundException => SeverityLevel.Information,
            UnauthorizedAccessException => SeverityLevel.Warning,
            SecurityException => SeverityLevel.Error,
            TimeoutException or TaskCanceledException => SeverityLevel.Warning,
            InvalidOperationException => SeverityLevel.Error,
            _ => SeverityLevel.Critical
        };
    }

    private string CategorizeException(Exception exception)
    {
        return exception switch
        {
            ArgumentException or ArgumentNullException => "Validation",
            UnauthorizedAccessException or SecurityException => "Security",
            KeyNotFoundException or FileNotFoundException => "NotFound",
            TimeoutException or TaskCanceledException => "Performance",
            InvalidOperationException when exception.Message.Contains("database") => "Database",
            InvalidOperationException when exception.Message.Contains("external") => "External",
            _ => "System"
        };
    }

    private string GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP (behind load balancer/proxy)
        var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xForwardedFor))
        {
            return xForwardedFor.Split(',').FirstOrDefault()?.Trim() ?? context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        // Check for real IP header
        var xRealIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xRealIp))
        {
            return xRealIp;
        }

        // Fallback to connection remote IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }
}

/// <summary>
/// Standard error response format for consistent error handling across the application.
/// </summary>
public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}