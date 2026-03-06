using System.Security.Claims;
using ELKH.Data;
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
/// Handles cart operations (add, remove, checkout) and order placement with inventory management.
/// </summary>
/// <remarks>
/// All endpoints require authentication (inherited from AuthenticatedControllerBase).
///
/// Cart operations:
/// - GET /Cart - Display current user's cart items
/// - POST /Cart/AddToCart - Add product to cart with quantity validation
/// - POST /Cart/BuyNow - Quick purchase (add to cart and redirect to checkout)
/// - POST /Cart/RemoveFromCart - Remove item from cart
///
/// Checkout operations:
/// - GET /Cart/Checkout - Display checkout page with cart summary and total
/// - POST /Cart/PlaceOrder - Process order, decrement inventory, clear cart
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
    // Dependency: cart service implements domain logic for cart management and ordering.
    private readonly ICartService _cartService;

    public CartController(ICartService cartService, IUserService userService)
        : base(userService)
    {
        _cartService = cartService;
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
        return View(items);
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

    // ---------------------------------------------------------------------
    // Checkout and ordering endpoints
    // ---------------------------------------------------------------------
    // GET: /Cart/Checkout
    public async Task<IActionResult> Checkout()
    {
        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return authResult;

        // Gather items and compute totals for the checkout view. Keep presentation concerns here
        // while the service remains responsible for pricing/business calculations.
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
