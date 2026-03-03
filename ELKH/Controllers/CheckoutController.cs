using Microsoft.AspNetCore.Mvc;
using ELKH.ViewModels;
using System.Collections.Generic;
using System.Security.Claims;
using ELKH.Data;
using ELKH.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ELKH.Controllers;

public class CheckoutController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICartRepo _cartRepo;

    public CheckoutController(ApplicationDbContext db, ICartRepo cartRepo)
    {
        _db = db;
        _cartRepo = cartRepo;
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
        checkoutVM.Tax = checkoutVM.Subtotal * 0.12m;
        checkoutVM.ShippingCost = checkoutVM.Subtotal >= 50m ? 0m : 7.99m;
        checkoutVM.Total = checkoutVM.Subtotal + checkoutVM.Tax + checkoutVM.ShippingCost;

        return View(checkoutVM);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ProcessPayment(CheckoutVM vm)
    {
        if (!ModelState.IsValid) return View("Index", vm);

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
        ViewBag.OrderId = "ORDER-12345-MOCK";
        return View();
    }
}