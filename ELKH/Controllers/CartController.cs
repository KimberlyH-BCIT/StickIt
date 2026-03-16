using System.Linq;
using System.Threading.Tasks;
using ELKH.Services;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers;

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

        var items = cartItems.Select(c =>
        {
            var qty = c.Quantity;
            var unit = c.Product?.Price ?? 0m;          // use product price for display
            var line = unit * qty;                      // compute line total consistently

            return new CartItemVM
            {
                CartItemId = c.PkCartId,
                ProductId = c.FkProductID,              // if your CartModel field name differs, adjust
                ProductName = c.Product?.Name ?? "",
                ImageUrl = c.Product?.ProductImages?.FirstOrDefault()?.ProductImageURL,
                UnitPrice = unit,
                Quantity = qty,
                LineTotal = line
            };
        }).ToList();

        var vm = new CartVM
        {
            Items = items
        };

        // Subtotal is computed by vm.Subtotal (read-only)
        vm.Tax = vm.Subtotal * 0.12m;
        vm.ShippingCost = vm.Subtotal >= 50m ? 0m : 7.99m;
        vm.Total = vm.Subtotal + vm.Tax + vm.ShippingCost;

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

    // POST: /Cart/Update
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int cartId, int quantity)
    {
        if (quantity < 1) quantity = 1;

        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return authResult;

        await _cartService.UpdateQuantityAsync(email, cartId, quantity);
        return RedirectToAction(nameof(Index));
    }

    // POST: /Cart/Remove  (matches your view asp-action="Remove")
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int cartId)
    {
        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return authResult;

        await _cartService.RemoveFromCartAsync(email, cartId);
        return RedirectToAction(nameof(Index));
    }

    // POST: /Cart/Clear  (matches your view asp-action="Clear")
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear()
    {
        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return authResult;

        await _cartService.ClearCartAsync(email);
        SetSuccessMessage("Cart cleared.");
        return RedirectToAction(nameof(Index));
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