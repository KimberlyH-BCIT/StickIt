using ELKH.Configuration;
using ELKH.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ELKH.Controllers;

/// <summary>
/// API controller for PayPal payment processing integration.
/// Handles order creation and payment capture for the ELKH e-commerce platform.
/// </summary>
/// <remarks>
/// This controller provides RESTful API endpoints for PayPal payment workflow:
/// 1. Create payment order with specified amount
/// 2. Capture payment after user approval
/// 
/// <para><strong>Security Implementation:</strong></para>
/// <list type="bullet">
/// <item>[Authorize] - Requires user authentication for all endpoints</item>
/// <item>[ValidateAntiForgeryToken] - CSRF protection for state-changing operations</item>
/// <item>API controller design for clean JSON request/response handling</item>
/// <item>Secure PayPal service integration with proper credential management</item>
/// </list>
/// 
/// <para><strong>PayPal Integration Flow:</strong></para>
/// <list type="number">
/// <item>Frontend calls /paypal/create-order with cart total</item>
/// <item>Controller creates PayPal order and returns order ID</item>
/// <item>User completes payment on PayPal's secure checkout page</item>
/// <item>Frontend calls /paypal/capture-order with PayPal order ID</item>
/// <item>Controller captures payment and confirms transaction</item>
/// </list>
/// 
/// <para><strong>Error Handling:</strong></para>
/// PayPal API errors are handled by the IPayPalService implementation
/// and propagated as appropriate HTTP status codes with error details.
/// 
/// <para><strong>Configuration Dependencies:</strong></para>
/// Requires PayPalOptions configuration with ClientId, Secret, Environment,
/// and Currency settings properly configured via secure providers.
/// </remarks>
[Authorize]
[ApiController]
[Route("paypal")]
public class PayPalController : ControllerBase
{
    private readonly IPayPalService  _payPal;
    private readonly PayPalOptions  _opts;

    /// <summary>
    /// Initializes a new instance of the PayPalController.
    /// </summary>
    /// <param name="payPal">PayPal service for payment processing operations.</param>
    /// <param name="opts">PayPal configuration options.</param>
    public PayPalController(IPayPalService payPal, IOptions<PayPalOptions> opts)
    {
        _payPal = payPal;
        _opts   = opts.Value;
    }

    /// <summary>
    /// Request model for creating a PayPal payment order.
    /// </summary>
    /// <param name="Total">The total amount to charge for the order.</param>
    public record CreateOrderRequest(decimal Total);

    /// <summary>
    /// Request model for capturing a PayPal payment order.
    /// </summary>
    /// <param name="OrderId">The PayPal order ID to capture payment for.</param>
    public record CaptureOrderRequest(string OrderId);

    /// <summary>
    /// Creates a new PayPal payment order for the specified amount.
    /// </summary>
    /// <param name="req">Request containing the order total amount.</param>
    /// <returns>JSON response with PayPal order ID for frontend integration.</returns>
    /// <remarks>
    /// This endpoint initiates the PayPal payment flow by creating an order
    /// with the specified total amount. The returned order ID is used by
    /// the frontend PayPal SDK to present the payment interface to the user.
    /// 
    /// <para><strong>Request Example:</strong></para>
    /// <code>
    /// POST /paypal/create-order
    /// Content-Type: application/json
    /// {
    ///   "total": 25.99
    /// }
    /// </code>
    /// 
    /// <para><strong>Response Example:</strong></para>
    /// <code>
    /// {
    ///   "id": "8XY12345ABC67890D"
    /// }
    /// </code>
    /// </remarks>
    [HttpPost("create-order")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest req)
    {
        var id = await _payPal.CreateOrderAsync(req.Total, _opts.Currency);
        return Ok(new { id });
    }

    /// <summary>
    /// Captures payment for an existing PayPal order after user approval.
    /// </summary>
    /// <param name="req">Request containing the PayPal order ID to capture.</param>
    /// <returns>JSON response confirming successful payment capture.</returns>
    /// <remarks>
    /// This endpoint completes the PayPal payment flow by capturing the payment
    /// for an order that has been approved by the user. This should only be
    /// called after the user has successfully completed PayPal's checkout flow.
    /// 
    /// <para><strong>Request Example:</strong></para>
    /// <code>
    /// POST /paypal/capture-order
    /// Content-Type: application/json
    /// {
    ///   "orderId": "8XY12345ABC67890D"
    /// }
    /// </code>
    /// 
    /// <para><strong>Response Example:</strong></para>
    /// <code>
    /// {
    ///   "ok": true
    /// }
    /// </code>
    /// 
    /// <para><strong>Error Handling:</strong></para>
    /// If payment capture fails (e.g., insufficient funds, cancelled payment),
    /// the PayPal service will throw an appropriate exception that will be
    /// converted to an HTTP error response.
    /// </remarks>
    [HttpPost("capture-order")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CaptureOrder([FromBody] CaptureOrderRequest req)
    {
        await _payPal.CaptureOrderAsync(req.OrderId);
        return Ok(new { ok = true });
    }
}
