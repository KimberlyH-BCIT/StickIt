using System.Security.Cryptography;
using System.Text;
using ELKH.Extensions;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Services;

public sealed class CheckoutOrchestrationService(
    ApplicationDbContext db,
    IUserService userService,
    ICartRepo cartRepo,
    IContactDetailRepo contactRepo,
    IGuestCartService guestCartService,
    IShippingService shippingService,
    IPayPalService payPalService,
    IOrderEmailService orderEmailService,
    ILogger<CheckoutOrchestrationService> logger) : ICheckoutOrchestrationService
{
    public async Task<CheckoutVM?> BuildCheckoutAsync(string email, CancellationToken ct = default)
    {
        var model = new CheckoutVM();
        await PopulateCheckoutAsync(model, email, ct);
        return model.Items.Count == 0 ? null : model;
    }

    public async Task PopulateCheckoutAsync(CheckoutVM model, string email, CancellationToken ct = default)
    {
        var user = await userService.GetByEmailAsync(email, ct);
        if (user == null)
        {
            model.Items = [];
            model.SavedAddresses = [];
            model.AvailableShippingMethods = [];
            return;
        }

        var cartItems = await cartRepo.GetByUserIdAsync(user.PkRegisteredUserId);
        model.Items = cartItems.Select(c => new CartItemVM
        {
            CartItemId = c.PkCartId,
            ProductName = c.Product?.Name ?? string.Empty,
            Quantity = c.Quantity,
            UnitPrice = c.Product?.GetEffectivePrice() ?? 0m,
            LineTotal = (c.Product?.GetEffectivePrice() ?? 0m) * c.Quantity
        }).ToList();

        model.SavedAddresses = (await contactRepo.GetAllByUserIdAsync(user.PkRegisteredUserId))
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

        model.AvailableShippingMethods = (await shippingService.GetAvailableShippingMethodsAsync()).ToList();
        if (model.SelectedShippingMethodId <= 0)
        {
            model.SelectedShippingMethodId = model.AvailableShippingMethods.FirstOrDefault()?.PkShippingMethodId ?? 1;
        }

        await ApplyTotalsAsync(model);
        ApplyDefaultAddress(model);
    }

    public async Task<CheckoutProcessResult> ProcessPaymentAsync(string email, CheckoutVM vm, string expectedCurrency, CancellationToken ct = default)
    {
        var user = await userService.GetByEmailAsync(email, ct);
        if (user == null)
        {
            return CheckoutProcessResult.Fail("Your cart could not be loaded.");
        }

        var cartItems = (await cartRepo.GetByUserIdAsync(user.PkRegisteredUserId)).ToList();
        if (cartItems.Count == 0)
        {
            return CheckoutProcessResult.Fail("Your cart is empty.");
        }

        var shippingMethod = await shippingService.GetShippingMethodByIdAsync(vm.SelectedShippingMethodId);
        if (shippingMethod == null || !shippingMethod.IsActive)
        {
            return CheckoutProcessResult.Fail("Invalid shipping method selected.");
        }

        var subtotal = cartItems.Sum(c => (c.Product?.GetEffectivePrice() ?? 0m) * c.Quantity);
        var tax = subtotal * 0.12m;
        var shipping = await shippingService.CalculateShippingCostAsync(vm.SelectedShippingMethodId, subtotal);
        var total = subtotal + tax + shipping;

        if (string.IsNullOrWhiteSpace(vm.PayPalOrderId))
        {
            return CheckoutProcessResult.Fail("PayPal payment verification is required before placing your order.");
        }

        PayPalVerificationResult paymentVerification;
        try
        {
            paymentVerification = await VerifyPayPalPaymentAsync(vm.PayPalOrderId, total, expectedCurrency, ct);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Rejected PayPal verification for order submission by user {Email}", email);
            return CheckoutProcessResult.Fail(ex.Message);
        }

        foreach (var item in cartItems)
        {
            if (item.Product == null || (item.Product.StockQuantity ?? 0) < item.Quantity)
            {
                return CheckoutProcessResult.Fail("One or more items in your cart are out of stock.");
            }
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            var contact = await ResolveCheckoutContactAsync(user.PkRegisteredUserId, vm, ct);

            var order = new OrderModel
            {
                FkContactId = contact.PkContactId,
                FkRegisteredUserId = user.PkRegisteredUserId,
                OrderStatus = OrderStatus.Paid,
                TotalAmount = total,
                CreatedAt = DateTime.UtcNow,
                DeliveryStatus = DeliveryStatus.Pending,
                FkShippingMethodId = vm.SelectedShippingMethodId,
                ShippingMethodName = shippingMethod.Name,
                ShippingCost = shipping
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);

            db.Transactions.Add(CreateVerifiedTransaction(order, contact.PkContactId, shipping, paymentVerification));
            await db.SaveChangesAsync(ct);

            foreach (var cartItem in cartItems)
            {
                db.OrderItems.Add(new OrderItemModel
                {
                    FkOrderId = order.PkOrderId,
                    FkProductId = cartItem.FkProductID,
                    Quantity = cartItem.Quantity,
                    UnitPrice = cartItem.Product?.GetEffectivePrice() ?? 0m
                });

                if (!await TryReserveInventoryAsync(cartItem.FkProductID, cartItem.Quantity, ct))
                {
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync(ct);
                    }
                    return CheckoutProcessResult.Fail("One or more items in your cart are no longer available in the requested quantity.");
                }
            }

            await db.SaveChangesAsync(ct);
            if (transaction != null)
            {
                await transaction.CommitAsync(ct);
            }

            await cartRepo.ClearByUserIdAsync(user.PkRegisteredUserId);

            return CheckoutProcessResult.Ok(order.PkOrderId);
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(ct);
            }
            throw;
        }
    }

    public async Task<GuestCheckoutProcessResult> ProcessGuestPaymentAsync(GuestCheckoutVM vm, string expectedCurrency, string requestScheme, string requestHost, CancellationToken ct = default)
    {
        var cartItems = await guestCartService.GetCartItemsAsync();
        if (cartItems.Count == 0)
        {
            return GuestCheckoutProcessResult.Fail("Your cart is empty.");
        }

        var shippingMethod = await shippingService.GetShippingMethodByIdAsync(vm.SelectedShippingMethodId);
        if (shippingMethod == null || !shippingMethod.IsActive)
        {
            return GuestCheckoutProcessResult.Fail("Invalid shipping method selected.");
        }

        var subtotal = cartItems.Sum(i => i.UnitPrice * i.Quantity);
        var tax = subtotal * 0.12m;
        var shipping = await shippingService.CalculateShippingCostAsync(vm.SelectedShippingMethodId, subtotal);
        var total = decimal.Round(subtotal + tax + shipping, 2, MidpointRounding.AwayFromZero);

        if (string.IsNullOrWhiteSpace(vm.PayPalOrderId))
        {
            return GuestCheckoutProcessResult.Fail("PayPal payment verification is required before placing your order.");
        }

        PayPalVerificationResult paymentVerification;
        try
        {
            paymentVerification = await VerifyPayPalPaymentAsync(vm.PayPalOrderId, total, expectedCurrency, ct);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Rejected guest PayPal verification for {Email}", vm.Email);
            return GuestCheckoutProcessResult.Fail(ex.Message);
        }

        foreach (var item in cartItems)
        {
            var product = await db.Products.FindAsync(new object?[] { item.ProductId }, ct);
            if (product == null || (product.StockQuantity ?? 0) < item.Quantity)
            {
                return GuestCheckoutProcessResult.Fail("One or more items in your cart are out of stock.");
            }
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            var names = (vm.FullName ?? string.Empty).Split(' ', 2);
            var guestAccessToken = GenerateGuestAccessToken();
            var contact = new ContactDetailModel
            {
                FkRegisteredUserId = null,
                FirstName = names.Length > 0 ? names[0] : string.Empty,
                LastName = names.Length > 1 ? names[1] : string.Empty,
                PhoneNumber = vm.PhoneNumber ?? string.Empty,
                Street = vm.Street ?? string.Empty,
                City = vm.City ?? string.Empty,
                Province = vm.Province ?? string.Empty,
                PostCode = vm.PostalCode ?? string.Empty,
                Country = vm.Country ?? "Canada",
                IsDefault = false
            };

            db.ContactDetails.Add(contact);
            await db.SaveChangesAsync(ct);

            var order = new OrderModel
            {
                FkContactId = contact.PkContactId,
                ContactDetail = contact,
                FkRegisteredUserId = null,
                OrderStatus = OrderStatus.Paid,
                TotalAmount = total,
                CreatedAt = DateTime.UtcNow,
                DeliveryStatus = DeliveryStatus.Pending,
                FkShippingMethodId = vm.SelectedShippingMethodId,
                ShippingMethodName = shippingMethod.Name,
                ShippingCost = shipping,
                GuestAccessTokenHash = HashGuestAccessToken(guestAccessToken)
            };

            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);

            db.Transactions.Add(CreateVerifiedTransaction(order, contact.PkContactId, shipping, paymentVerification));
            await db.SaveChangesAsync(ct);

            foreach (var item in cartItems)
            {
                var product = await db.Products.FindAsync(new object?[] { item.ProductId }, ct);
                if (product == null)
                {
                    await transaction.RollbackAsync(ct);
                    return GuestCheckoutProcessResult.Fail("One or more items in your cart are out of stock.");
                }

                db.OrderItems.Add(new OrderItemModel
                {
                    FkOrderId = order.PkOrderId,
                    Order = order,
                    FkProductId = product.PkProductId,
                    Product = product,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                });

                if (!await TryReserveInventoryAsync(product.PkProductId, item.Quantity, ct))
                {
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync(ct);
                    }
                    return GuestCheckoutProcessResult.Fail("One or more items in your cart are no longer available in the requested quantity.");
                }
            }

            await db.SaveChangesAsync(ct);
            if (transaction != null)
            {
                await transaction.CommitAsync(ct);
            }

            await guestCartService.ClearCartAsync();

            var confirmationLink = BuildGuestConfirmationLink(requestScheme, requestHost, guestAccessToken);
            try
            {
                await orderEmailService.SendOrderConfirmationAsync(vm.Email, contact.FirstName, order.PkOrderId, confirmationLink);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send guest order confirmation email for order {OrderId}", order.PkOrderId);
            }

            return GuestCheckoutProcessResult.Ok(order.PkOrderId, guestAccessToken);
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(ct);
            }
            throw;
        }
    }

    private async Task ApplyTotalsAsync(CheckoutVM model)
    {
        model.Subtotal = model.Items.Sum(i => i.LineTotal);
        model.Tax = model.Subtotal * 0.12m;
        model.ShippingCost = await shippingService.CalculateShippingCostAsync(model.SelectedShippingMethodId, model.Subtotal);
        model.Total = model.Subtotal + model.Tax + model.ShippingCost;
    }

    private void ApplyDefaultAddress(CheckoutVM model)
    {
        var defaultAddress = model.SavedAddresses.FirstOrDefault(a => a.IsDefault);
        if (defaultAddress == null)
        {
            return;
        }

        model.SelectedContactId = defaultAddress.ContactId;
        model.FullName = $"{defaultAddress.FirstName} {defaultAddress.LastName}".Trim();
        model.Street = defaultAddress.Street;
        model.City = defaultAddress.City;
        model.Province = defaultAddress.Province;
        model.PostalCode = defaultAddress.PostalCode;
        model.Country = defaultAddress.Country;
        model.PhoneNumber = defaultAddress.PhoneNumber;
    }

    private async Task<ContactDetailModel> ResolveCheckoutContactAsync(int userId, CheckoutVM vm, CancellationToken ct)
    {
        if (vm.SelectedContactId is > 0)
        {
            var contact = await db.ContactDetails.FirstOrDefaultAsync(c => c.PkContactId == vm.SelectedContactId.Value && c.FkRegisteredUserId == userId, ct);
            if (contact != null)
            {
                return contact;
            }
        }

        var names = (vm.FullName ?? string.Empty).Split(' ', 2);
        var newContact = new ContactDetailModel
        {
            FkRegisteredUserId = userId,
            FirstName = names.Length > 0 ? names[0] : string.Empty,
            LastName = names.Length > 1 ? names[1] : string.Empty,
            PhoneNumber = vm.PhoneNumber ?? string.Empty,
            Street = vm.Street ?? string.Empty,
            City = vm.City ?? string.Empty,
            Province = vm.Province ?? string.Empty,
            PostCode = vm.PostalCode ?? string.Empty,
            Country = vm.Country ?? "Canada",
            IsDefault = false
        };

        db.ContactDetails.Add(newContact);
        await db.SaveChangesAsync(ct);
        return newContact;
    }

    private async Task<PayPalVerificationResult> VerifyPayPalPaymentAsync(string payPalOrderId, decimal expectedTotal, string expectedCurrency, CancellationToken ct)
    {
        var duplicateTransaction = await db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.PaymentOrderId == payPalOrderId || t.PaymentTransactionId == payPalOrderId, ct);

        if (duplicateTransaction != null)
        {
            throw new InvalidOperationException("This PayPal payment has already been used for another order.");
        }

        var verification = await payPalService.VerifyCapturedOrderAsync(payPalOrderId, expectedTotal, expectedCurrency);
        if (verification is null)
        {
            throw new InvalidOperationException("PayPal verification did not return a payment result.");
        }

        var duplicateCapture = await db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.PaymentTransactionId == verification.CaptureId || t.PaymentOrderId == verification.PayPalOrderId, ct);

        if (duplicateCapture != null)
        {
            throw new InvalidOperationException("This PayPal payment has already been used for another order.");
        }

        return verification;
    }

    private async Task<bool> TryReserveInventoryAsync(int productId, int quantity, CancellationToken ct)
    {
        if (quantity <= 0)
        {
            return false;
        }

        var product = await db.Products.FirstOrDefaultAsync(p => p.PkProductId == productId, ct);
        if (product == null || (product.StockQuantity ?? 0) < quantity)
        {
            return false;
        }

        product.StockQuantity -= quantity;
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private static TransactionModel CreateVerifiedTransaction(OrderModel order, int contactId, decimal shipping, PayPalVerificationResult verification)
    {
        return new TransactionModel
        {
            FkOrderId = order.PkOrderId,
            FkContactId = contactId,
            TransactionStatus = verification.Status,
            Amount = verification.Amount,
            Currency = verification.Currency,
            DeliveryFee = shipping,
            TransactionDate = verification.CapturedAtUtc ?? DateTime.UtcNow,
            PaymentOrderId = verification.PayPalOrderId,
            PaymentTransactionId = verification.CaptureId,
            PaymentProvider = "PayPal",
            PaymentCapturedAtUtc = verification.CapturedAtUtc,
            PayerId = verification.PayerId,
            PayerEmail = verification.PayerEmail,
            VerificationSummary = verification.VerificationSummaryJson
        };
    }

    private static string GenerateGuestAccessToken()
        => Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string HashGuestAccessToken(string token)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string BuildGuestConfirmationLink(string scheme, string host, string token)
    {
        var path = $"/Checkout/GuestConfirmation?token={Uri.EscapeDataString(token)}";
        return string.IsNullOrWhiteSpace(host) ? path : $"{scheme}://{host}{path}";
    }
}
