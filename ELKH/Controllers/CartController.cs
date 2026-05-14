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
/// Shopping cart management controller for authenticated and guest users.
/// Handles cart operations (add, remove) and quick purchase functionality.
/// </summary>
public class CartController : Controller
{
    #region Constructor & Dependencies

    private readonly ICartService _cartService;
    private readonly IGuestCartService _guestCartService;
    private readonly IUserService _userService;
    private readonly ILogger<CartController> _logger;

    public CartController(
        ICartService cartService,
        IGuestCartService guestCartService,
        IUserService userService,
        ILogger<CartController> logger)
    {
        _cartService = cartService;
        _guestCartService = guestCartService;
        _userService = userService;
        _logger = logger;
    }

    #endregion

    #region Cart Display & Calculations

    /// <summary>
    /// Displays the user's shopping cart with item details and order summary.
    /// Supports both authenticated users (database cart) and guest users (session cart).
    /// </summary>
    /// <returns>View with cart items and calculated totals (tax, shipping, total)</returns>
    /// <remarks>
    /// PRICING CALCULATIONS:
    /// - Tax: 12% of subtotal (BC PST 7% + GST 5% composite rate)
    /// - Shipping: $5.99 flat rate, FREE for orders $50.00+
    /// - Total: Subtotal + Tax + Shipping
    ///
    /// BUSINESS RULES:
    /// - Free shipping threshold: $50.00 subtotal
    /// - Effective price calculated per product (includes discounts)
    /// - Line totals computed from effective price â”œÃ¹ quantity
    ///
    /// AUTHENTICATION ROUTING:
    /// - Authenticated: ICartService retrieves from database
    /// - Guest: IGuestCartService retrieves from session
    /// </remarks>
    public async Task<IActionResult> Index()
    {
        ViewModels.CartVM cartVM;

        if (User.Identity?.IsAuthenticated == true)
        {
            // Authenticated user: retrieve cart from database
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            var items = await _cartService.GetCartItemsAsync(email);

            cartVM = new ViewModels.CartVM
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
        }
        else
        {
            // Guest user: retrieve cart from session
            var items = await _guestCartService.GetCartItemsAsync();
            cartVM = new ViewModels.CartVM
            {
                Items = items
            };
        }

        // Calculate summary values
        // Tax: 12% (BC PST 7% + GST 5% composite rate, simplified for customer-facing display)
        // Shipping: $5.99 flat rate, waived for orders $50.00+ to incentivize larger purchases
        cartVM.Tax = cartVM.Subtotal * 0.12m;
        cartVM.ShippingCost = cartVM.Subtotal >= 50 ? 0 : 5.99m;
        cartVM.Total = cartVM.Subtotal + cartVM.Tax + cartVM.ShippingCost;

        return View(cartVM);
    }

    #endregion

    #region Cart Item Management

