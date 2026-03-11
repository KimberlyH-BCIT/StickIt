using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ELKH.ViewModels;
using System.Collections.Generic;
using System.Security.Claims;
using ELKH.Data;
using ELKH.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ELKH.Controllers;

/// <summary>
/// Handles the multi-step checkout flow: cart summary display, order pricing, and
/// (currently stubbed) payment processing.
/// </summary>
/// <remarks>
/// TABLE OF CONTENTS
/// ================================================================================
/// 1. Fields &amp; Constructor
/// 2. Checkout display
///    - Index()               // GET: Build checkout VM with live pricing
/// 3. Payment processing
///    - ProcessPayment()      // POST: Validate and process payment (stub)
///    - Complete()            // GET:  Order confirmation page (stub)
/// ================================================================================
///
/// Pricing rules applied in <see cref="Index"/>:
/// - Tax:      12 % of subtotal (BC PST + GST composite rate).
/// - Shipping: $7.99 flat rate; waived when the subtotal reaches $50.00 or more.
///
/// STUB NOTICE: Payment processing is not yet integrated with a payment gateway.
/// <see cref="ProcessPayment"/> always succeeds; <see cref="Complete"/> returns a
/// hard-coded mock order reference. Both methods must be replaced before go-live.
/// </remarks>
[Authorize]
public class CheckoutController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICartRepo _cartRepo;
    private readonly IWebHostEnvironment _env;

    public CheckoutController(ApplicationDbContext db, ICartRepo cartRepo, IWebHostEnvironment env)
    {
        _db = db;
        _cartRepo = cartRepo;
        _env = env;
    }

    // Guard: payment flow is a stub — never serve outside Development.
    // Returns a 503 result if not in Development, null otherwise.
    // Remove this method (and all call sites) once a real payment gateway is wired up.
    private IActionResult? StubGuard() =>
        _env.IsDevelopment()
            ? null
            : StatusCode(503, "Checkout is not available in this environment. " +
                              "A real payment gateway must be integrated before go-live.");

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (StubGuard() is { } guard) return guard;

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
    public IActionResult ProcessPayment(CheckoutVM vm)
    {
        if (StubGuard() is { } guard) return guard;
        if (!ModelState.IsValid) return View("Index", vm);

        // STUB: Payment gateway integration is not yet implemented.
        // Replace this flag with a real payment provider call (e.g. Stripe, PayPal)
        // that returns success/failure before this goes to production.
        bool paymentSuccess = true;

        if (paymentSuccess)
        {
            return RedirectToAction(nameof(Complete));
        }
        else
        {
            ModelState.AddModelError("", "Payment failed.");
            return View("Index", vm);
        }
    }

    public IActionResult Complete()
    {
        if (StubGuard() is { } guard) return guard;

        // STUB: Replace with the real order ID returned by the payment/order pipeline.
        ViewBag.OrderId = "ORDER-12345-MOCK";
        return View();
    }
}