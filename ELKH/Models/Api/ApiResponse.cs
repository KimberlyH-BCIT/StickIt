using System.Text.Json.Serialization;

namespace ELKH.Models.Api;

/// <summary>
/// Generic API response wrapper for successful operations.
/// Provides consistent response structure across all API endpoints.
/// </summary>
/// <typeparam name="T">The type of data being returned</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// Indicates if the operation was successful.
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// The response data.
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Optional message providing additional context.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Timestamp when the response was generated.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
