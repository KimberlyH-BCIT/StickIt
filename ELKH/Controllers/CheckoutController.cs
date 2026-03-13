using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ELKH.ViewModels;
using ELKH.Data;
using ELKH.Services;
using System.Security.Claims;
using System.Linq;

namespace ELKH.Controllers;

/// <summary>
/// Handles the multi-step checkout flow: cart summary display, order pricing, and
/// (currently stubbed) payment processing.
/// </summary>
public class CheckoutController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICartService _cartService;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public CheckoutController(
        ApplicationDbContext db,
        ICartService cartService,
        IWebHostEnvironment env,
        IConfiguration configuration)
    {
        _db = db;
        _cartService = cartService;
        _env = env;
        _configuration = configuration;

        if (!_env.IsDevelopment())
            throw new InvalidOperationException(
                "CheckoutController: payment processing is not integrated. " +
                "Wire up a real payment gateway before deploying outside Development.");
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

        var cartItems = await _cartService.GetCartItemsAsync(email);

        var checkoutVM = new CheckoutVM
        {
            Items = cartItems.Select(c => new CartItemVM
            {
                CartItemId = c.PkCartId,
                ProductName = c.Product?.Name ?? string.Empty,
                ImageUrl = c.Product?.ProductImages?.FirstOrDefault()?.ProductImageURL ?? string.Empty,
                Quantity = c.Quantity,
                UnitPrice = c.Product?.Price ?? 0m,
                LineTotal = c.TotalPrice
            }).ToList()
        };

        checkoutVM.Subtotal = checkoutVM.Items.Sum(i => i.LineTotal);
        checkoutVM.Tax = checkoutVM.Subtotal * 0.12m;
        checkoutVM.ShippingCost = checkoutVM.Subtotal >= 50m ? 0m : 7.99m;
        checkoutVM.Total = checkoutVM.Subtotal + checkoutVM.Tax + checkoutVM.ShippingCost;

        ViewBag.PayPalClientId = _configuration["PayPal:ClientId"];

        return View(checkoutVM);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ProcessPayment(CheckoutVM vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.PayPalClientId = _configuration["PayPal:ClientId"];
            return View("Index", vm);
        }

        bool paymentSuccess = true;

        if (paymentSuccess)
        {
            return RedirectToAction(nameof(Complete));
        }

        ModelState.AddModelError(string.Empty, "Payment failed.");
        ViewBag.PayPalClientId = _configuration["PayPal:ClientId"];
        return View("Index", vm);
    }

    public IActionResult Complete()
    {
        ViewBag.OrderId = "ORDER-12345";
        return View();
    }
}