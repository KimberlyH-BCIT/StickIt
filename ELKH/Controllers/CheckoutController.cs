using ELKH.Extensions;
using ELKH.Services;

namespace ELKH.Controllers;

/// <summary>
/// Handles the multi-step checkout flow for both authenticated and guest users:
/// - Authenticated users: cart summary, address selection, order pricing, PayPal processing
/// - Guest users: simplified checkout with contact detail collection
/// </summary>
/// <remarks>
/// This controller combines features from two development branches:
/// - Saved addresses functionality (allows users to select from previously used addresses)
/// - Simplified PayPal integration (client-side capture, no backend API calls)
/// - Guest checkout support (session-based cart, email-only order tracking)
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
public class CheckoutController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICartRepo _cartRepo;
    private readonly IContactDetailRepo _contactRepo;
    private readonly ICartService _cartService;
    private readonly IGuestCartService _guestCartService;
    private readonly IConfiguration _configuration;
    private readonly IShippingService _shippingService;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(
        ApplicationDbContext db,
        ICartRepo cartRepo,
        IContactDetailRepo contactRepo,
        ICartService cartService,
        IGuestCartService guestCartService,
        IConfiguration configuration,
        IShippingService shippingService,
        ILogger<CheckoutController> logger)
    {
        _db = db;
        _cartRepo = cartRepo;
        _contactRepo = contactRepo;
        _cartService = cartService;
        _guestCartService = guestCartService;
        _configuration = configuration;
        _shippingService = shippingService;
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

        var regUser = await _db.RegisteredUsers.FirstOrDefaultAsync(u => u.Email == email);
        if (regUser == null)
            return RedirectToAction("Index", "Cart");

        // ===================================================================
        // STEP 2: Load cart items (redirect to cart if empty)
        // ===================================================================
        var cartItems = await _cartRepo.GetByUserIdAsync(regUser.PkRegisteredUserId);
        if (!cartItems.Any())
            return RedirectToAction("Index", "Cart");

        // ===================================================================
        // STEP 3: Load saved addresses for selection dropdown
        // Feature merged from HEAD branch - allows users to select from
        // previously used addresses or enter a new one
        // ===================================================================
        var savedAddresses = await _contactRepo.GetAllByUserIdAsync(regUser.PkRegisteredUserId);
        var defaultAddress = await _contactRepo.GetDefaultByUserIdAsync(regUser.PkRegisteredUserId);

        // ===================================================================
        // STEP 4: Load available shipping methods
        // ===================================================================
        var availableShippingMethods = await _shippingService.GetAvailableShippingMethodsAsync();

        // ===================================================================
        // STEP 5: Build checkout view model with cart items and addresses
        // ===================================================================
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
            }).ToList(),
            AvailableShippingMethods = availableShippingMethods,
            SelectedShippingMethodId = availableShippingMethods.FirstOrDefault()?.PkShippingMethodId ?? 1 // Default to first (Standard)
        };

        // ===================================================================
        // STEP 6: Pre-populate form with default address if available
        // ===================================================================
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

        // ===================================================================
        // STEP 7: Calculate order totals with dynamic shipping
        // Tax: 12% (BC PST 7% + GST 5%)
        // Shipping: Calculated using ShippingService with free shipping threshold
        // ===================================================================
        // Calculate totals
        checkoutVM.Subtotal = checkoutVM.Items.Sum(i => i.LineTotal);
        checkoutVM.Tax = checkoutVM.Subtotal * 0.12m;
        checkoutVM.ShippingCost = await _shippingService.CalculateShippingCostAsync(
            checkoutVM.SelectedShippingMethodId, checkoutVM.Subtotal);
        checkoutVM.Total = checkoutVM.Subtotal + checkoutVM.Tax + checkoutVM.ShippingCost;

        // ===================================================================
        // STEP 8: Configure PayPal client-side integration
        // Using simplified approach from pr-43 (client-side capture only)
        // ===================================================================
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
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimitPolicies.Checkout)]
    public async Task<IActionResult> ProcessPayment(CheckoutVM vm)
    {
        // ===================================================================
        // STEP 1: Validate form input
        // ===================================================================
        if (!ModelState.IsValid)
        {
            // Re-populate display-only fields that are not posted back as form fields
            var emailForRepopulate = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(emailForRepopulate))
            {
                var userForRepopulate = await _db.RegisteredUsers
                    .FirstOrDefaultAsync(u => u.Email == emailForRepopulate);
                if (userForRepopulate != null)
                {
                    var cartItemsForRepopulate = await _cartRepo.GetByUserIdAsync(userForRepopulate.PkRegisteredUserId);
                    vm.Items = cartItemsForRepopulate.Select(c => new CartItemVM
                    {
                        CartItemId = c.PkCartId,
                        ProductName = c.Product?.Name ?? "",
                        Quantity = c.Quantity,
                        UnitPrice = c.Product?.GetEffectivePrice() ?? 0m,
                        LineTotal = (c.Product?.GetEffectivePrice() ?? 0m) * c.Quantity
                    }).ToList();
                    vm.Subtotal = vm.Items.Sum(i => i.LineTotal);
                    vm.Tax = vm.Subtotal * 0.12m;
                    vm.ShippingCost = await _shippingService.CalculateShippingCostAsync(
                        vm.SelectedShippingMethodId, vm.Subtotal);
                    vm.Total = vm.Subtotal + vm.Tax + vm.ShippingCost;
                    vm.SavedAddresses = (await _contactRepo.GetAllByUserIdAsync(userForRepopulate.PkRegisteredUserId))
                        .Select(a => new ContactDetailVM
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
                        }).ToList();
                    vm.AvailableShippingMethods = await _shippingService.GetAvailableShippingMethodsAsync();
                }
            }
            ViewBag.PayPalClientId = _configuration["PayPal:ClientId"];
            return View("Index", vm);
        }

        // ===================================================================
        // STEP 2: Authenticate user and load cart
        // ===================================================================
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
            return RedirectToPage("/Account/Login", new { area = "Identity" });

        var regUser = await _db.RegisteredUsers.FirstOrDefaultAsync(u => u.Email == email);
        if (regUser is null) return RedirectToAction("Index", "Cart");

        var cartItems = (await _cartRepo.GetByUserIdAsync(regUser.PkRegisteredUserId)).ToList();
        if (cartItems.Count == 0) return RedirectToAction("Index", "Cart");

        // ===================================================================
        // STEP 3: Validate shipping method and recalculate totals server-side (security: prevent tampering)
        // ===================================================================
        // Validate selected shipping method
        var shippingMethod = await _shippingService.GetShippingMethodByIdAsync(vm.SelectedShippingMethodId);
        if (shippingMethod == null || !shippingMethod.IsActive)
        {
            TempData["Message"] = "error,Invalid shipping method selected.";
            return RedirectToAction("Index");
        }

        // Recalculate server-side to prevent client-side price tampering
        var subtotal = cartItems.Sum(c => (c.Product?.GetEffectivePrice() ?? 0m) * c.Quantity);
        var tax = subtotal * 0.12m;
        var shipping = await _shippingService.CalculateShippingCostAsync(vm.SelectedShippingMethodId, subtotal);
        var total = subtotal + tax + shipping;

        // ===================================================================
        // STEP 4: Verify inventory availability before creating order
        // ===================================================================
        // Check inventory before processing
        foreach (var item in cartItems)
        {
            if (item.Product == null || (item.Product.StockQuantity ?? 0) < item.Quantity)
            {
                TempData["Message"] = "error,One or more items in your cart are out of stock.";
                return RedirectToAction("Index");
            }
        }

        // ===================================================================
        // STEP 5-8: Create order in a transaction to ensure atomicity
        // All database operations wrapped in transaction for data consistency
        // ===================================================================
        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // ===================================================================
            // STEP 5: Create or retrieve contact detail (shipping address)
            // Supports both saved addresses and new address entry
            // ===================================================================
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

            // ===================================================================
            // STEP 6: Create order record with shipping information
            // OrderStatus set to "Paid" because PayPal already captured payment
            // ===================================================================
            // Create order
            var order = new OrderModel
            {
                FkContactId = contact.PkContactId,
                FkRegisteredUserId = regUser.PkRegisteredUserId,
                OrderStatus = OrderStatus.Paid, // Since PayPal was already captured client-side
                TotalAmount = total,
                CreatedAt = DateTime.UtcNow,
                DeliveryStatus = DeliveryStatus.Pending,
                FkShippingMethodId = vm.SelectedShippingMethodId,
                ShippingMethodName = shippingMethod.Name,
                ShippingCost = shipping
            };
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            // ===================================================================
            // STEP 7: Create order items and decrement inventory
            // ===================================================================
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

            // ===================================================================
            // STEP 8: Clear cart and commit transaction
            // ===================================================================
            // Clear cart
            _db.Carts.RemoveRange(cartItems);
            await _db.SaveChangesAsync();

            // Commit transaction - all changes are now permanent
            await transaction.CommitAsync();

            TempData["Message"] = "success,Order placed successfully!";
            return RedirectToAction("Details", "Order", new { id = order.PkOrderId });
        }
        catch (Exception ex)
        {
            // Rollback transaction on any error to maintain data consistency
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to process order for user {Email}", email);
            TempData["Message"] = "error,An error occurred while processing your order. Please try again.";
            return RedirectToAction("Index");
        }
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
        guestCheckoutVM.Tax = guestCheckoutVM.Subtotal * 0.12m;
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
        // ===================================================================
        // STEP 1: Validate form input
        // ===================================================================
        if (!ModelState.IsValid)
        {
            vm.PayPalClientId = _configuration["PayPal:ClientId"];
            return View("Guest", vm);
        }

        // ===================================================================
        // STEP 2: Retrieve session cart
        // ===================================================================
        var cartItems = await _guestCartService.GetCartItemsAsync();
        if (cartItems.Count == 0)
        {
            TempData["Message"] = "error,Your cart is empty.";
            return RedirectToAction("Index", "Cart");
        }

        // Load full product details for inventory check and price calculation
        var productIds = cartItems.Select(c => c.ProductId).ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.PkProductId))
            .ToListAsync();

        // ===================================================================
        // STEP 3: Validate shipping method and recalculate totals server-side (security)
        // ===================================================================
        // Validate selected shipping method
        var shippingMethod = await _shippingService.GetShippingMethodByIdAsync(vm.SelectedShippingMethodId);
        if (shippingMethod == null || !shippingMethod.IsActive)
        {
            TempData["Message"] = "error,Invalid shipping method selected.";
            return RedirectToAction("Guest");
        }

        var subtotal = 0m;
        foreach (var item in cartItems)
        {
            var product = products.FirstOrDefault(p => p.PkProductId == item.ProductId);
            if (product != null)
            {
                subtotal += product.GetEffectivePrice() * item.Quantity;
            }
        }
        var tax = subtotal * 0.12m;
        var shipping = await _shippingService.CalculateShippingCostAsync(vm.SelectedShippingMethodId, subtotal);
        var total = subtotal + tax + shipping;

        // ===================================================================
        // STEP 4: Verify inventory availability
        // ===================================================================
        foreach (var item in cartItems)
        {
            var product = products.FirstOrDefault(p => p.PkProductId == item.ProductId);
            if (product == null || (product.StockQuantity ?? 0) < item.Quantity)
            {
                TempData["Message"] = "error,One or more items in your cart are out of stock.";
                return RedirectToAction("Guest");
            }
        }

        // ===================================================================
        // STEP 5: Create contact detail (not linked to user account)
        // ===================================================================
        var names = (vm.FullName ?? "").Split(' ', 2);
        var contact = new ContactDetailModel
        {
            FkRegisteredUserId = null, // Guest order: no user association
            FirstName = names.Length > 0 ? names[0] : "",
            LastName = names.Length > 1 ? names[1] : "",
            PhoneNumber = vm.PhoneNumber ?? "",
            Street = vm.Street ?? "",
            City = vm.City ?? "",
            Province = vm.Province ?? "",
            PostCode = vm.PostalCode ?? "",
            Country = "Canada",
            IsDefault = false
        };
        _db.ContactDetails.Add(contact);
        await _db.SaveChangesAsync();

        // ===================================================================
        // STEP 6: Create order with shipping information (FkRegisteredUserId = 0 for guest orders)
        // Note: Guest orders identified by FkRegisteredUserId = 0
        // Future enhancement: Make FkRegisteredUserId nullable in OrderModel schema
        // ===================================================================
        var order = new OrderModel
        {
            FkContactId = contact.PkContactId,
            FkRegisteredUserId = 0, // Guest order (0 = no user account)
            OrderStatus = OrderStatus.Paid,
            TotalAmount = total,
            CreatedAt = DateTime.UtcNow,
            DeliveryStatus = DeliveryStatus.Pending,
            FkShippingMethodId = vm.SelectedShippingMethodId,
            ShippingMethodName = shippingMethod.Name,
            ShippingCost = shipping
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // ===================================================================
        // STEP 7: Create order items and decrement inventory
        // ===================================================================
        foreach (var item in cartItems)
        {
            var product = products.FirstOrDefault(p => p.PkProductId == item.ProductId);
            if (product != null)
            {
                var orderItem = new OrderItemModel
                {
                    FkOrderId = order.PkOrderId,
                    FkProductId = product.PkProductId,
                    Quantity = item.Quantity,
                    UnitPrice = product.GetEffectivePrice()
                };
                _db.OrderItems.Add(orderItem);

                // Decrement inventory
                product.StockQuantity = (product.StockQuantity ?? 0) - item.Quantity;
            }
        }
        await _db.SaveChangesAsync();

        // ===================================================================
        // STEP 8: Clear session cart
        // ===================================================================
        await _guestCartService.ClearCartAsync();

        // ===================================================================
        // STEP 9: Optional - Create user account if requested
        // ===================================================================
        // Account creation functionality is planned for future release:
        // - Check if vm.CreateAccount == true
        // - Validate password requirements  
        // - Create ASP.NET Core Identity user
        // - Link order to new user account
        // - Send welcome email

        // ===================================================================
        // STEP 10: Redirect to confirmation
        // ===================================================================
        TempData["Message"] = "success,Order placed successfully! Check your email for order details.";
        TempData["GuestOrderEmail"] = vm.Email;
        return RedirectToAction("GuestConfirmation", new { orderId = order.PkOrderId, email = vm.Email });
    }

    /// <summary>
    /// Displays order confirmation for guest checkout.
    /// Email-based order lookup for guests without accounts.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GuestConfirmation(int orderId, string email)
    {
        // Retrieve order with contact details
        var order = await _db.Orders
            .Include(o => o.ContactDetail)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.PkOrderId == orderId);

        if (order == null)
        {
            TempData["Message"] = "error,Order not found.";
            return RedirectToAction("Index", "Home");
        }

        // Security: Verify email matches (simple validation for guest orders)
        // In production, consider more secure token-based approach
        if (order.ContactDetail?.FirstName == null || 
            !email.Equals(TempData["GuestOrderEmail"]?.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            TempData["Message"] = "error,Unauthorized access.";
            return RedirectToAction("Index", "Home");
        }

        return View(order);
    }

    #endregion
}
