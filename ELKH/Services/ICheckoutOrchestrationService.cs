using ELKH.ViewModels;

namespace ELKH.Services;

/// <summary>
/// Coordinates checkout view-model building and payment processing workflows.
/// </summary>
public interface ICheckoutOrchestrationService
{
    Task<CheckoutVM?> BuildCheckoutAsync(string email, CancellationToken ct = default);

    Task PopulateCheckoutAsync(CheckoutVM model, string email, CancellationToken ct = default);

    Task<CheckoutProcessResult> ProcessPaymentAsync(string email, CheckoutVM vm, string expectedCurrency, CancellationToken ct = default);

    Task<GuestCheckoutProcessResult> ProcessGuestPaymentAsync(GuestCheckoutVM vm, string expectedCurrency, string requestScheme, string requestHost, CancellationToken ct = default);

    Task<OrderModel?> GetGuestOrderByAccessTokenAsync(string token, CancellationToken ct = default);
}

public sealed record CheckoutProcessResult(bool Success, int? OrderId, string? ErrorMessage)
{
    public static CheckoutProcessResult Ok(int orderId) => new(true, orderId, null);

    public static CheckoutProcessResult Fail(string errorMessage) => new(false, null, errorMessage);
}

public sealed record GuestCheckoutProcessResult(bool Success, int? OrderId, string? GuestAccessToken, string? ErrorMessage)
{
    public static GuestCheckoutProcessResult Ok(int orderId, string guestAccessToken) => new(true, orderId, guestAccessToken, null);

    public static GuestCheckoutProcessResult Fail(string errorMessage) => new(false, null, null, errorMessage);
}
