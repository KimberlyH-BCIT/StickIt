using Microsoft.AspNetCore.Mvc;
using ELKH.ViewModels;
using System.Security.Claims;
using ELKH.Data;
using ELKH.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using ELKH.Models;
using ELKH.Services;
using Microsoft.Extensions.Options;

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
public class CheckoutController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICartRepo _cartRepo;
    private readonly PayPalOptions _pp;

    public CheckoutController(ApplicationDbContext db, ICartRepo cartRepo, IOptions<PayPalOptions> pp)
    {
        _db = db;
        _cartRepo = cartRepo;
        _pp = pp.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        List<CartModel> cartItems;

        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(email))
        {
            cartItems = new List<CartModel>();
        }
        else
        {
            var regUser = await _db.RegisteredUsers.FirstOrDefaultAsync(u => u.Email == email);
            if (regUser == null)
            {
                cartItems = new List<CartModel>();
            }
            else
            {
                cartItems = (await _cartRepo.GetByUserIdAsync(regUser.PkRegisteredUserId)).ToList();
            }
        }

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

        // PayPal client id 
        ViewBag.PayPalClientId = _pp.ClientId;

        return View(checkoutVM);
    }

    // POST: Shipping form, A PayPal approve+capture
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Finalize(CheckoutVM vm, string paypalOrderId)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.PayPalClientId = _pp.ClientId;
            return View("Index", vm);
        }

        if (string.IsNullOrWhiteSpace(paypalOrderId))
        {
            ModelState.AddModelError("", "PayPal payment is required.");
            ViewBag.PayPalClientId = _pp.ClientId;
            return View("Index", vm);
        }

        return RedirectToAction(nameof(Complete), new { orderId = paypalOrderId });
    }

    [HttpGet]
    public IActionResult Complete(string orderId)
    {
        ViewBag.OrderId = string.IsNullOrWhiteSpace(orderId) ? "ORDER-12345-MOCK" : orderId;
        return View();
    }
}