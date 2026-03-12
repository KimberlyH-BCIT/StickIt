using ELKH.Configuration;
using ELKH.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ELKH.Controllers;

[Authorize]
[ApiController]
[Route("paypal")]
public class PayPalController : ControllerBase
{
    private readonly PayPalService  _payPal;
    private readonly PayPalOptions  _opts;

    public PayPalController(PayPalService payPal, IOptions<PayPalOptions> opts)
    {
        _payPal = payPal;
        _opts   = opts.Value;
    }

    public record CreateOrderRequest(decimal Total);
    public record CaptureOrderRequest(string OrderId);

    [HttpPost("create-order")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest req)
    {
        var id = await _payPal.CreateOrderAsync(req.Total, _opts.Currency);
        return Ok(new { id });
    }

    [HttpPost("capture-order")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CaptureOrder([FromBody] CaptureOrderRequest req)
    {
        await _payPal.CaptureOrderAsync(req.OrderId);
        return Ok(new { ok = true });
    }
}