    /// <summary>
    /// Adds a product to the user's cart with specified quantity.
    /// Supports both AJAX (returns JSON) and standard form submission (redirects).
    /// </summary>
    /// <param name="itemId">Product ID to add</param>
    /// <param name="quantity">Quantity to add (must be positive)</param>
    /// <param name="returnUrl">Optional URL to redirect back to after adding (defaults to product details)</param>
    /// <returns>
    /// - AJAX: JSON with success status and cart count
    /// - Standard: Redirects to returnUrl or product details page with success message
    /// </returns>
    /// <remarks>
    /// Delegates to ICartService.AddToCartAsync for:
    /// - Inventory availability check
    /// - Merging with existing cart item if product already in cart
    /// - Creating new cart item if product not in cart
    /// 
    /// For AJAX requests, returns JSON: { "success": true, "cartCount": 5, "message": "Added 1 item to cart" }
    /// For standard requests, returns to specified URL or product details page with TempData success message.
    /// </remarks>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(ELKH.Extensions.RateLimitPolicies.Cart)]
    public async Task<IActionResult> AddToCart(int itemId, int quantity, string? returnUrl = null)
    {
        if (quantity <= 0) return BadRequest("Quantity must be positive.");

        try
        {
            int cartCount;

            if (User.Identity?.IsAuthenticated == true)
            {
                // Authenticated user: add to database cart
                var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                if (string.IsNullOrEmpty(email))
                {
                    return RedirectToAction("Login", "Account", new { area = "Identity" });
                }

                await _cartService.AddToCartAsync(email, itemId, quantity);
                var cartItems = await _cartService.GetCartItemsAsync(email);
                cartCount = cartItems.Sum(c => c.Quantity);
            }
            else
            {
                // Guest user: add to session cart
                await _guestCartService.AddToCartAsync(itemId, quantity);
                var cartItems = await _guestCartService.GetCartItemsAsync();
                cartCount = cartItems.Sum(c => c.Quantity);
            }

            // Check if this is an AJAX request
            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                         Request.Headers["Accept"].ToString().Contains("application/json");

            if (isAjax)
            {
                // Return JSON for AJAX requests
                var itemText = quantity == 1 ? "item" : "items";
                return Json(new
                {
                    success = true,
                    cartCount = cartCount,
                    quantity = quantity,
                    message = $"Î“Â£Ã´ Added {quantity} {itemText} to your cart!"
                });
            }

            // Standard form submission - redirect with TempData message
            TempData["Message"] = $"success,Î“Â£Ã´ Added {quantity} item{(quantity > 1 ? "s" : "")} to your cart!";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Details", "Product", new { id = itemId });
        }
        catch (InvalidOperationException ex)
        {
            // Handle out-of-stock and validation errors
            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                         Request.Headers["Accept"].ToString().Contains("application/json");

            if (isAjax)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }

            TempData["Message"] = $"error,{ex.Message}";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Details", "Product", new { id = itemId });
        }
    }

    /// <summary>
    /// Updates the quantity of an existing cart item.
    /// </summary>
    /// <param name="cartId">Cart item ID to update</param>
    /// <param name="quantity">New quantity (enforced minimum: 1)</param>
    /// <returns>Redirects to cart index</returns>
    /// <remarks>
    /// BUSINESS RULES:
    /// - Minimum quantity enforced: 1 (cannot reduce to zero)
    /// - To remove item completely, use Remove() instead
    /// - Inventory availability checked by service layer
    /// </remarks>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int cartId, int quantity)
    {
        if (quantity < 1) quantity = 1;

        if (User.Identity?.IsAuthenticated == true)
        {
            // Authenticated user: update database cart
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            await _cartService.UpdateQuantityAsync(email, cartId, quantity);
        }
        else
        {
            // Guest user: update session cart (cartId is productId for guests)
            await _guestCartService.UpdateQuantityAsync(cartId, quantity);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Removes a single item from the user's cart.
    /// </summary>
    /// <param name="cartId">Cart item ID to remove (productId for guest users)</param>
    /// <returns>Redirects to cart index</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int cartId)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            // Authenticated user: remove from database cart
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            await _cartService.RemoveFromCartAsync(email, cartId);
        }
        else
        {
            // Guest user: remove from session cart (cartId is productId for guests)
            await _guestCartService.RemoveFromCartAsync(cartId);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Clears all items from the user's cart.
    /// </summary>
    /// <returns>Redirects to cart index with success message</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            // Authenticated user: clear database cart
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            await _cartService.ClearCartAsync(email);
        }
        else
        {
            // Guest user: clear session cart
            await _guestCartService.ClearCartAsync();
        }

        TempData["Message"] = "success,Cart cleared.";
        return RedirectToAction(nameof(Index));
    }

    #endregion

    #region Order Creation

    /// <summary>
    /// Quick purchase: Add single item and immediately create order, bypassing cart.
    /// AUTHENTICATED USERS ONLY - Guest users must use standard checkout.
    /// </summary>
    /// <param name="itemId">Product ID to purchase</param>
    /// <param name="quantity">Quantity to purchase</param>
    /// <returns>Redirects based on operation status (see status codes in remarks)</returns>
    /// <remarks>
    /// WORKFLOW:
    /// 1. Add product to temporary cart entry
    /// 2. Validate user has delivery address
    /// 3. Check inventory availability
    /// 4. Create order and decrement stock
    /// 5. Clear temporary cart entry
    ///
    /// STATUS CODE HANDLING:
    /// - (-2): No delivery address Î“Ã¥Ã† Redirect to User/Addresses
    /// - (-1): Out of stock Î“Ã¥Ã† Redirect to Product/Details with warning
    /// - (0): Database error Î“Ã¥Ã† Redirect to Product/Details with warning
    /// - (>0): Success Î“Ã¥Ã† Redirect to Order/Details with order ID
    ///
    /// STREAMLINED UX:
    /// Bypasses traditional cart viewing for single-item purchases,
    /// reducing friction for impulse buys and mobile users.
    ///
    /// AUTHENTICATION:
    /// Guest users redirected to login - BuyNow requires stored delivery address.
    /// </remarks>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BuyNow(int itemId, int quantity)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login", "Account");
        }

        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
            return RedirectToAction("Login", "Account", new { area = "Identity" });
        }

        // Default to standard shipping - shipping method selection will be added in future checkout UI enhancement
        const int DEFAULT_SHIPPING_METHOD_ID = 1; // Standard Shipping
        var orderId = await _cartService.BuyNowAsync(email, itemId, quantity, DEFAULT_SHIPPING_METHOD_ID);
        switch (orderId)
        {
            case -3:
                TempData["Message"] = "warning,Invalid shipping method selected.";
                return RedirectToAction("Details", "Product", new { id = itemId });
            case -2:
                TempData["Message"] = "warning,Please add a delivery address before purchasing.";
                return RedirectToAction("Addresses", "User");
            case -1:
                TempData["Message"] = "warning,This item is currently out of stock.";
                return RedirectToAction("Details", "Product", new { id = itemId });
            case 0:
                TempData["Message"] = "warning,Unable to complete purchase. Please try again.";
                return RedirectToAction("Details", "Product", new { id = itemId });
            default:
                TempData["Message"] = "success,Order placed successfully!";
                return RedirectToAction("Details", "Order", new { id = orderId });
        }
    }

    /// <summary>
    /// Creates an order from all items currently in the user's cart.
    /// AUTHENTICATED USERS ONLY - Guest users must use guest checkout flow.
    /// </summary>
    /// <returns>Redirects based on operation status (see status codes in remarks)</returns>
    /// <remarks>
    /// WORKFLOW:
    /// 1. Validate user has delivery address
    /// 2. Retrieve all cart items for user
    /// 3. Verify inventory availability for all items
    /// 4. Create order with all cart items as order items
    /// 5. Decrement stock for each product
    /// 6. Clear user's cart
    ///
    /// STATUS CODE HANDLING:
    /// - (-2): No delivery address Î“Ã¥Ã† Redirect to User/Addresses
    /// - (-1): One or more items out of stock Î“Ã¥Ã† Redirect to Cart with warning
    /// - (0): Database error Î“Ã¥Ã† Redirect to Cart
    /// - (>0): Success Î“Ã¥Ã† Redirect to Order/Details with order ID
    ///
    /// ATOMICITY:
    /// Order creation is transactional - all items must be available
    /// or the entire order fails (no partial orders).
    ///
    /// AUTHENTICATION:
    /// Guest users redirected to guest checkout - PlaceOrder requires stored delivery address.
    /// </remarks>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Guest", "Checkout");
        }

        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
            return RedirectToAction("Login", "Account", new { area = "Identity" });
        }

        // Default to standard shipping - shipping method selection will be added in future checkout UI enhancement
        const int DEFAULT_SHIPPING_METHOD_ID = 1; // Standard Shipping
        var orderId = await _cartService.PlaceOrderAsync(email, DEFAULT_SHIPPING_METHOD_ID);
        switch (orderId)
        {
            case -3:
                TempData["Message"] = "warning,Invalid shipping method selected.";
                return RedirectToAction(nameof(Index));
            case -2:
                TempData["Message"] = "warning,Please add a delivery address to your account before placing an order.";
                return RedirectToAction("Addresses", "User");
            case -1:
                TempData["Message"] = "warning,One or more items in your cart are out of stock. Please update your cart.";
                return RedirectToAction(nameof(Index));
            case 0:
                return RedirectToAction(nameof(Index));
            default:
                TempData["Message"] = "success,Order placed successfully";
                return RedirectToAction("Details", "Order", new { id = orderId });
        }
    }

    #endregion
}
