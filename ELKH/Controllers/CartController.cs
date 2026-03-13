using System.Security.Claims;
using ELKH.Data;
using ELKH.Extensions;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.Services;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

namespace ELKH.Controllers;

/// <summary>
/// Shopping cart management controller for authenticated users.
/// Handles cart operations (add, remove) and quick purchase functionality.
/// </summary>
/// <remarks>
/// All endpoints require authentication (inherited from AuthenticatedControllerBase).
///
/// Cart operations:
/// - GET /Cart - Display current user's cart items
/// - POST /Cart/AddToCart - Add product to cart with quantity validation
/// - POST /Cart/BuyNow - Quick purchase (bypasses cart, creates order immediately)
/// - POST /Cart/RemoveFromCart - Remove item from cart
///
/// Checkout flow:
/// - Users proceed to checkout via CheckoutController which handles PayPal payment processing
///
/// Business logic delegation:
/// - All cart operations delegated to ICartService for separation of concerns
/// - Controller focuses on HTTP concerns (validation, authorization, result shaping)
/// - Service layer handles business rules (inventory checks, price calculations)
///
/// Security:
/// - User email retrieved from authenticated context via AuthenticatedControllerBase
/// - All operations scoped to current user's cart
/// - Anti-forgery tokens required for all POST operations
/// </remarks>
public class CartController : AuthenticatedControllerBase
{
    private readonly ICartService _cartService;
    private readonly ILogger<CartController> _logger;

    public CartController(ICartService cartService, IUserService userService, ILogger<CartController> logger, ELKH.Data.ApplicationDbContext db)
        : base(db, userService)
    {
        _cartService = cartService;
        _logger = logger;
    }

    // ---------------------------------------------------------------------
    // Viewing endpoints
    // ---------------------------------------------------------------------
    // GET: /Cart
    public async Task<IActionResult> Index()
    {
        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return authResult;

        // Delegate retrieval to the cart service and render the view with model data.
        var items = await _cartService.GetCartItemsAsync(email);

        // Map CartModel list to CartVM
        var cartVM = new ViewModels.CartVM
        {
            Items = items.Select(c => new ViewModels.CartItemVM
            {
                CartItemId = c.PkCartId,
                ProductId = c.FkProductID,
                ProductName = c.Product?.Name ?? "Unknown",
                UnitPrice = c.Product?.GetEffectivePrice() ?? 0,
                Quantity = c.Quantity,
                ImageUrl = c.Product?.ProductImage?.FirstOrDefault()?.ProductImageURL,
                LineTotal = c.TotalPrice
            }).ToList()
        };

        // Calculate summary values
        cartVM.Tax = cartVM.Subtotal * 0.12m; // 12% tax
        cartVM.ShippingCost = cartVM.Subtotal >= 50 ? 0 : 5.99m;
        cartVM.Total = cartVM.Subtotal + cartVM.Tax + cartVM.ShippingCost;

        return View(cartVM);
    }

    // ---------------------------------------------------------------------
    // Cart modification endpoints (mutating operations)
    // ---------------------------------------------------------------------
    // POST: /Cart/AddToCart
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int itemId, int quantity)
    {
        // Validate input early to avoid unnecessary work.
        if (quantity <= 0) return BadRequest("Quantity must be positive.");

        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return authResult;

        // Service performs the actual add-to-cart business logic including inventory checks.
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

        // Removal is delegated to the service which enforces ownership and consistency checks.
        await _cartService.RemoveFromCartAsync(email, cartId);
        return RedirectToAction(nameof(Index));
    }
}
