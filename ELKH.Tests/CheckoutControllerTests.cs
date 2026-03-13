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

        var opts = Options.Create(new PayPalOptions { Currency = "CAD" });

        var ctrl = new CheckoutController(
            db, cartRepo.Object, payPal, orderEmail.Object,
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
        payPal.Setup(p => p.CreateOrderAsync(It.IsAny<decimal>(), It.IsAny<string>()))
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
        payPal.Setup(p => p.CreateOrderAsync(It.IsAny<decimal>(), It.IsAny<string>()))
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
        payPal.Verify(p => p.CreateOrderAsync(It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
    }
}
