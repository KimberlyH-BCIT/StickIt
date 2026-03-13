namespace ELKH.Services;

/// <summary>
/// Abstraction over PayPal REST API operations.
/// Decouples controllers from the concrete HTTP implementation so payment
/// calls can be mocked in unit tests without network access.
/// </summary>
public interface IPayPalService
{
    /// <summary>
    /// Creates a PayPal order and returns its PayPal-assigned ID.
    /// </summary>
    /// <param name="total">The order total to charge.</param>
    /// <param name="currency">ISO 4217 currency code (e.g. "CAD").</param>
    /// <param name="idempotencyKey">
    /// Optional caller-supplied <c>PayPal-Request-Id</c>. When provided, PayPal
    /// deduplicates requests with the same key within a short window, preventing
    /// double-charges on network retries. Generate once per checkout attempt and
    /// reuse on retry. A new GUID is generated automatically when omitted.
    /// </param>
    Task<string> CreateOrderAsync(decimal total, string currency, string? idempotencyKey = null);

    /// <summary>Captures (completes) a previously created PayPal order.</summary>
    Task CaptureOrderAsync(string paypalOrderId);
}
