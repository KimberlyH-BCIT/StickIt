using System.Net;
using System.Security;
using System.Text.Json;
using ELKH.Services;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace ELKH.Middleware;

// TABLE OF CONTENTS
// - Middleware invocation
// - Exception logging
// - Error response generation

/// <summary>
/// Middleware that catches unhandled exceptions and returns standardized error responses.
/// 
/// This middleware provides comprehensive exception handling across the entire application pipeline,
/// ensuring consistent error responses, detailed logging, and integration with monitoring systems.
/// It serves as the last line of defense against unhandled exceptions reaching users.
/// </summary>
public class GlobalExceptionMiddleware
{
    private static readonly JsonSerializerOptions ApiJsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions DevelopmentApiJsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
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
        var isDevelopment = _environment.IsDevelopment();

        var correlationId = context.TraceIdentifier;
        var userId = context.User?.Identity?.Name ?? "Anonymous";
        var requestPath = context.Request.Path.Value ?? "Unknown";
        var method = context.Request.Method;
        var telemetryClient = context.RequestServices.GetService<TelemetryClient>();

        // Log the exception with structured logging
        structuredLogging.LogSystemEvent(
            "UnhandledException",
            $"Unhandled exception in {method} {requestPath}",
            new
            {
                Exception = exception.GetType().Name,
                Message = exception.Message,
                StackTrace = isDevelopment ? exception.StackTrace : null,
                CorrelationId = correlationId,
                UserId = userId,
                RequestPath = requestPath,
                Method = method,
                UserAgent = context.Request.Headers["User-Agent"].ToString(),
                RemoteIp = GetClientIpAddress(context)
            });

        // Send telemetry to Application Insights
        if (telemetryClient != null)
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

            telemetryClient.TrackException(exceptionTelemetry);
        }

        var isApiRequest = IsApiRequest(context);
        var response = CreateErrorResponse(exception, correlationId, isDevelopment);

        context.Response.StatusCode = response.StatusCode;

        if (isApiRequest)
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(
                response,
                isDevelopment ? DevelopmentApiJsonSerializerOptions : ApiJsonSerializerOptions));

            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(CreateHtmlErrorPage(response));
    }

    private static ErrorResponse CreateErrorResponse(Exception exception, string correlationId, bool isDevelopment)
    {
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
                Details = isDevelopment ? exception.Message : null
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
                Message = isDevelopment
                    ? exception.Message
                    : "An unexpected error occurred. Please try again later.",
                CorrelationId = correlationId,
                Timestamp = DateTimeOffset.UtcNow,
                Details = isDevelopment ? exception.StackTrace : null
            }
        };
    }

    private static bool IsApiRequest(HttpContext context)
    {
        return context.Request.Path.StartsWithSegments("/api");
    }

    private static string CreateHtmlErrorPage(ErrorResponse response)
    {
        var title = WebUtility.HtmlEncode(response.Error);
        var message = WebUtility.HtmlEncode(response.Message);
        var correlationId = WebUtility.HtmlEncode(response.CorrelationId);

        return $"<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>{title}</title></head><body><main><h1>{title}</h1><p>{message}</p><p>Request ID: <code>{correlationId}</code></p></main></body></html>";
    }

    private static SeverityLevel GetSeverityLevel(Exception exception)
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

    private static string CategorizeException(Exception exception)
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

    private static string GetClientIpAddress(HttpContext context)
    {
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
