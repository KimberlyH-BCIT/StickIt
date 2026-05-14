using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
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

namespace ELKH.Tests.Unit.Controllers;

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
            .Options;
        _context = new ApplicationDbContext(options);

        _mockCartRepo = new Mock<ICartRepo>();
        _mockContactDetailRepo = new Mock<IContactDetailRepo>();
        _mockCartService = new Mock<ICartService>();
        _mockGuestCartService = new Mock<IGuestCartService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockShippingService = new Mock<IShippingService>();
        _mockPayPalService = new Mock<IPayPalService>();
        _mockOrderEmailService = new Mock<IOrderEmailService>();
        _mockUrlHelper = new Mock<IUrlHelper>();
        _mockLogger = new Mock<ILogger<CheckoutController>>();

        var shippingMethods = new List<ShippingMethodModel>
        {
            new()
            {
                PkShippingMethodId = 1,
                Name = "Standard",
                IsActive = true,
                BasePrice = 7.99m
            }
        };

        _mockShippingService.Setup(s => s.GetAvailableShippingMethodsAsync())
            .ReturnsAsync(shippingMethods);
        _mockShippingService.Setup(s => s.GetShippingMethodByIdAsync(1))
            .ReturnsAsync(shippingMethods[0]);
        _mockShippingService.Setup(s => s.CalculateShippingCostAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>()))
            .ReturnsAsync((int _, decimal subtotal, decimal threshold) => subtotal >= threshold ? 0m : 7.99m);

        _controller = new CheckoutController(
            _context,
            _mockCartRepo.Object,
            _mockContactDetailRepo.Object,
            _mockCartService.Object,
            _mockGuestCartService.Object,
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
                Name = "Standard",
                IsActive = true,
                BasePrice = 7.99m
            });

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
        model.Tax.Should().Be(3.84m); // 12% of 31.98
        model.ShippingCost.Should().Be(7.99m); // Under $50
        model.Total.Should().Be(43.81m);
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

        // Verify order was created
        var order = await _context.Orders.FirstOrDefaultAsync();
        order.Should().NotBeNull();
        order!.FkRegisteredUserId.Should().BeNull();
        order.OrderStatus.Should().Be(OrderStatus.Paid);
        order.DeliveryStatus.Should().Be(DeliveryStatus.Pending);
        order.GuestAccessTokenHash.Should().NotBeNullOrWhiteSpace();

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
            o => o.SendOrderConfirmationAsync(validModel.Email, "Jane", order.PkOrderId, It.Is<string>(link => link.Contains("/Checkout/GuestConfirmation?token="))),
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
        // Tax: 31.98 * 0.12 = 3.84
        // Shipping: 7.99 (under $50)
        // Total: 31.98 + 3.84 + 7.99 = 43.81
        order!.TotalAmount.Should().Be(43.81m);
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
            o => o.SendOrderConfirmationAsync(validModel.Email, "Jane", It.IsAny<int>(), It.Is<string>(link => link.Contains("/Checkout/GuestConfirmation?token="))),
            Times.Once);
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

        var order = new OrderModel
        {
            PkOrderId = 1,
            FkContactId = 1,
            FkRegisteredUserId = null,
            OrderStatus = OrderStatus.Paid,
            TotalAmount = 43.81m,
            CreatedAt = DateTime.UtcNow,
            DeliveryStatus = DeliveryStatus.Pending,
            GuestAccessTokenHash = HashGuestAccessToken("guest-token")
        };

        var product = await _context.Products.FindAsync(1);
        
        var orderItem = new OrderItemModel
        {
            FkOrderId = 1,
            FkProductId = 1,
            Quantity = 2,
            UnitPrice = 15.99m,
            Product = product
        };

        _context.ContactDetails.Add(contact);
        _context.Orders.Add(order);
        _context.OrderItems.Add(orderItem);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GuestConfirmation("guest-token");

        // Assert
        result.Should().BeOfType<ViewResult>();
        var viewResult = result as ViewResult;
        var model = viewResult!.Model as OrderModel;
        
        model.Should().NotBeNull();
        model!.PkOrderId.Should().Be(1);
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
            FullName = "Jane Doe",
            PhoneNumber = "604-555-0100",
            Street = "123 Test St",
            City = "Vancouver",
            Province = "BC",
            PostalCode = "V6B 1A1",
            Country = "Canada",
            SelectedShippingMethodId = 1,
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
