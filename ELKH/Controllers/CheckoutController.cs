using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ELKH.Configuration;
using ELKH.Extensions;
using ELKH.Models;
using ELKH.Services;
using ELKH.ViewModels;
using System.Security.Claims;
using ELKH.Data;
using ELKH.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static ELKH.Extensions.RateLimitPolicies;

namespace ELKH.Controllers;

/// <summary>
/// Handles the multi-step checkout flow: cart summary display, order pricing, and
/// PayPal payment processing.
/// </summary>
/// <remarks>
/// TABLE OF CONTENTS
/// ================================================================================
/// 1. Fields &amp; Constructor
/// 2. Checkout display
///    - Index()               // GET: Build checkout VM with live pricing
/// 3. Payment processing
///    - ProcessPayment()      // POST: Validate pricing and process PayPal payment
///    - Complete()            // GET:  Order confirmation page with PayPal order ID
/// ================================================================================
///
/// Pricing rules applied in <see cref="Index"/> and re-verified server-side in
/// <see cref="ProcessPayment"/> to prevent client-side tampering:
/// - Tax:      12 % of subtotal (BC PST + GST composite rate).
/// - Shipping: $7.99 flat rate; waived when the subtotal reaches $50.00 or more.
/// </remarks>
[Authorize]
public class CheckoutController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICartRepo _cartRepo;
    private readonly IPayPalService _paypal;
    private readonly IOrderEmailService _orderEmail;
    private readonly ILogger<CheckoutController> _logger;
    private readonly string _currency;

    public CheckoutController(
        ApplicationDbContext db,
        ICartRepo cartRepo,
        IPayPalService paypal,
        IOrderEmailService orderEmail,
        ILogger<CheckoutController> logger,
        IOptions<PayPalOptions> paypalOpts)
    {
        _db         = db;
        _cartRepo   = cartRepo;
        _paypal     = paypal;
        _orderEmail = orderEmail;
        _logger     = logger;
        _currency   = paypalOpts.Value.Currency;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
            return RedirectToPage("/Account/Login", new { area = "Identity" });

        var regUser = await _db.RegisteredUsers.FirstOrDefaultAsync(u => u.Email == email);
        if (regUser == null)
            return RedirectToAction("Index", "Cart");

        var cartItems = await _cartRepo.GetByUserIdAsync(regUser.PkRegisteredUserId);

        var checkoutVM = new CheckoutVM
        {
            Items = cartItems.Select(c => new CartItemVM
            {
                ProductName = c.Product?.Name ?? "",
                Quantity    = c.Quantity,
                UnitPrice   = c.Product?.Price ?? 0m,
                LineTotal   = (c.Product?.Price ?? 0m) * c.Quantity
            }).ToList()
        };

        checkoutVM.Subtotal     = checkoutVM.Items.Sum(i => i.LineTotal);
        checkoutVM.Tax          = checkoutVM.Subtotal * 0.12m;
        checkoutVM.ShippingCost = checkoutVM.Subtotal >= 50m ? 0m : 7.99m;
        checkoutVM.Total        = checkoutVM.Subtotal + checkoutVM.Tax + checkoutVM.ShippingCost;

        return View(checkoutVM);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimitPolicies.Checkout)]
    public async Task<IActionResult> ProcessPayment(CheckoutVM vm)
    {
        if (!ModelState.IsValid) return View("Index", vm);

        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
            return RedirectToPage("/Account/Login", new { area = "Identity" });

        var regUser = await _db.RegisteredUsers.FirstOrDefaultAsync(u => u.Email == email);
        if (regUser is null) return RedirectToAction("Index", "Cart");

        var cartItems = (await _cartRepo.GetByUserIdAsync(regUser.PkRegisteredUserId)).ToList();
        if (!cartItems.Any()) return RedirectToAction("Index", "Cart");

        // Recalculate server-side to prevent client-side price tampering.
        var subtotal = cartItems.Sum(c => (c.Product?.Price ?? 0m) * c.Quantity);
        var tax      = subtotal * 0.12m;
        var shipping = subtotal >= 50m ? 0m : 7.99m;
        var total    = subtotal + tax + shipping;

        // ── 1. Process PayPal payment ────────────────────────────────────────
        string paypalOrderId;
        try
        {
            paypalOrderId = await _paypal.CreateOrderAsync(total, _currency);
            await _paypal.CaptureOrderAsync(paypalOrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayPal payment failed for {Email}", email);
            ModelState.AddModelError("", "Payment failed. Please try again.");
            return View("Index", vm);
        }

        // ── 2. Record the order — payment is already captured so we must not
        //       lose the order. On failure we log the PayPal reference and tell
        //       the customer to contact support with it.
        var contact = await _db.ContactDetails
            .FirstOrDefaultAsync(c => c.FkRegisteredUserId == regUser.PkRegisteredUserId && c.IsDefault);
        int contactId = contact?.PkContactId ?? 0;

        var productIds = cartItems.Select(c => c.FkProductID).ToList();
        var products   = await _db.Product
            .Where(p => productIds.Contains(p.PkProductId))
            .ToDictionaryAsync(p => p.PkProductId);

        int orderId;
        await using var dbTx = await _db.Database.BeginTransactionAsync();
        try
        {
            var order = new OrderModel
            {
                OrderStatus        = "Placed",
                TotalAmount        = total,
                CreatedAt          = DateTime.UtcNow,
                DeliveryStatus     = "Pending",
                FkRegisteredUserId = regUser.PkRegisteredUserId,
                FkContactId        = contactId
            };
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            foreach (var item in cartItems)
            {
                if (!products.TryGetValue(item.FkProductID, out var product)) continue;
                _db.OrderItems.Add(new OrderItemModel
                {
                    FkOrderId   = order.PkOrderId,
                    FkProductId = item.FkProductID,
                    Quantity    = item.Quantity,
                    UnitPrice   = product.GetEffectivePrice()
                });
                product.StockQuantity = Math.Max(0, (product.StockQuantity ?? 0) - item.Quantity);
            }

            _db.Transactions.Add(new TransactionModel
            {
                FkOrderId         = order.PkOrderId,
                FkContactId       = contactId,
                Amount            = total,
                DeliveryFee       = shipping,
                TransactionDate   = DateTime.UtcNow,
                TransactionStatus = "Completed"
            });

            var cartRows = await _db.Carts
                .Where(c => c.FkRegisteredUserId == regUser.PkRegisteredUserId)
                .ToListAsync();
            _db.Carts.RemoveRange(cartRows);

            await _db.SaveChangesAsync();
            await dbTx.CommitAsync();
            orderId = order.PkOrderId;
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync();
            _logger.LogError(ex,
                "Order creation failed after PayPal capture {PayPalId} for {Email}",
                paypalOrderId, email);
            TempData["Error"] = $"Payment was processed (ref: {paypalOrderId}) but your order " +
                                 "could not be recorded. Please contact support with this reference.";
            return RedirectToAction("Index", "Cart");
        }

        // ── 3. Confirmation email — non-fatal ────────────────────────────────
        try
        {
            await _orderEmail.SendOrderConfirmationAsync(
                email, contact?.FirstName ?? "Customer", orderId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Order confirmation email failed for order {OrderId}", orderId);
        }

        return RedirectToAction(nameof(Complete), new { orderId });
    }

    public IActionResult Complete(int orderId)
    {
        ViewBag.OrderId = orderId;
        return View();
    }
}