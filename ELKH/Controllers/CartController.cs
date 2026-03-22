using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ELKH.Data;
using ELKH.Extensions;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.Services;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Mvc;

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
/// - POST /Cart/Update - Update item quantity in cart
/// - POST /Cart/Remove - Remove item from cart
/// - POST /Cart/Clear - Clear all items from cart
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
///
/// TABLE OF CONTENTS
/// ==================
/// 1. Constructor & Dependencies (lines 44-52)
/// 2. Cart Display & Calculations
///    - Index() - Display cart with tax/shipping calculations (lines 55-84)
/// 3. Cart Item Management
///    - AddToCart() - Add product with quantity validation (lines 87-98)
///    - Update() - Modify item quantity (minimum 1) (lines 127-138)
///    - Remove() - Remove single item from cart (lines 141-150)
///    - Clear() - Empty entire cart (lines 153-163)
/// 4. Order Creation
///    - BuyNow() - Quick purchase bypassing cart (lines 101-124)
///    - PlaceOrder() - Create order from cart items (lines 166-181)
///
/// Status Code Conventions:
/// - (-2) = User has no delivery address configured
/// - (-1) = Product out of stock / inventory insufficient
/// - (0)  = Operation failed / database error
/// - (>0) = Success: returns order ID
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
        // Tax: 12% (BC PST 7% + GST 5% composite rate, simplified for customer-facing display)
        // Shipping: $5.99 flat rate, waived for orders $50.00+ to incentivize larger purchases
        cartVM.Tax = cartVM.Subtotal * 0.12m;
        cartVM.ShippingCost = cartVM.Subtotal >= 50 ? 0 : 5.99m;
        cartVM.Total = cartVM.Subtotal + cartVM.Tax + cartVM.ShippingCost;

        return View(cartVM);
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
    // Quick purchase: Add single item to cart and immediately create order.
    // Bypasses the traditional cart viewing step for streamlined UX.
    // Returns status codes: -2=no address, -1=out of stock, 0=error, >0=order ID
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
    // Modify quantity of an existing cart item.
    // Enforces minimum quantity of 1 (cannot reduce to zero; use Remove instead).
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
    // Create order from all current cart items, verify inventory, and clear cart on success.
    // Returns status codes: -2=no address, -1=out of stock, 0=error, >0=order ID
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