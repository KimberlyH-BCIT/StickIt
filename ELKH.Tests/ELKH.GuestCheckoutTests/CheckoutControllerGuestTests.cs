using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using ELKH.Controllers;
using ELKH.Data;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.Services;
using ELKH.ViewModels;

namespace ELKH.GuestCheckoutTests;

/// <summary>
/// Unit tests for CheckoutController guest checkout functionality.
/// Tests guest checkout flow, order creation, and confirmation.
/// </summary>
public class CheckoutControllerGuestTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ICartRepo> _mockCartRepo;
    private readonly Mock<IContactDetailRepo> _mockContactDetailRepo;
    private readonly Mock<ICartService> _mockCartService;
    private readonly Mock<IGuestCartService> _mockGuestCartService;
    private readonly Mock<ICheckoutOrchestrationService> _mockCheckoutOrchestrationService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IShippingService> _mockShippingService;
    private readonly Mock<IPayPalService> _mockPayPalService;
    private readonly Mock<IOrderEmailService> _mockOrderEmailService;
    private readonly Mock<IUrlHelper> _mockUrlHelper;
    private readonly Mock<ILogger<CheckoutController>> _mockLogger;
    private readonly CheckoutController _controller;

    public CheckoutControllerGuestTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new ApplicationDbContext(options);

        _mockCartRepo = new Mock<ICartRepo>();
        _mockContactDetailRepo = new Mock<IContactDetailRepo>();
        _mockCartService = new Mock<ICartService>();
        _mockGuestCartService = new Mock<IGuestCartService>();
        _mockCheckoutOrchestrationService = new Mock<ICheckoutOrchestrationService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockShippingService = new Mock<IShippingService>();
        _mockPayPalService = new Mock<IPayPalService>();
        _mockOrderEmailService = new Mock<IOrderEmailService>();
        _mockUrlHelper = new Mock<IUrlHelper>();
        _mockLogger = new Mock<ILogger<CheckoutController>>();

        _controller = new CheckoutController(
            _context,
            _mockCartRepo.Object,
            _mockContactDetailRepo.Object,
            _mockCartService.Object,
            _mockGuestCartService.Object,
            _mockCheckoutOrchestrationService.Object,
            _mockConfiguration.Object,
            _mockShippingService.Object,
            _mockPayPalService.Object,
            _mockOrderEmailService.Object,
            _mockLogger.Object);

        SetupControllerContext();
        SeedTestData();
    }

    private void SetupControllerContext()
    {
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        _controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        _mockUrlHelper.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns((string?)null);
        _controller.Url = _mockUrlHelper.Object;
    }

    private void SeedTestData()
    {
        var products = new List<ProductModel>
        {
            new ProductModel
            {
                PkProductId = 1,
                Name = "Test Sticker 1",
                Price = 15.99m,
                DiscountPercent = 0,
                StockQuantity = 50,
                IsActive = true,
                FkCategoryId = 1
            },
            new ProductModel
            {
                PkProductId = 2,
                Name = "Test Sticker 2",
                Price = 25.99m,
                DiscountPercent = 10,
                StockQuantity = 30,
                IsActive = true,
                FkCategoryId = 1
            }
        };

        _context.Products.AddRange(products);
        _context.SaveChanges();

        _mockShippingService.Setup(s => s.GetShippingMethodByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => new ShippingMethodModel
            {
                PkShippingMethodId = id,
                Name = id == 1 ? "Standard" : "Express",
                BasePrice = id == 1 ? 7.99m : 14.99m,
                IsActive = true
            });

        _mockShippingService.Setup(s => s.CalculateShippingCostAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>()))
            .ReturnsAsync((int id, decimal subtotal, decimal freeShippingThreshold) => id == 1 && subtotal >= freeShippingThreshold ? 0m : (id == 1 ? 7.99m : 14.99m));

        _mockShippingService.Setup(s => s.GetAvailableShippingMethodsAsync())
            .ReturnsAsync(new List<ShippingMethodModel>
            {
                new() { PkShippingMethodId = 1, Name = "Standard", BasePrice = 7.99m, IsActive = true },
                new() { PkShippingMethodId = 2, Name = "Express", BasePrice = 14.99m, IsActive = true }
            });

        _mockGuestCartService.Setup(g => g.ClearCartAsync())
            .Returns(Task.CompletedTask);

        _mockConfiguration.Setup(c => c["PayPal:Currency"]).Returns("CAD");
    }

    private void SetupVerifiedPayment(decimal amount, string orderId = "PAYPAL-ORDER-1", string captureId = "CAPTURE-1")
    {
        _mockPayPalService
            .Setup(p => p.VerifyCapturedOrderAsync(
                orderId,
                It.Is<decimal>(total => decimal.Round(total, 2, MidpointRounding.AwayFromZero) == amount),
                "CAD"))
            .ReturnsAsync(new PayPalVerificationResult
            {
                PayPalOrderId = orderId,
                CaptureId = captureId,
                Status = "COMPLETED",
                Amount = amount,
                Currency = "CAD",
                CapturedAtUtc = DateTime.UtcNow,
                PayerId = "PAYER-123",
                PayerEmail = "payer@example.com",
                VerificationSummaryJson = "{\"status\":\"COMPLETED\"}"
            });
    }

    #region Guest GET Tests

    [Fact]
    public async Task Guest_WithEmptyCart_ShouldRedirectToCart()
    {
        // Arrange
        _mockGuestCartService.Setup(g => g.GetCartItemsAsync())
            .ReturnsAsync(new List<CartItemVM>());

        // Act
        var result = await _controller.Guest();

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        var redirect = result as RedirectToActionResult;
        redirect!.ActionName.Should().Be("Index");
        redirect.ControllerName.Should().Be("Cart");
        _controller.TempData["Message"]?.ToString().Should().Contain("empty");
    }

    [Fact]
    public async Task Guest_WithCartItems_ShouldReturnViewWithModel()
    {
        // Arrange
        var cartItems = new List<CartItemVM>
        {
            new CartItemVM
            {
                ProductId = 1,
                ProductName = "Test Sticker 1",
                UnitPrice = 15.99m,
                Quantity = 2,
                LineTotal = 31.98m
            }
        };

        _mockGuestCartService.Setup(g => g.GetCartItemsAsync())
            .ReturnsAsync(cartItems);

        _mockConfiguration.Setup(c => c["PayPal:ClientId"])
            .Returns("test-paypal-client-id");

        // Act
        var result = await _controller.Guest();

        // Assert
        result.Should().BeOfType<ViewResult>();
        var viewResult = result as ViewResult;
        var model = viewResult!.Model as GuestCheckoutVM;

        model.Should().NotBeNull();
        model!.Items.Should().HaveCount(1);
        model.Subtotal.Should().Be(31.98m);
        // 12% tax calculation may have precision differences
        model.Tax.Should().BeApproximately(3.84m, 0.01m);
        model.ShippingCost.Should().Be(7.99m); // Under $50
        model.Total.Should().BeApproximately(43.81m, 0.01m);
        model.PayPalClientId.Should().Be("test-paypal-client-id");
    }

    [Fact]
    public async Task Guest_WithFreeShippingThreshold_ShouldNotChargeShipping()
    {
        // Arrange
        var cartItems = new List<CartItemVM>
        {
            new CartItemVM
            {
                ProductId = 1,
                ProductName = "Test Sticker 1",
                UnitPrice = 60.00m,
                Quantity = 1,
                LineTotal = 60.00m
            }
        };

        _mockGuestCartService.Setup(g => g.GetCartItemsAsync())
            .ReturnsAsync(cartItems);

        // Act
        var result = await _controller.Guest();

        // Assert
        var viewResult = result as ViewResult;
        var model = viewResult!.Model as GuestCheckoutVM;

        model!.ShippingCost.Should().Be(0m); // Free shipping over $50
    }

    #endregion

    #region ProcessGuestPayment Tests

    [Fact]
    public async Task ProcessGuestPayment_WithInvalidModel_ShouldReturnViewWithErrors()
    {
        // Arrange
        var invalidModel = new GuestCheckoutVM
        {
            // Missing required fields
            Email = "",
            FullName = ""
        };

        _controller.ModelState.AddModelError("Email", "Email is required");
        _mockConfiguration.Setup(c => c["PayPal:ClientId"]).Returns("test-id");

        // Act
        var result = await _controller.ProcessGuestPayment(invalidModel);

        // Assert
        result.Should().BeOfType<ViewResult>();
        var viewResult = result as ViewResult;
        viewResult!.ViewName.Should().Be("Guest");
        viewResult.Model.Should().Be(invalidModel);
    }

    [Fact]
    public async Task ProcessGuestPayment_WithEmptyCart_ShouldRedirectToCart()
    {
        // Arrange
        var validModel = CreateValidGuestCheckoutVM();

        _mockGuestCartService.Setup(g => g.GetCartItemsAsync())
            .ReturnsAsync(new List<CartItemVM>());

        // Act
        var result = await _controller.ProcessGuestPayment(validModel);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        _controller.TempData["Message"]?.ToString().Should().Contain("empty");
    }

    [Fact]
    public async Task ProcessGuestPayment_WithOutOfStockItem_ShouldRedirectWithError()
    {
        // Arrange
        var validModel = CreateValidGuestCheckoutVM();
        SetupVerifiedPayment(1790.88m);

        var cartItems = new List<CartItemVM>
        {
            new CartItemVM { ProductId = 1, Quantity = 100 } // Exceeds stock
        };

        _mockGuestCartService.Setup(g => g.GetCartItemsAsync())
            .ReturnsAsync(cartItems);

        // Act
        var result = await _controller.ProcessGuestPayment(validModel);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        _controller.TempData["Message"]?.ToString().Should().Contain("out of stock");
    }

    [Fact]
    public async Task ProcessGuestPayment_WithValidData_ShouldCreateOrderAndRedirect()
    {
        // Arrange
        var validModel = CreateValidGuestCheckoutVM();
        SetupVerifiedPayment(43.81m);

        var cartItems = new List<CartItemVM>
        {
            new CartItemVM
            {
                ProductId = 1,
                ProductName = "Test Sticker 1",
                UnitPrice = 15.99m,
                Quantity = 2,
                LineTotal = 31.98m
            }
        };

        _mockGuestCartService.Setup(g => g.GetCartItemsAsync())
            .ReturnsAsync(cartItems);

        _mockGuestCartService.Setup(g => g.ClearCartAsync())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ProcessGuestPayment(validModel);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        var redirect = result as RedirectToActionResult;
        redirect!.ActionName.Should().Be("GuestConfirmation");
        redirect.RouteValues.Should().ContainKey("token");
        var token = redirect.RouteValues!["token"]?.ToString();
        token.Should().NotBeNullOrWhiteSpace();

        // Verify order was created
        var order = await _context.Orders.FirstOrDefaultAsync();
        order.Should().NotBeNull();
        order!.FkRegisteredUserId.Should().BeNull();
        order.OrderStatus.Should().Be(OrderStatus.Paid);
        order.DeliveryStatus.Should().Be(DeliveryStatus.Pending);
        order.GuestAccessTokenHash.Should().NotBeNullOrWhiteSpace();

        var transaction = await _context.Transactions.FirstOrDefaultAsync();
        transaction.Should().NotBeNull();
        transaction!.PaymentTransactionId.Should().Be("CAPTURE-1");
        transaction.PaymentOrderId.Should().Be("PAYPAL-ORDER-1");
        transaction.TransactionStatus.Should().Be("COMPLETED");

        // Verify contact detail was created
        var contact = await _context.ContactDetails.FirstOrDefaultAsync();
        contact.Should().NotBeNull();
        contact!.FirstName.Should().Be("Jane");
        contact.LastName.Should().Be("Doe");

        // Verify order items were created
        var orderItems = await _context.OrderItems.ToListAsync();
        orderItems.Should().HaveCount(1);
        orderItems.First().Quantity.Should().Be(2);

        // Verify inventory was decremented
        var product = await _context.Products.FindAsync(1);
        product!.StockQuantity.Should().Be(48); // 50 - 2

        // Verify cart was cleared
        _mockGuestCartService.Verify(g => g.ClearCartAsync(), Times.Once);
        _mockOrderEmailService.Verify(
            o => o.SendOrderConfirmationAsync(
                validModel.Email,
                "Jane",
                order.PkOrderId,
                It.Is<string>(link => link.Contains("/Checkout/GuestConfirmation?token=") && link.Contains(token!))),
            Times.Once);
    }

    [Fact]
    public async Task ProcessGuestPayment_ShouldCalculateTotalsServerSide()
    {
        // Arrange
        var validModel = CreateValidGuestCheckoutVM();
        SetupVerifiedPayment(43.81m);

        var cartItems = new List<CartItemVM>
        {
            new CartItemVM { ProductId = 1, UnitPrice = 15.99m, Quantity = 2, LineTotal = 31.98m }
        };

        _mockGuestCartService.Setup(g => g.GetCartItemsAsync())
            .ReturnsAsync(cartItems);

        // Act
        var result = await _controller.ProcessGuestPayment(validModel);

        // Assert
        var order = await _context.Orders.FirstOrDefaultAsync();

        // Subtotal: 31.98
        // Tax: 31.98 * 0.12 = 3.8376 (precision differences expected)
        // Shipping: 7.99 (under $50)
        // Total: 31.98 + 3.8376 + 7.99 = 43.8076
        order!.TotalAmount.Should().BeApproximately(43.81m, 0.01m);
    }

    [Fact]
    public async Task ProcessGuestPayment_WithMultipleItems_ShouldCreateAllOrderItems()
    {
        // Arrange
        var validModel = CreateValidGuestCheckoutVM();
        SetupVerifiedPayment(62.02m);

        var cartItems = new List<CartItemVM>
        {
            new CartItemVM { ProductId = 1, UnitPrice = 15.99m, Quantity = 2, LineTotal = 31.98m },
            new CartItemVM { ProductId = 2, UnitPrice = 23.39m, Quantity = 1, LineTotal = 23.39m } // 25.99 - 10%
        };

        _mockGuestCartService.Setup(g => g.GetCartItemsAsync())
            .ReturnsAsync(cartItems);

        // Act
        var result = await _controller.ProcessGuestPayment(validModel);

        // Assert
        var orderItems = await _context.OrderItems.ToListAsync();
        orderItems.Should().HaveCount(2);

        orderItems.Should().Contain(oi => oi.FkProductId == 1 && oi.Quantity == 2);
        orderItems.Should().Contain(oi => oi.FkProductId == 2 && oi.Quantity == 1);
    }

    [Fact]
    public async Task ProcessGuestPayment_ShouldSendSecureConfirmationLinkEmail()
    {
        // Arrange
        var validModel = CreateValidGuestCheckoutVM();
        SetupVerifiedPayment(25.90m);

        var cartItems = new List<CartItemVM>
        {
            new CartItemVM { ProductId = 1, UnitPrice = 15.99m, Quantity = 1, LineTotal = 15.99m }
        };

        _mockGuestCartService.Setup(g => g.GetCartItemsAsync())
            .ReturnsAsync(cartItems);

        // Act
        var result = await _controller.ProcessGuestPayment(validModel);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        _controller.TempData.ContainsKey("GuestOrderEmail").Should().BeFalse();
        _mockOrderEmailService.Verify(
            o => o.SendOrderConfirmationAsync(
                validModel.Email,
                "Jane",
                It.IsAny<int>(),
                It.Is<string>(link => link.Contains("/Checkout/GuestConfirmation?token="))),
            Times.Once);
    }

    [Fact]
    public async Task ProcessGuestPayment_WithMissingPayPalOrderId_ShouldRedirectWithError()
    {
        var model = CreateValidGuestCheckoutVM();
        model.PayPalOrderId = null;

        var cartItems = new List<CartItemVM>
        {
            new CartItemVM { ProductId = 1, UnitPrice = 15.99m, Quantity = 1, LineTotal = 15.99m }
        };

        _mockGuestCartService.Setup(g => g.GetCartItemsAsync())
            .ReturnsAsync(cartItems);

        var result = await _controller.ProcessGuestPayment(model);

        result.Should().BeOfType<RedirectToActionResult>();
        _controller.TempData["Message"]?.ToString().Should().Contain("PayPal payment verification is required");
        _mockPayPalService.Verify(p => p.VerifyCapturedOrderAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessGuestPayment_WithFailedCapture_ShouldRedirectWithError()
    {
        var model = CreateValidGuestCheckoutVM();
        var cartItems = new List<CartItemVM>
        {
            new() { ProductId = 1, UnitPrice = 15.99m, Quantity = 2, LineTotal = 31.98m }
        };

        _mockGuestCartService.Setup(g => g.GetCartItemsAsync()).ReturnsAsync(cartItems);
        _mockPayPalService
            .Setup(p => p.VerifyCapturedOrderAsync("PAYPAL-ORDER-1", It.IsAny<decimal>(), "CAD"))
            .ThrowsAsync(new InvalidOperationException("PayPal capture for order PAYPAL-ORDER-1 is not completed. Current status: DECLINED."));

        var result = await _controller.ProcessGuestPayment(model);

        result.Should().BeOfType<RedirectToActionResult>();
        _controller.TempData["Message"]?.ToString().Should().Contain("not completed");
        (await _context.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ProcessGuestPayment_WithTamperedTotal_ShouldRejectOrder()
    {
        var model = CreateValidGuestCheckoutVM();
        model.SelectedShippingMethodId = 2;
        var cartItems = new List<CartItemVM>
        {
            new() { ProductId = 1, UnitPrice = 15.99m, Quantity = 2, LineTotal = 31.98m }
        };

        _mockGuestCartService.Setup(g => g.GetCartItemsAsync()).ReturnsAsync(cartItems);
        _mockPayPalService
            .Setup(p => p.VerifyCapturedOrderAsync("PAYPAL-ORDER-1", It.IsAny<decimal>(), "CAD"))
            .ThrowsAsync(new InvalidOperationException("PayPal amount mismatch for order PAYPAL-ORDER-1. Expected 50.81, received 43.81."));

        var result = await _controller.ProcessGuestPayment(model);

        result.Should().BeOfType<RedirectToActionResult>();
        _controller.TempData["Message"]?.ToString().Should().Contain("amount mismatch");
        (await _context.Transactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ProcessGuestPayment_WithDuplicateCapture_ShouldRejectOrder()
    {
        var model = CreateValidGuestCheckoutVM();
        var cartItems = new List<CartItemVM>
        {
            new() { ProductId = 1, UnitPrice = 15.99m, Quantity = 2, LineTotal = 31.98m }
        };

        _mockGuestCartService.Setup(g => g.GetCartItemsAsync()).ReturnsAsync(cartItems);

        _context.Transactions.Add(new TransactionModel
        {
            PaymentOrderId = "PAYPAL-ORDER-1",
            PaymentTransactionId = "CAPTURE-OLD",
            TransactionStatus = "COMPLETED",
            Amount = 43.81m,
            Currency = "CAD",
            VerificationSummary = "{}"
        });
        await _context.SaveChangesAsync();

        var result = await _controller.ProcessGuestPayment(model);

        result.Should().BeOfType<RedirectToActionResult>();
        _controller.TempData["Message"]?.ToString().Should().Contain("already been used");
        _mockPayPalService.Verify(p => p.VerifyCapturedOrderAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessGuestPayment_WithMismatchedAmountFromVerification_ShouldRejectOrder()
    {
        var model = CreateValidGuestCheckoutVM();
        var cartItems = new List<CartItemVM>
        {
            new() { ProductId = 1, UnitPrice = 15.99m, Quantity = 2, LineTotal = 31.98m }
        };

        _mockGuestCartService.Setup(g => g.GetCartItemsAsync()).ReturnsAsync(cartItems);
        _mockPayPalService
            .Setup(p => p.VerifyCapturedOrderAsync("PAYPAL-ORDER-1", It.IsAny<decimal>(), "CAD"))
            .ThrowsAsync(new InvalidOperationException("PayPal amount mismatch for order PAYPAL-ORDER-1. Expected 43.81, received 41.00."));

        var result = await _controller.ProcessGuestPayment(model);

        result.Should().BeOfType<RedirectToActionResult>();
        _controller.TempData["Message"]?.ToString().Should().Contain("amount mismatch");
        (await _context.Orders.CountAsync()).Should().Be(0);
    }

    #endregion

    #region GuestConfirmation Tests

    [Fact]
    public async Task GuestConfirmation_WithInvalidToken_ShouldRedirectToHome()
    {
        // Act
        var result = await _controller.GuestConfirmation("invalid-token");

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        var redirect = result as RedirectToActionResult;
        redirect!.ActionName.Should().Be("Index");
        redirect.ControllerName.Should().Be("Home");
        _controller.TempData["Message"]?.ToString().Should().Contain("Invalid guest order access link");
    }

    [Fact]
    public async Task GuestConfirmation_WithValidOrder_ShouldReturnView()
    {
        // Arrange
        var contact = new ContactDetailModel
        {
            PkContactId = 1,
            FkRegisteredUserId = null,
            FirstName = "Jane",
            LastName = "Doe",
            PhoneNumber = "604-555-0100",
            Street = "123 Test St",
            City = "Vancouver",
            Province = "BC",
            PostCode = "V6B 1A1",
            Country = "Canada"
        };

        // First save the contact to get the PK set
        _context.ContactDetails.Add(contact);
        await _context.SaveChangesAsync();

        var order = new OrderModel
        {
            PkOrderId = 1,
            FkContactId = contact.PkContactId, // Use the generated PK
            FkRegisteredUserId = null,
            OrderStatus = OrderStatus.Paid,
            TotalAmount = 43.81m,
            CreatedAt = DateTime.UtcNow,
            DeliveryStatus = DeliveryStatus.Pending,
            ContactDetail = contact, // Set navigation property
            GuestAccessTokenHash = HashGuestAccessToken("guest-token")
        };

        // Save order first to generate PK
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var product = await _context.Products.FindAsync(1);

        var orderItem = new OrderItemModel
        {
            FkOrderId = order.PkOrderId, // Use generated PK
            FkProductId = 1,
            Quantity = 2,
            UnitPrice = 15.99m,
            Product = product,
            Order = order // Set navigation property
        };

        _context.OrderItems.Add(orderItem);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GuestConfirmation("guest-token");

        // Assert
        result.Should().BeOfType<ViewResult>();
        var viewResult = result as ViewResult;
        var model = viewResult!.Model as OrderModel;

        model.Should().NotBeNull();
        model!.PkOrderId.Should().Be(order.PkOrderId);
        model.TotalAmount.Should().Be(43.81m);
        model.ContactDetail.Should().NotBeNull();
        model.OrderItems.Should().HaveCount(1);
    }

    [Fact]
    public async Task GuestConfirmation_WithInvalidTokenForExistingOrder_ShouldRedirectToHome()
    {
        // Arrange
        var contact = new ContactDetailModel
        {
            PkContactId = 1,
            FirstName = "Jane",
            LastName = "Doe"
        };

        var order = new OrderModel
        {
            PkOrderId = 1,
            FkContactId = 1,
            FkRegisteredUserId = null,
            OrderStatus = OrderStatus.Paid,
            TotalAmount = 43.81m,
            GuestAccessTokenHash = HashGuestAccessToken("expected-token")
        };

        _context.ContactDetails.Add(contact);
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GuestConfirmation("wrong-token");

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        _controller.TempData["Message"]?.ToString().Should().Contain("Invalid guest order access link");
    }

    #endregion

    #region Helper Methods

    private GuestCheckoutVM CreateValidGuestCheckoutVM()
    {
        return new GuestCheckoutVM
        {
            Email = "jane.doe@example.com",
            PayPalOrderId = "PAYPAL-ORDER-1",
            PayPalPayerId = "PAYER-123",
            SelectedShippingMethodId = 1,
            FullName = "Jane Doe",
            PhoneNumber = "604-555-0100",
            Street = "123 Test St",
            City = "Vancouver",
            Province = "BC",
            PostalCode = "V6B 1A1",
            Country = "Canada",
            SubscribeToNewsletter = false,
            CreateAccount = false
        };
    }

    private static string HashGuestAccessToken(string token)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    #endregion

    public void Dispose()
    {
        _context?.Dispose();
    }
}
