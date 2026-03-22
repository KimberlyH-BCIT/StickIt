using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ELKH.ViewModels;
using ELKH.Data;
using ELKH.Services;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.Extensions;
using System.Security.Claims;
using System.Linq;

namespace ELKH.Controllers;

/// <summary>
/// Handles the multi-step checkout flow: cart summary display, address selection,
/// order pricing calculation, and PayPal payment processing.
/// </summary>
/// <remarks>
/// This controller combines features from two development branches:
/// - Saved addresses functionality (allows users to select from previously used addresses)
/// - Simplified PayPal integration (client-side capture, no backend API calls)
///
/// Pricing rules applied in Index() and re-verified server-side in ProcessPayment():
/// - Tax:      12% of subtotal (BC PST 7% + GST 5% composite rate).
/// - Shipping: $7.99 flat rate; waived when the subtotal reaches $50.00 or more.
///
/// TABLE OF CONTENTS
/// ==================
/// 1. Constructor & Dependencies (lines 34-46)
/// 2. GET /Checkout/Index - Display Checkout Page
///    - Load cart items (lines 59-61)
///    - Load saved addresses (lines 63-103)
///    - Calculate totals (lines 105-109)
///    - Configure PayPal (lines 111-114)
/// 3. POST /Checkout/ProcessPayment - Complete Order
///    - Validate form data (lines 122-126)
///    - Authenticate user (lines 128-133)
///    - Recalculate totals server-side (lines 138-142)
///    - Verify inventory (lines 144-152)
///    - Handle contact details (lines 154-180)
///    - Create order & order items (lines 182-208)
///    - Clear cart (lines 210-211)
///
/// Security Notes:
/// - All prices recalculated server-side to prevent tampering
/// - Inventory checked before order creation
/// - PayPal payment captured client-side, order status set to "Paid"
/// - Rate limiting applied via RateLimitPolicies.Checkout
/// </remarks>
[Authorize]
public class CheckoutController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICartRepo _cartRepo;
    private readonly IContactDetailRepo _contactRepo;
    private readonly ICartService _cartService;
    private readonly IConfiguration _configuration;

    public CheckoutController(
        ApplicationDbContext db,
        ICartRepo cartRepo,
        IContactDetailRepo contactRepo,
        ICartService cartService,
        IConfiguration configuration)
    {
        _db = db;
        _cartRepo = cartRepo;
        _contactRepo = contactRepo;
        _cartService = cartService;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // ═══════════════════════════════════════════════════════════════════
        // STEP 1: Authenticate and validate user
        // ═══════════════════════════════════════════════════════════════════
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
            return RedirectToPage("/Account/Login", new { area = "Identity" });

        var regUser = await _db.RegisteredUsers.FirstOrDefaultAsync(u => u.Email == email);
        if (regUser == null)
            return RedirectToAction("Index", "Cart");

        // ═══════════════════════════════════════════════════════════════════
        // STEP 2: Load cart items (redirect to cart if empty)
        // ═══════════════════════════════════════════════════════════════════
        var cartItems = await _cartRepo.GetByUserIdAsync(regUser.PkRegisteredUserId);
        if (!cartItems.Any())
            return RedirectToAction("Index", "Cart");

        // ═══════════════════════════════════════════════════════════════════
        // STEP 3: Load saved addresses for selection dropdown
        // Feature merged from HEAD branch - allows users to select from
        // previously used addresses or enter a new one
        // ═══════════════════════════════════════════════════════════════════
        var savedAddresses = await _contactRepo.GetAllByUserIdAsync(regUser.PkRegisteredUserId);
        var defaultAddress = await _contactRepo.GetDefaultByUserIdAsync(regUser.PkRegisteredUserId);

        // ═══════════════════════════════════════════════════════════════════
        // STEP 4: Build checkout view model with cart items and addresses
        // ═══════════════════════════════════════════════════════════════════
        var checkoutVM = new CheckoutVM
        {
            Items = cartItems.Select(c => new CartItemVM
            {
                CartItemId = c.PkCartId,
                ProductName = c.Product?.Name ?? "",
                Quantity = c.Quantity,
                UnitPrice = c.Product?.GetEffectivePrice() ?? 0m,
                LineTotal = (c.Product?.GetEffectivePrice() ?? 0m) * c.Quantity
            }).ToList(),
            SavedAddresses = savedAddresses.Select(a => new ContactDetailVM
            {
                ContactId = a.PkContactId,
                FirstName = a.FirstName,
                LastName = a.LastName,
                PhoneNumber = a.PhoneNumber,
                Street = a.Street,
                City = a.City,
                Province = a.Province,
                PostalCode = a.PostCode,
                Country = a.Country,
                IsDefault = a.IsDefault
            }).ToList()
        };

        // ═══════════════════════════════════════════════════════════════════
        // STEP 5: Pre-populate form with default address if available
        // ═══════════════════════════════════════════════════════════════════
        // Pre-populate with default address if available
        if (defaultAddress != null)
        {
            checkoutVM.SelectedContactId = defaultAddress.PkContactId;
            checkoutVM.FullName = $"{defaultAddress.FirstName} {defaultAddress.LastName}";
            checkoutVM.Street = defaultAddress.Street;
            checkoutVM.City = defaultAddress.City;
            checkoutVM.Province = defaultAddress.Province;
            checkoutVM.PostalCode = defaultAddress.PostCode;
            checkoutVM.Country = defaultAddress.Country;
            checkoutVM.PhoneNumber = defaultAddress.PhoneNumber;
        }

        // ═══════════════════════════════════════════════════════════════════
        // STEP 6: Calculate order totals
        // Tax: 12% (BC PST 7% + GST 5%)
        // Shipping: Free for orders $50+, otherwise $7.99
        // ═══════════════════════════════════════════════════════════════════
        // Calculate totals
        checkoutVM.Subtotal = checkoutVM.Items.Sum(i => i.LineTotal);
        checkoutVM.Tax = checkoutVM.Subtotal * 0.12m;
        checkoutVM.ShippingCost = checkoutVM.Subtotal >= 50m ? 0m : 7.99m;
        checkoutVM.Total = checkoutVM.Subtotal + checkoutVM.Tax + checkoutVM.ShippingCost;

        // ═══════════════════════════════════════════════════════════════════
        // STEP 7: Configure PayPal client-side integration
        // Using simplified approach from pr-43 (client-side capture only)
        // ═══════════════════════════════════════════════════════════════════
        // Simple PayPal client ID from config (pr-43 approach that was working)
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
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimitPolicies.Checkout)]
    public async Task<IActionResult> ProcessPayment(CheckoutVM vm)
    {
        // ═══════════════════════════════════════════════════════════════════
        // STEP 1: Validate form input
        // ═══════════════════════════════════════════════════════════════════
        if (!ModelState.IsValid)
        {
            ViewBag.PayPalClientId = _configuration["PayPal:ClientId"];
            return View("Index", vm);
        }

        // ═══════════════════════════════════════════════════════════════════
        // STEP 2: Authenticate user and load cart
        // ═══════════════════════════════════════════════════════════════════
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
            return RedirectToPage("/Account/Login", new { area = "Identity" });

        var regUser = await _db.RegisteredUsers.FirstOrDefaultAsync(u => u.Email == email);
        if (regUser is null) return RedirectToAction("Index", "Cart");

        var cartItems = (await _cartRepo.GetByUserIdAsync(regUser.PkRegisteredUserId)).ToList();
        if (!cartItems.Any()) return RedirectToAction("Index", "Cart");

        // ═══════════════════════════════════════════════════════════════════
        // STEP 3: Recalculate totals server-side (security: prevent tampering)
        // ═══════════════════════════════════════════════════════════════════
        // Recalculate server-side to prevent client-side price tampering
        var subtotal = cartItems.Sum(c => (c.Product?.GetEffectivePrice() ?? 0m) * c.Quantity);
        var tax = subtotal * 0.12m;
        var shipping = subtotal >= 50m ? 0m : 7.99m;
        var total = subtotal + tax + shipping;

        // ═══════════════════════════════════════════════════════════════════
        // STEP 4: Verify inventory availability before creating order
        // ═══════════════════════════════════════════════════════════════════
        // Check inventory before processing
        foreach (var item in cartItems)
        {
            if (item.Product == null || (item.Product.StockQuantity ?? 0) < item.Quantity)
            {
                TempData["Message"] = "error,One or more items in your cart are out of stock.";
                return RedirectToAction("Index");
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // STEP 5: Create or retrieve contact detail (shipping address)
        // Supports both saved addresses and new address entry
        // ═══════════════════════════════════════════════════════════════════
        // Create or update contact detail
        ContactDetailModel? contact = null;
        if (vm.SelectedContactId.HasValue && vm.SelectedContactId.Value > 0)
        {
            contact = await _contactRepo.GetByIdAsync(vm.SelectedContactId.Value);
        }

        if (contact == null)
        {
            // Create new contact
            // Split full name into first/last (simple split on first space)
            var names = (vm.FullName ?? "").Split(' ', 2);
            contact = new ContactDetailModel
            {
                FkRegisteredUserId = regUser.PkRegisteredUserId,
                FirstName = names.Length > 0 ? names[0] : "",
                LastName = names.Length > 1 ? names[1] : "",
                PhoneNumber = vm.PhoneNumber ?? "",
                Street = vm.Street ?? "",
                City = vm.City ?? "",
                Province = vm.Province ?? "",
                PostCode = vm.PostalCode ?? "",
                Country = vm.Country ?? "Canada",
                IsDefault = false
            };
            _db.ContactDetails.Add(contact);
            await _db.SaveChangesAsync();
        }

        // ═══════════════════════════════════════════════════════════════════
        // STEP 6: Create order record
        // OrderStatus set to "Paid" because PayPal already captured payment
        // ═══════════════════════════════════════════════════════════════════
        // Create order
        var order = new OrderModel
        {
            FkContactId = contact.PkContactId,
            FkRegisteredUserId = regUser.PkRegisteredUserId,
            OrderStatus = "Paid", // Since PayPal was already captured client-side
            TotalAmount = total,
            CreatedAt = DateTime.UtcNow,
            DeliveryStatus = "Pending"
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // ═══════════════════════════════════════════════════════════════════
        // STEP 7: Create order items and decrement inventory
        // ═══════════════════════════════════════════════════════════════════
        // Create order items and decrement inventory
        foreach (var cartItem in cartItems)
        {
            var orderItem = new OrderItemModel
            {
                FkOrderId = order.PkOrderId,
                FkProductId = cartItem.FkProductID,
                Quantity = cartItem.Quantity,
                UnitPrice = cartItem.Product?.GetEffectivePrice() ?? 0m
            };
            _db.OrderItems.Add(orderItem);

            // Decrement inventory
            if (cartItem.Product != null)
            {
                cartItem.Product.StockQuantity = (cartItem.Product.StockQuantity ?? 0) - cartItem.Quantity;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // STEP 8: Clear cart and redirect to order confirmation
        // ═══════════════════════════════════════════════════════════════════
        // Clear cart
        _db.Carts.RemoveRange(cartItems);
        await _db.SaveChangesAsync();

        TempData["Message"] = "success,Order placed successfully!";
        return RedirectToAction("Details", "Order", new { id = order.PkOrderId });
    }
}
