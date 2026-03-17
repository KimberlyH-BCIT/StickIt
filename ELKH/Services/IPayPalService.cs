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
}
