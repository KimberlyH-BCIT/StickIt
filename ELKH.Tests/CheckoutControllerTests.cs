using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ELKH.Configuration;
using ELKH.Controllers;
using ELKH.Data;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.Services;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ELKH.Tests;

/// <summary>
/// Unit tests for <see cref="CheckoutController.ProcessPayment"/>.
/// Verifies order creation, cart clearing, PayPal error handling, and edge cases.
/// </summary>
public class CheckoutControllerTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static ApplicationDbContext CreateDb(string name)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static CheckoutController BuildController(
        ApplicationDbContext db,
        IPayPalService payPal,
        IEnumerable<CartModel> cartItems,
        string email = "buyer@test.com")
    {
        var cartRepo = new Mock<ICartRepo>();
        cartRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<int>()))
                .ReturnsAsync(cartItems);

        var orderEmail = new Mock<IOrderEmailService>();
        orderEmail.Setup(e => e.SendOrderConfirmationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        // UserService resolves the current user from the DB via email — same lookup
        // the base controller helper performs, but through the cached service layer.
        // Use Returns(async) with a lambda so the DB is queried lazily when the mock
        // is invoked (not eagerly at setup time before SaveChangesAsync has run).
        var userSvc = new Mock<IUserService>();
        userSvc.Setup(u => u.GetByEmailAsync(email))
               .Returns(() => Task.FromResult(db.RegisteredUsers.FirstOrDefault(u => u.Email == email)));

        var opts = Options.Create(new PayPalOptions { Currency = "CAD" });

        var ctrl = new CheckoutController(
            db, userSvc.Object, cartRepo.Object, payPal, orderEmail.Object,
            NullLogger<CheckoutController>.Instance, opts);

        // Simulate an authenticated user with email claim
        var claims = new[]
        {
            new Claim(ClaimTypes.Name,  email),
            new Claim(ClaimTypes.Email, email)
        };
        var identity   = new ClaimsIdentity(claims, "Test");
        var principal  = new ClaimsPrincipal(identity);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return ctrl;
    }

    private static CartModel CartItem(decimal price = 10m, int qty = 2) =>
        new()
        {
            FkRegisteredUserId = 1,
            FkProductID        = 1,
            Quantity           = qty,
            TotalPrice         = price * qty,
            Product            = new ProductModel
            {
                PkProductId  = 1, Name = "Sticker", Price = price,
                StockQuantity = 10, IsActive = true
            }
        };

    // ── ProcessPayment tests ──────────────────────────────────────────────────

    [Fact]
    public async Task ProcessPayment_HappyPath_CreatesOrderClearsCart()
    {
        var db = CreateDb("Checkout_Happy");
        var user = new RegisteredUserModel { PkRegisteredUserId = 1, Email = "buyer@test.com" };
        db.RegisteredUsers.Add(user);
        var contact = new ContactDetailModel
        {
            PkContactId = 1, FkRegisteredUserId = 1,
            FirstName = "Jane", IsDefault = true
        };
        db.ContactDetails.Add(contact);
        var product = new ProductModel
        {
            PkProductId = 1, Name = "Sticker", Price = 10m,
            StockQuantity = 10, IsActive = true
        };
        db.Products.Add(product);
        var cartRow = new CartModel
        {
            FkRegisteredUserId = 1, FkProductID = 1, Quantity = 2, TotalPrice = 20m
        };
        db.Carts.Add(cartRow);
        await db.SaveChangesAsync();

        var payPal = new Mock<IPayPalService>();
        payPal.Setup(p => p.CreateOrderAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>()))
              .ReturnsAsync("PAYPAL-ORDER-ID");
        payPal.Setup(p => p.CaptureOrderAsync(It.IsAny<string>()))
              .Returns(Task.CompletedTask);

        var ctrl = BuildController(db, payPal.Object, new[] { cartRow });

        var result = await ctrl.ProcessPayment(new CheckoutVM());

        // Should redirect to Complete
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Complete", redirect.ActionName);

        // Order persisted
        Assert.Equal(1, await db.Orders.CountAsync());

        // Cart cleared
        Assert.Equal(0, await db.Carts.CountAsync());

        // Transaction recorded
        Assert.Equal(1, await db.Transactions.CountAsync());

        // Stock decremented
        var p = await db.Products.FindAsync(1);
        Assert.Equal(8, p!.StockQuantity);    // 10 - 2
    }

    [Fact]
    public async Task ProcessPayment_PayPalThrows_ReturnsViewWithError_NoOrderCreated()
    {
        var db = CreateDb("Checkout_PayPalFail");
        db.RegisteredUsers.Add(new RegisteredUserModel { PkRegisteredUserId = 1, Email = "buyer@test.com" });
        db.Products.Add(new ProductModel { PkProductId = 1, Name = "S", Price = 5m, StockQuantity = 3, IsActive = true });
        var cartRow = new CartModel { FkRegisteredUserId = 1, FkProductID = 1, Quantity = 1, TotalPrice = 5m };
        db.Carts.Add(cartRow);
        await db.SaveChangesAsync();

        var payPal = new Mock<IPayPalService>();
        payPal.Setup(p => p.CreateOrderAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>()))
              .ThrowsAsync(new Exception("network error"));

        var ctrl = BuildController(db, payPal.Object, new[] { cartRow });

        var result = await ctrl.ProcessPayment(new CheckoutVM());

        // Should re-render the checkout view, not redirect
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", view.ViewName);

        // No order created
        Assert.Equal(0, await db.Orders.CountAsync());

        // Cart unchanged
        Assert.Equal(1, await db.Carts.CountAsync());
    }

    [Fact]
    public async Task ProcessPayment_EmptyCart_RedirectsToCart()
    {
        var db = CreateDb("Checkout_EmptyCart");
        db.RegisteredUsers.Add(new RegisteredUserModel { PkRegisteredUserId = 1, Email = "buyer@test.com" });
        await db.SaveChangesAsync();

        var payPal = new Mock<IPayPalService>();
        var ctrl   = BuildController(db, payPal.Object, Array.Empty<CartModel>());

        var result = await ctrl.ProcessPayment(new CheckoutVM());

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Cart", redirect.ControllerName);
    }

    [Fact]
    public async Task ProcessPayment_InvalidModelState_ReturnsIndexView()
    {
        var db     = CreateDb("Checkout_InvalidModel");
        db.RegisteredUsers.Add(new RegisteredUserModel { PkRegisteredUserId = 1, Email = "buyer@test.com" });
        await db.SaveChangesAsync();

        var payPal = new Mock<IPayPalService>();
        var ctrl   = BuildController(db, payPal.Object, Array.Empty<CartModel>());
        ctrl.ModelState.AddModelError("card", "required");

        var result = await ctrl.ProcessPayment(new CheckoutVM());

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", view.ViewName);
        payPal.Verify(p => p.CreateOrderAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    /// <summary>
    /// ProcessPayment must recalculate pricing server-side.
    /// Even if the submitted CheckoutVM carries a manipulated Total the controller
    /// ignores it and derives the total from live cart data — verifying that the
    /// PayPal CreateOrderAsync call receives the correct server-calculated value.
    /// </summary>
    [Fact]
    public async Task ProcessPayment_PriceTampering_UsesServerSideCalculation()
    {
        const decimal unitPrice = 10m;
        const int     qty       = 2;

        var db = CreateDb("Checkout_PriceTamper");
        db.RegisteredUsers.Add(new RegisteredUserModel { PkRegisteredUserId = 1, Email = "buyer@test.com" });
        db.ContactDetails.Add(new ContactDetailModel { PkContactId = 1, FkRegisteredUserId = 1, FirstName = "Jane", IsDefault = true });
        db.Products.Add(new ProductModel { PkProductId = 1, Name = "Sticker", Price = unitPrice, StockQuantity = 10, IsActive = true });
        var cartRow = new CartModel { FkRegisteredUserId = 1, FkProductID = 1, Quantity = qty, TotalPrice = unitPrice * qty };
        db.Carts.Add(cartRow);
        await db.SaveChangesAsync();

        decimal capturedTotal = 0m;
        var payPal = new Mock<IPayPalService>();
        payPal.Setup(p => p.CreateOrderAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()))
              .Callback<decimal, string, string?>((t, _, _) => capturedTotal = t)
              .ReturnsAsync("PAYPAL-ORDER-ID");
        payPal.Setup(p => p.CaptureOrderAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        // Attempt to tamper: submit a VM with a suspiciously low total.
        var tamperedVm = new CheckoutVM { Total = 0.01m };
        var ctrl = BuildController(db, payPal.Object, new[] { cartRow });

        await ctrl.ProcessPayment(tamperedVm);

        // Server-calculated: subtotal = 20, tax = 2.40, shipping = 7.99 → 30.39
        var expectedSubtotal = unitPrice * qty;
        var expectedTotal    = expectedSubtotal + expectedSubtotal * 0.12m + 7.99m;
        Assert.Equal(expectedTotal, capturedTotal);
    }

    /// <summary>
    /// When the cart repo returns no items (empty enumerable) the controller
    /// must redirect to the Cart index without attempting any payment.
    /// </summary>
    [Fact]
    public async Task ProcessPayment_EmptyCartRepo_NeverCallsPayPal()
    {
        var db = CreateDb("Checkout_EmptyCartNoPay");
        db.RegisteredUsers.Add(new RegisteredUserModel { PkRegisteredUserId = 1, Email = "buyer@test.com" });
        await db.SaveChangesAsync();

        var payPal = new Mock<IPayPalService>();
        var ctrl   = BuildController(db, payPal.Object, Array.Empty<CartModel>());

        await ctrl.ProcessPayment(new CheckoutVM());

        payPal.Verify(p =>
            p.CreateOrderAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }
}
