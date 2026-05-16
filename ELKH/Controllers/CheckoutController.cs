using System.Security.Cryptography;
using System.Text;
using ELKH.Extensions;
using ELKH.Services;
using ELKH.Models;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Controllers;

/// <summary>
/// Handles the multi-step checkout flow for both authenticated and guest users:
/// - Authenticated users: cart summary, address selection, order pricing, PayPal processing
/// - Guest users: simplified checkout with contact detail collection
/// </summary>
public class CheckoutController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICartRepo _cartRepo;
    private readonly IContactDetailRepo _contactRepo;
    private readonly ICartService _cartService;
    private readonly IGuestCartService _guestCartService;
    private readonly ICheckoutOrchestrationService _checkoutOrchestrationService;
    private readonly IConfiguration _configuration;
    private readonly IShippingService _shippingService;
    private readonly IPayPalService _payPalService;
    private readonly IOrderEmailService _orderEmailService;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(
        ApplicationDbContext db,
        ICartRepo cartRepo,
        IContactDetailRepo contactRepo,
        ICartService cartService,
        IGuestCartService guestCartService,
        ICheckoutOrchestrationService checkoutOrchestrationService,
        IConfiguration configuration,
        IShippingService shippingService,
        IPayPalService payPalService,
        IOrderEmailService orderEmailService,
        ILogger<CheckoutController> logger)
    {
        _db = db;
        _cartRepo = cartRepo;
        _contactRepo = contactRepo;
        _cartService = cartService;
        _guestCartService = guestCartService;
        _checkoutOrchestrationService = checkoutOrchestrationService;
        _configuration = configuration;
        _shippingService = shippingService;
        _payPalService = payPalService;
        _orderEmailService = orderEmailService;
        _logger = logger;
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // ===================================================================
        // STEP 1: Authenticate and validate user
        // ===================================================================
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
            return RedirectToPage("/Account/Login", new { area = "Identity" });

        var checkoutVM = await _checkoutOrchestrationService.BuildCheckoutAsync(email);
        if (checkoutVM == null)
            return RedirectToAction("Index", "Cart");
        ViewBag.PayPalClientId = _configuration["PayPal:ClientId"];

        return View(checkoutVM);
    }

    /// <summary>
    /// Processes the checkout payment after PayPal capture.
    /// Creates order, decrements inventory, and clears cart on success.
    /// </summary>
    /// <remarks>
    /// This endpoint is called AFTER PayPal has captured payment client-side.
    /// All prices are recalculated server-side to prevent client-side tampering.
    /// Rate limiting applied to prevent abuse.
    /// </remarks>
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimitPolicies.Checkout)]
    public async Task<IActionResult> ProcessPayment(CheckoutVM vm)
    {
        if (!ModelState.IsValid)
        {
            var emailForRepopulate = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(emailForRepopulate))
            {
                await _checkoutOrchestrationService.PopulateCheckoutAsync(vm, emailForRepopulate);
            }
            ViewBag.PayPalClientId = _configuration["PayPal:ClientId"];
            return View("Index", vm);
        }

        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
            return RedirectToPage("/Account/Login", new { area = "Identity" });

        var expectedCurrency = _configuration["PayPal:Currency"] ?? "CAD";
        var result = await _checkoutOrchestrationService.ProcessPaymentAsync(email, vm, expectedCurrency);
        if (!result.Success)
        {
            TempData["Message"] = $"error,{result.ErrorMessage}";
            return RedirectToAction("Index");
        }

        TempData["Message"] = "success,Order placed successfully!";
        return RedirectToAction("Details", "Order", new { id = result.OrderId });
    }

    #region Guest Checkout

    /// <summary>
    /// Displays guest checkout form with session cart items.
    /// </summary>
    /// <remarks>
    /// GUEST CHECKOUT FLOW:
    /// 1. Retrieve cart from session storage
    /// 2. Calculate totals (tax, shipping, total)
    /// 3. Display form for guest information collection
    /// 4. Optional account creation checkbox
    ///
    /// BUSINESS RULES:
    /// - Tax: 12% (BC PST 7% + GST 5%)
    /// - Shipping: $7.99 flat rate, FREE for orders $50.00+
    /// - Session timeout: 30 minutes idle
    /// </remarks>
    [HttpGet]
    public async Task<IActionResult> Guest()
    {
        // Retrieve cart from session
        var cartItems = await _guestCartService.GetCartItemsAsync();
        if (cartItems.Count == 0)
        {
            TempData["Message"] = "warning,Your cart is empty. Please add items before checking out.";
            return RedirectToAction("Index", "Cart");
        }

        // Load available shipping methods
        var availableShippingMethods = await _shippingService.GetAvailableShippingMethodsAsync();

        // Build guest checkout view model
        var guestCheckoutVM = new GuestCheckoutVM
        {
            Items = cartItems,
            AvailableShippingMethods = availableShippingMethods,
            SelectedShippingMethodId = availableShippingMethods.FirstOrDefault()?.PkShippingMethodId ?? 1 // Default to first (Standard)
        };

        // Calculate totals using ShippingService
        guestCheckoutVM.Tax = decimal.Round(guestCheckoutVM.Subtotal * 0.12m, 2, MidpointRounding.AwayFromZero);
        guestCheckoutVM.ShippingCost = await _shippingService.CalculateShippingCostAsync(
            guestCheckoutVM.SelectedShippingMethodId, guestCheckoutVM.Subtotal);

        // Configure PayPal
        guestCheckoutVM.PayPalClientId = _configuration["PayPal:ClientId"];

        return View(guestCheckoutVM);
    }

    /// <summary>
    /// Processes guest checkout payment after PayPal capture.
    /// Creates order without user account, clears session cart.
    /// Optionally creates user account if requested.
    /// </summary>
    /// <remarks>
    /// WORKFLOW:
    /// 1. Validate form input (email, name, address, phone)
    /// 2. Retrieve session cart items
    /// 3. Recalculate totals server-side (security)
    /// 4. Verify inventory availability
    /// 5. Create contact detail record (not linked to user)
    /// 6. Create order record (FkRegisteredUserId = null for guests)
    /// 7. Create order items and decrement inventory
    /// 8. Clear session cart
    /// 9. Optional: Create user account if requested
    /// 10. Redirect to order confirmation (email-based lookup)
    ///
    /// SECURITY:
    /// - All prices recalculated server-side
    /// - Inventory verified before order creation
    /// - Rate limiting applied
    /// - PayPal payment captured client-side
    /// </remarks>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimitPolicies.Checkout)]
    public async Task<IActionResult> ProcessGuestPayment(GuestCheckoutVM vm)
    {
        if (!ModelState.IsValid)
        {
            vm.PayPalClientId = _configuration["PayPal:ClientId"];
            return View("Guest", vm);
        }

        var expectedCurrency = _configuration["PayPal:Currency"] ?? "CAD";
        var result = await _checkoutOrchestrationService.ProcessGuestPaymentAsync(
            vm,
            expectedCurrency,
            Request.Scheme,
            Request.Host.Value ?? string.Empty);

        if (!result.Success)
        {
            TempData["Message"] = $"error,{result.ErrorMessage}";
            return result.ErrorMessage == "Your cart is empty."
                ? RedirectToAction("Index", "Cart")
                : RedirectToAction("Guest");
        }

        TempData["Message"] = "success,Order placed successfully! Check your email for order details.";
        return RedirectToAction(nameof(GuestConfirmation), new { token = result.GuestAccessToken });
    }

    /// <summary>
    /// Displays order confirmation for guest checkout using a secure access token.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GuestConfirmation(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["Message"] = "error,Invalid guest order access link.";
            return RedirectToAction("Index", "Home");
        }

        var tokenHash = HashGuestAccessToken(token);

        var order = await _db.Orders
            .Include(o => o.ContactDetail)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.GuestAccessTokenHash == tokenHash && o.FkRegisteredUserId == null);

        if (order == null)
        {
            TempData["Message"] = "error,Invalid guest order access link.";
            return RedirectToAction("Index", "Home");
        }

        if (order.ContactDetail == null)
        {
            order.ContactDetail = await _db.ContactDetails.FindAsync(order.FkContactId);
        }

        if (order.OrderItems.Count == 0)
        {
            order.OrderItems = await _db.OrderItems
                .Include(oi => oi.Product)
                .Where(oi => oi.FkOrderId == order.PkOrderId)
                .ToListAsync();
        }

        return View(order);
    }

    private static string HashGuestAccessToken(string token)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    #endregion
}
