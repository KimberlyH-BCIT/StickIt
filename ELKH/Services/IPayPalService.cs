namespace ELKH.Services;

/// <summary>
/// Abstraction over PayPal REST API operations.
/// Decouples controllers from the concrete HTTP implementation so payment
/// calls can be mocked in unit tests without network access.
/// </summary>
public interface IPayPalService
{
    /// <summary>Creates a PayPal order and returns its PayPal-assigned ID.</summary>
    Task<string> CreateOrderAsync(decimal total, string currency);

    /// <summary>Captures (completes) a previously created PayPal order.</summary>
    Task CaptureOrderAsync(string paypalOrderId);

    /// <summary>
    /// Verifies a PayPal checkout order and returns the captured payment details used to trust the payment server-side.
    /// </summary>
    Task<PayPalVerificationResult> VerifyCapturedOrderAsync(string paypalOrderId, decimal expectedAmount, string expectedCurrency);
}

/// <summary>
/// Normalized PayPal verification details used by checkout to trust and persist a payment.
/// </summary>
public sealed class PayPalVerificationResult
{
    public string PayPalOrderId { get; init; } = string.Empty;
    public string CaptureId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string? MerchantId { get; init; }
    public string? MerchantEmail { get; init; }
    public DateTime? CapturedAtUtc { get; init; }
    public string? PayerId { get; init; }
    public string? PayerEmail { get; init; }
    public string VerificationSummaryJson { get; init; } = string.Empty;
}
