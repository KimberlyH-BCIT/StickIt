using System;
using System.Collections.Generic;
using System.Linq;
using ELKH.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ELKH.Services;

// TABLE OF CONTENTS
// - User action logging
// - System event logging
// - Performance metrics
// - Business events
// - Error logging

/// <summary>
/// Interface for structured logging service with correlation ID support
/// </summary>
public interface IStructuredLoggingService
{
    /// <summary>
    /// Logs a user action with structured data and correlation ID
    /// </summary>
    void LogUserAction(string action, string userId, object? additionalData = null);

    /// <summary>
    /// Logs a system event with structured data
    /// </summary>
    void LogSystemEvent(string eventType, string description, object? additionalData = null);

    /// <summary>
    /// Logs a performance metric with timing information
    /// </summary>
    void LogPerformanceMetric(string operation, TimeSpan duration, object? additionalData = null);

    /// <summary>
    /// Logs a business event with rich context
    /// </summary>
    void LogBusinessEvent(string eventName, string category, object? data = null);

    /// <summary>
    /// Logs an error with full context and correlation ID
    /// </summary>
    void LogError(Exception exception, string context, object? additionalData = null);
}

/// <summary>
/// Service for structured logging with correlation IDs and rich context
/// Provides consistent logging patterns across the application
/// </summary>
public class StructuredLoggingService : IStructuredLoggingService
{
    private readonly ILogger<StructuredLoggingService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public StructuredLoggingService(
        ILogger<StructuredLoggingService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public void LogUserAction(string action, string userId, object? additionalData = null)
    {
        var correlationId = _httpContextAccessor.GetCorrelationId();
        var context = _httpContextAccessor.HttpContext;

        var logData = new Dictionary<string, object>
        {
            ["Action"] = action,
            ["UserId"] = userId,
            ["Timestamp"] = DateTimeOffset.UtcNow,
            ["CorrelationId"] = correlationId ?? "Unknown",
            ["UserAgent"] = context?.Request.Headers.UserAgent.FirstOrDefault() ?? "Unknown",
            ["IpAddress"] = context?.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
        };

        if (additionalData != null)
        {
            logData["AdditionalData"] = additionalData;
        }

        using var scope = _logger.BeginScope(logData);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User action: {Action} by user {UserId}",
                action, userId);
        }
    }

    public void LogSystemEvent(string eventType, string description, object? additionalData = null)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            var correlationId = _httpContextAccessor.GetCorrelationId();

            var logData = new Dictionary<string, object>
            {
                ["EventType"] = eventType,
                ["Description"] = description,
                ["Timestamp"] = DateTimeOffset.UtcNow,
                ["CorrelationId"] = correlationId ?? "System",
                ["MachineName"] = Environment.MachineName
            };

            if (additionalData != null)
            {
                logData["AdditionalData"] = additionalData;
            }

            using var scope = _logger.BeginScope(logData);
            _logger.LogInformation("System event: {EventType} - {Description}",
                eventType, description);
        }
    }

    public void LogPerformanceMetric(string operation, TimeSpan duration, object? additionalData = null)
    {
        var logLevel = duration.TotalMilliseconds > 5000 ? LogLevel.Warning : LogLevel.Information;

        if (_logger.IsEnabled(logLevel))
        {
            var correlationId = _httpContextAccessor.GetCorrelationId();

            var logData = new Dictionary<string, object>
            {
                ["Operation"] = operation,
                ["DurationMs"] = duration.TotalMilliseconds,
                ["Timestamp"] = DateTimeOffset.UtcNow,
                ["CorrelationId"] = correlationId ?? "Unknown"
            };

            if (additionalData != null)
            {
                logData["AdditionalData"] = additionalData;
            }

            using var scope = _logger.BeginScope(logData);
            _logger.Log(logLevel, "Performance: {Operation} completed in {DurationMs}ms",
                operation, duration.TotalMilliseconds);
        }
    }

    public void LogBusinessEvent(string eventName, string category, object? data = null)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            var correlationId = _httpContextAccessor.GetCorrelationId();
            var context = _httpContextAccessor.HttpContext;

            var logData = new Dictionary<string, object>
            {
                ["EventName"] = eventName,
                ["Category"] = category,
                ["Timestamp"] = DateTimeOffset.UtcNow,
                ["CorrelationId"] = correlationId ?? "Unknown",
                ["UserId"] = context?.User?.Identity?.Name ?? "Anonymous"
            };

            if (data != null)
            {
                logData["EventData"] = data;
            }

            using var scope = _logger.BeginScope(logData);
            _logger.LogInformation("Business event: {Category}.{EventName}",
                category, eventName);
        }
    }

    public void LogError(Exception exception, string context, object? additionalData = null)
    {
        if (_logger.IsEnabled(LogLevel.Error))
        {
            var correlationId = _httpContextAccessor.GetCorrelationId();
            var httpContext = _httpContextAccessor.HttpContext;

            var logData = new Dictionary<string, object>
            {
                ["Context"] = context,
                ["ExceptionType"] = exception.GetType().Name,
                ["Timestamp"] = DateTimeOffset.UtcNow,
                ["CorrelationId"] = correlationId ?? "Unknown",
                ["RequestPath"] = httpContext?.Request.Path.Value ?? "Unknown",
                ["UserId"] = httpContext?.User?.Identity?.Name ?? "Anonymous"
            };

            if (additionalData != null)
            {
                logData["AdditionalData"] = additionalData;
            }

            using var scope = _logger.BeginScope(logData);
            _logger.LogError(exception, "Error in {Context}: {ExceptionMessage}",
                context, exception.Message);
        }
    }
}
