using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ELKH.Configuration;
using ELKH.Services;
using ELKH.ViewModels;
using System.Collections.Generic;
using System.Security.Claims;
using ELKH.Data;
using ELKH.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Linq;

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
    private readonly PayPalService _paypal;
    private readonly string _currency;

    public CheckoutController(ApplicationDbContext db, ICartRepo cartRepo, PayPalService paypal, IOptions<PayPalOptions> paypalOpts)
    {
        _db = db;
        _cartRepo = cartRepo;
        _paypal = paypal;
        _currency = paypalOpts.Value.Currency;
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
                Quantity = c.Quantity,
                UnitPrice = c.Product?.Price ?? 0m,
                LineTotal = (c.Product?.Price ?? 0m) * c.Quantity
            }).ToList()
        };

        checkoutVM.Subtotal = checkoutVM.Items.Sum(i => i.LineTotal);

        // 12 % composite tax rate (BC PST 7 % + GST 5 %).
        checkoutVM.Tax = checkoutVM.Subtotal * 0.12m;

        // Free shipping threshold: orders of $50.00 or more ship free; otherwise a flat $7.99 fee.
        checkoutVM.ShippingCost = checkoutVM.Subtotal >= 50m ? 0m : 7.99m;

        checkoutVM.Total = checkoutVM.Subtotal + checkoutVM.Tax + checkoutVM.ShippingCost;

        return View(checkoutVM);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessPayment(CheckoutVM vm)
    {
        if (!ModelState.IsValid) return View("Index", vm);

        // Recalculate total server-side to prevent client-side price tampering.
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
        var regUser = email is not null
            ? await _db.RegisteredUsers.FirstOrDefaultAsync(u => u.Email == email)
            : null;
        if (regUser is null) return RedirectToAction("Index", "Cart");

        var cartItems = await _cartRepo.GetByUserIdAsync(regUser.PkRegisteredUserId);
        var subtotal  = cartItems.Sum(c => (c.Product?.Price ?? 0m) * c.Quantity);
        var tax       = subtotal * 0.12m;
        var shipping  = subtotal >= 50m ? 0m : 7.99m;
        var total     = subtotal + tax + shipping;

        try
        {
            var paypalOrderId = await _paypal.CreateOrderAsync(total, _currency);
            await _paypal.CaptureOrderAsync(paypalOrderId);
            return RedirectToAction(nameof(Complete), new { orderId = paypalOrderId });
        }
        catch
        {
            ModelState.AddModelError("", "Payment failed. Please try again.");
            return View("Index", vm);
        }
    }

    public IActionResult Complete(string? orderId)
    {
        ViewBag.OrderId = orderId ?? "N/A";
        return View();
    }
}