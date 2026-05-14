using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;
using ELKH.Controllers;
using ELKH.Data;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.Services;
using ELKH.ViewModels;

namespace ELKH.Tests.Unit.Controllers;

public class CheckoutControllerTests : IDisposable
{
    private const decimal ExpectedSubtotal = 31.82m;
    private const decimal ExpectedTax = 3.8184m;
    private const decimal ExpectedShipping = 7.99m;
    private const decimal ExpectedTotal = ExpectedSubtotal + ExpectedTax + ExpectedShipping;

    private readonly SqliteConnection _connection;
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

    public CheckoutControllerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

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

        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
        _controller.ControllerContext = new ControllerContext(actionContext);
        _controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        _mockUrlHelper.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns((string?)null);
        _controller.Url = _mockUrlHelper.Object;

        SetupAuthenticatedUser("test@example.com");
        SeedData();
        ConfigureDefaults();
    }

    [Fact]
    public async Task ProcessPayment_WithInvalidShippingMethod_ShouldRedirectToIndexWithError()
    {
        var vm = CreateValidCheckoutVm();
        _mockShippingService.Setup(s => s.GetShippingMethodByIdAsync(vm.SelectedShippingMethodId))
            .ReturnsAsync((ShippingMethodModel?)null);

        var result = await _controller.ProcessPayment(vm);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        _controller.TempData["Message"]?.ToString().Should().Be("error,Invalid shipping method selected.");
        _mockPayPalService.Verify(
            p => p.VerifyCapturedOrderAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>()),
            Times.Never);
        _context.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessPayment_WithoutPayPalOrderId_ShouldRedirectToIndexWithError()
    {
        var vm = CreateValidCheckoutVm();
        vm.PayPalOrderId = null;

        var result = await _controller.ProcessPayment(vm);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        _controller.TempData["Message"]?.ToString()
            .Should().Be("error,PayPal payment verification is required before placing your order.");
        _mockPayPalService.Verify(
            p => p.VerifyCapturedOrderAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>()),
            Times.Never);
        _context.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessPayment_WithRejectedPayPalVerification_ShouldNotCreatePaidOrder()
    {
        var vm = CreateValidCheckoutVm();
        _mockPayPalService.Setup(p => p.VerifyCapturedOrderAsync(vm.PayPalOrderId!, ExpectedTotal, "CAD"))
            .ThrowsAsync(new InvalidOperationException("Captured amount does not match the server-calculated total."));

        var result = await _controller.ProcessPayment(vm);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        _controller.TempData["Message"]?.ToString()
            .Should().Be("error,Captured amount does not match the server-calculated total.");
        _context.Orders.Should().BeEmpty();
        _context.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessPayment_WithVerifiedPayPalCapture_ShouldCreatePaidOrderAndDecrementStock()
    {
        var vm = CreateValidCheckoutVm();
        _mockPayPalService.Setup(p => p.VerifyCapturedOrderAsync(vm.PayPalOrderId!, ExpectedTotal, "CAD"))
            .ReturnsAsync(new PayPalVerificationResult
            {
                PayPalOrderId = vm.PayPalOrderId!,
                CaptureId = "CAPTURE-1",
                Status = "COMPLETED",
                Amount = ExpectedTotal,
                Currency = "CAD",
                CapturedAtUtc = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                PayerId = "PAYER-1",
                PayerEmail = "buyer@example.com",
                VerificationSummaryJson = "{\"status\":\"COMPLETED\"}"
            });

        var result = await _controller.ProcessPayment(vm);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Details");
        redirect.ControllerName.Should().Be("Order");

        var order = await _context.Orders.SingleAsync();
        order.OrderStatus.Should().Be(OrderStatus.Paid);
        order.TotalAmount.Should().Be(ExpectedTotal);
        order.FkRegisteredUserId.Should().Be(1);
        order.FkShippingMethodId.Should().Be(1);
        order.ShippingMethodName.Should().Be("Standard");
        order.ShippingCost.Should().Be(ExpectedShipping);

        var transaction = await _context.Transactions.SingleAsync();
        transaction.PaymentOrderId.Should().Be(vm.PayPalOrderId);
        transaction.PaymentTransactionId.Should().Be("CAPTURE-1");
        transaction.TransactionStatus.Should().Be("COMPLETED");
        transaction.Amount.Should().Be(ExpectedTotal);
        transaction.Currency.Should().Be("CAD");
        transaction.PayerEmail.Should().Be("buyer@example.com");

        var orderItem = await _context.OrderItems.SingleAsync();
        orderItem.FkProductId.Should().Be(1);
        orderItem.Quantity.Should().Be(2);
        orderItem.UnitPrice.Should().Be(15.91m);

        var product = await _context.Products.SingleAsync(p => p.PkProductId == 1);
        product.StockQuantity.Should().Be(3);
        _mockCartRepo.Verify(r => r.GetByUserIdAsync(1), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessPayment_WithContactOwnedByDifferentUser_ShouldRejectCheckout()
    {
        var vm = CreateValidCheckoutVm();
        vm.SelectedContactId = 99;

        _context.ContactDetails.Add(new ContactDetailModel
        {
            PkContactId = 99,
            FkRegisteredUserId = 2,
            FirstName = "Other",
            LastName = "User",
            PhoneNumber = "604-555-0199",
            Street = "999 Other St",
            City = "Victoria",
            Province = "BC",
            PostCode = "V8V 1A1",
            Country = "Canada"
        });
        await _context.SaveChangesAsync();

        _mockPayPalService.Setup(p => p.VerifyCapturedOrderAsync(vm.PayPalOrderId!, ExpectedTotal, "CAD"))
            .ReturnsAsync(new PayPalVerificationResult
            {
                PayPalOrderId = vm.PayPalOrderId!,
                CaptureId = "CAPTURE-OWNERSHIP",
                Status = "COMPLETED",
                Amount = ExpectedTotal,
                Currency = "CAD",
                CapturedAtUtc = DateTime.UtcNow,
                PayerId = "PAYER-OWNERSHIP",
                PayerEmail = "buyer@example.com",
                VerificationSummaryJson = "{\"status\":\"COMPLETED\"}"
            });

        var result = await _controller.ProcessPayment(vm);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        _controller.TempData["Message"]?.ToString()
            .Should().Be("error,Selected contact details could not be found for your account.");
        _context.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessPayment_WithInsufficientStock_ShouldRejectCheckout()
    {
        var vm = CreateValidCheckoutVm();
        var product = await _context.Products.SingleAsync(p => p.PkProductId == 1);
        product.StockQuantity = 1;
        await _context.SaveChangesAsync();

        _mockPayPalService.Setup(p => p.VerifyCapturedOrderAsync(vm.PayPalOrderId!, ExpectedTotal, "CAD"))
            .ReturnsAsync(new PayPalVerificationResult
            {
                PayPalOrderId = vm.PayPalOrderId!,
                CaptureId = "CAPTURE-STOCK",
                Status = "COMPLETED",
                Amount = ExpectedTotal,
                Currency = "CAD",
                CapturedAtUtc = DateTime.UtcNow,
                PayerId = "PAYER-STOCK",
                PayerEmail = "buyer@example.com",
                VerificationSummaryJson = "{\"status\":\"COMPLETED\"}"
            });

        var result = await _controller.ProcessPayment(vm);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        _controller.TempData["Message"]?.ToString()
            .Should().Be("error,One or more items in your cart are out of stock.");
        _context.Orders.Should().BeEmpty();
    }

    private void ConfigureDefaults()
    {
        _mockConfiguration.Setup(c => c["PayPal:Currency"]).Returns("CAD");
        _mockConfiguration.Setup(c => c["PayPal:ClientId"]).Returns("test-paypal-client-id");

        var shippingMethod = new ShippingMethodModel
        {
            PkShippingMethodId = 1,
            Name = "Standard",
            IsActive = true,
            BasePrice = 7.99m,
            DeliveryDaysMin = 3,
            DeliveryDaysMax = 5,
            Description = "Standard shipping"
        };

        _mockShippingService.Setup(s => s.GetAvailableShippingMethodsAsync())
            .ReturnsAsync(new List<ShippingMethodModel> { shippingMethod });
        _mockShippingService.Setup(s => s.GetShippingMethodByIdAsync(1))
            .ReturnsAsync(shippingMethod);
        _mockShippingService.Setup(s => s.CalculateShippingCostAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>()))
            .ReturnsAsync((int _, decimal subtotal, decimal threshold) => subtotal >= threshold ? 0m : 7.99m);

        _mockContactDetailRepo.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new ContactDetailModel
            {
                PkContactId = 1,
                FkRegisteredUserId = 1,
                FirstName = "Test",
                LastName = "User",
                PhoneNumber = "604-555-0100",
                Street = "123 Test St",
                City = "Vancouver",
                Province = "BC",
                PostCode = "V6B 1A1",
                Country = "Canada"
            });

        _mockCartRepo.Setup(r => r.GetByUserIdAsync(1))
            .ReturnsAsync(() =>
                _context.Carts
                    .Include(c => c.Product)
                    .Where(c => c.FkRegisteredUserId == 1)
                    .ToList());
    }

    private void SeedData()
    {
        var category = new CategoryModel
        {
            PkCategoryId = 1,
            CategoryName = "Stickers"
        };

        var user = new RegisteredUserModel
        {
            PkRegisteredUserId = 1,
            Email = "test@example.com"
        };

        var product = new ProductModel
        {
            PkProductId = 1,
            Name = "Test Sticker",
            NameNormalized = "test sticker",
            Description = "A test sticker",
            Price = 15.91m,
            DiscountPercent = 0m,
            StockQuantity = 5,
            IsActive = true,
            FkCategoryId = 1,
            Category = category
        };

        var cart = new CartModel
        {
            PkCartId = 1,
            FkRegisteredUserId = 1,
            RegisteredUser = user,
            FkProductID = 1,
            Product = product,
            Quantity = 2,
            TotalPrice = 31.82m
        };

        var contact = new ContactDetailModel
        {
            PkContactId = 1,
            FkRegisteredUserId = 1,
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "604-555-0100",
            Street = "123 Test St",
            City = "Vancouver",
            Province = "BC",
            PostCode = "V6B 1A1",
            Country = "Canada",
            IsDefault = true
        };

        _context.Categories.Add(category);
        _context.RegisteredUsers.Add(user);
        _context.Products.Add(product);
        _context.ContactDetails.Add(contact);
        _context.Carts.Add(cart);
        _context.SaveChanges();
    }

    private CheckoutVM CreateValidCheckoutVm()
    {
        return new CheckoutVM
        {
            PayPalOrderId = "PAYPAL-ORDER-1",
            SelectedContactId = 1,
            SelectedShippingMethodId = 1,
            FullName = "Test User",
            Street = "123 Test St",
            City = "Vancouver",
            Province = "BC",
            PostalCode = "V6B 1A1",
            Country = "Canada",
            PhoneNumber = "604-555-0100"
        };
    }

    private void SetupAuthenticatedUser(string email)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, email),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.NameIdentifier, "1")
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext.HttpContext.User = principal;
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
