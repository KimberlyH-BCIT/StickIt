using System.Text.Json.Serialization;

namespace ELKH.Models.Api;

/// <summary>
/// API response for error scenarios.
/// Provides structured error information for API consumers.
/// </summary>
public class ApiErrorResponse
{
    /// <summary>
    /// Indicates if the operation was successful (always false for errors).
    /// </summary>
    public bool Success { get; set; } = false;

    /// <summary>
    /// Error message describing what went wrong.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional error code for programmatic handling.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Optional additional error details.
    /// </summary>
    public object? Details { get; set; }

    /// <summary>
    /// Timestamp when the error occurred.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
