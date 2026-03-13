using System.Linq;
using System.Threading.Tasks;
using ELKH.Models;
using ELKH.Services;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers;

/// <summary>
/// Shopping cart management controller for authenticated users.
/// Handles cart operations (add, remove, checkout) and order placement with inventory management.
/// </summary>
public class CartController : AuthenticatedControllerBase
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService, IUserService userService)
        : base(userService)
    {
        _cartService = cartService;
    }

    // GET: /Cart
    public async Task<IActionResult> Index()
    {
        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return authResult;

        var cartItems = await _cartService.GetCartItemsAsync(email);

        var vm = new CartVM
        {
            Items = cartItems.Select(c => new CartItemVM
            {
                CartItemId = c.PkCartId,
                ProductName = c.Product?.Name ?? string.Empty,
                ImageUrl = c.Product?.ProductImages?.FirstOrDefault()?.ProductImageURL ?? string.Empty,
                UnitPrice = c.Product?.Price ?? 0,
                Quantity = c.Quantity,
                LineTotal = c.TotalPrice
            }).ToList()
        };

        return View(vm);
    }

    // POST: /Cart/AddToCart
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int itemId, int quantity)
    {
        if (quantity <= 0) return BadRequest("Quantity must be positive.");

        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return authResult;

        await _cartService.AddToCartAsync(email, itemId, quantity);
        return RedirectToAction(nameof(Index));
    }

    // POST: /Cart/BuyNow
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BuyNow(int itemId, int quantity)
    {
        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return authResult;

        var orderId = await _cartService.BuyNowAsync(email, itemId, quantity);
        switch (orderId)
        {
            case -2:
                SetWarningMessage("Please add a delivery address before purchasing.");
                return RedirectToAction("Addresses", "User");
            case -1:
                SetWarningMessage("This item is currently out of stock.");
                return RedirectToAction("Details", "Product", new { id = itemId });
            case 0:
                SetWarningMessage("Unable to complete purchase. Please try again.");
                return RedirectToAction("Details", "Product", new { id = itemId });
            default:
                SetSuccessMessage("Order placed successfully!");
                return RedirectToAction("Details", "Order", new { id = orderId });
        }
    }

    // POST: /Cart/RemoveFromCart
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFromCart(int cartId)
    {
        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return authResult;

        await _cartService.RemoveFromCartAsync(email, cartId);
        return RedirectToAction(nameof(Index));
    }

    // GET: /Cart/Checkout
    public async Task<IActionResult> Checkout()
    {
        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return authResult;

        var items = await _cartService.GetCartItemsAsync(email);
        var total = items.Sum(i => i.TotalPrice);
        ViewBag.Total = total;
        ViewBag.Items = items;
        return View();
    }

    // POST: /Cart/PlaceOrder
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder()
    {
        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return authResult;

        var orderId = await _cartService.PlaceOrderAsync(email);
        switch (orderId)
        {
            case -2:
                SetWarningMessage("Please add a delivery address to your account before placing an order.");
                return RedirectToAction("Addresses", "User");
            case -1:
                SetWarningMessage("One or more items in your cart are out of stock. Please update your cart.");
                return RedirectToAction(nameof(Index));
            case 0:
                return RedirectToAction(nameof(Index));
            default:
                SetSuccessMessage("Order placed successfully");
                return RedirectToAction("Details", "Order", new { id = orderId });
        }
    }
}