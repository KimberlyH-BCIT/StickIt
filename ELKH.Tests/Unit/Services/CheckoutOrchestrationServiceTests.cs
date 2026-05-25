using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ELKH.Data;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.ViewModels;

namespace ELKH.Tests.Unit.Services;

// TABLE OF CONTENTS
// - ProcessPaymentAsync tests
// - ProcessGuestPaymentAsync tests
// - Shared checkout setup and defaults

/// <summary>
/// Unit tests for checkout orchestration and payment processing workflows.
/// </summary>
public class CheckoutOrchestrationServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<ICartRepo> _mockCartRepo;
    private readonly Mock<IContactDetailRepo> _mockContactDetailRepo;
    private readonly Mock<IGuestCartService> _mockGuestCartService;
    private readonly Mock<IShippingService> _mockShippingService;
    private readonly Mock<IPayPalService> _mockPayPalService;
    private readonly Mock<IOrderEmailService> _mockOrderEmailService;
    private readonly CheckoutOrchestrationService _service;

    public CheckoutOrchestrationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mockUserService = new Mock<IUserService>();
        _mockCartRepo = new Mock<ICartRepo>();
        _mockContactDetailRepo = new Mock<IContactDetailRepo>();
        _mockGuestCartService = new Mock<IGuestCartService>();
        _mockShippingService = new Mock<IShippingService>();
        _mockPayPalService = new Mock<IPayPalService>();
        _mockOrderEmailService = new Mock<IOrderEmailService>();

        _service = new CheckoutOrchestrationService(
            _context,
            _mockUserService.Object,
            _mockCartRepo.Object,
            _mockContactDetailRepo.Object,
            _mockGuestCartService.Object,
            _mockShippingService.Object,
            _mockPayPalService.Object,
            _mockOrderEmailService.Object,
            Mock.Of<ILogger<CheckoutOrchestrationService>>());

        SeedData();
        ConfigureDefaults();
    }

    [Fact]
    public async Task ProcessPaymentAsync_WithValidCheckout_ShouldCreateOrderAndClearCart()
    {
        _mockCartRepo.Setup(r => r.GetByUserIdAsync(1))
            .ReturnsAsync(new List<CartModel>
            {
                new()
                {
                    PkCartId = 1,
                    FkRegisteredUserId = 1,
                    FkProductID = 1,
                    Quantity = 2,
                    Product = _context.Products.Single(p => p.PkProductId == 1),
                    TotalPrice = 31.98m
                }
            });

        _mockShippingService.Setup(s => s.GetShippingMethodByIdAsync(1))
            .ReturnsAsync(new ShippingMethodModel { PkShippingMethodId = 1, Name = "Standard", IsActive = true, BasePrice = 7.99m });

        _mockPayPalService.Setup(p => p.VerifyCapturedOrderAsync("PAYPAL-1", It.IsAny<decimal>(), "CAD"))
            .ReturnsAsync(new PayPalVerificationResult
            {
                PayPalOrderId = "PAYPAL-1",
                CaptureId = "CAPTURE-1",
                Status = "COMPLETED",
                Amount = 43.81m,
                Currency = "CAD",
                CapturedAtUtc = DateTime.UtcNow,
                VerificationSummaryJson = "{\"status\":\"COMPLETED\"}"
            });

        var vm = new CheckoutVM
        {
            PayPalOrderId = "PAYPAL-1",
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

        _mockPayPalService.Setup(p => p.VerifyCapturedOrderAsync("PAYPAL-1", It.IsAny<decimal>(), "CAD"))
            .ReturnsAsync(new PayPalVerificationResult
            {
                PayPalOrderId = "PAYPAL-1",
                CaptureId = "CAPTURE-1",
                Status = "COMPLETED",
                Amount = 43.81m,
                Currency = "CAD",
                CapturedAtUtc = DateTime.UtcNow,
                VerificationSummaryJson = "{\"status\":\"COMPLETED\"}"
            });

        var result = await _service.ProcessPaymentAsync("test@example.com", vm, "CAD");

        result.Success.Should().BeTrue();
        result.OrderId.Should().BeGreaterThan(0);
        _mockCartRepo.Verify(r => r.ClearByUserIdAsync(1), Times.Once);

        var order = await _context.Orders.SingleAsync();
        order.OrderStatus.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task ProcessGuestPaymentAsync_WithValidCheckout_ShouldCreateOrderAndEmailConfirmation()
    {
        var vm = new GuestCheckoutVM
        {
            Email = "guest@example.com",
            PayPalOrderId = "PAYPAL-GUEST-1",
            FullName = "Jane Doe",
            PhoneNumber = "604-555-0100",
            Street = "123 Test St",
            City = "Vancouver",
            Province = "BC",
            PostalCode = "V6B 1A1",
            Country = "Canada",
            SelectedShippingMethodId = 1
        };

        _mockGuestCartService.Setup(g => g.GetCartItemsAsync())
            .ReturnsAsync(new List<CartItemVM>
            {
                new() { ProductId = 1, ProductName = "Test Sticker", UnitPrice = 15.99m, Quantity = 1, LineTotal = 15.99m }
            });

        _mockPayPalService.Setup(p => p.VerifyCapturedOrderAsync("PAYPAL-GUEST-1", It.IsAny<decimal>(), "CAD"))
            .ReturnsAsync(new PayPalVerificationResult
            {
                PayPalOrderId = "PAYPAL-GUEST-1",
                CaptureId = "CAPTURE-GUEST-1",
                Status = "COMPLETED",
                Amount = 25.90m,
                Currency = "CAD",
                CapturedAtUtc = DateTime.UtcNow,
                VerificationSummaryJson = "{\"status\":\"COMPLETED\"}"
            });

        var result = await _service.ProcessGuestPaymentAsync(vm, "CAD", "https", "example.com");

        result.Success.Should().BeTrue();
        result.GuestAccessToken.Should().NotBeNullOrWhiteSpace();
        _mockGuestCartService.Verify(g => g.ClearCartAsync(), Times.Once);
        _mockOrderEmailService.Verify(
            e => e.SendOrderConfirmationAsync("guest@example.com", "Jane", It.IsAny<int>(), It.Is<string>(link => link.Contains("/Checkout/GuestConfirmation?token="))),
            Times.Once);
    }

    [Fact]
    public async Task GetGuestOrderByAccessTokenAsync_WithMatchingToken_ShouldReturnGuestOrderWithDetails()
    {
        var token = "guest-token-123";
        var hash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

        var contact = new ContactDetailModel
        {
            PkContactId = 1,
            FirstName = "Guest",
            LastName = "Buyer",
            PhoneNumber = "604-555-0100",
            Street = "123 Test St",
            City = "Vancouver",
            Province = "BC",
            PostCode = "V6B 1A1",
            Country = "Canada"
        };

        var order = new OrderModel
        {
            PkOrderId = 42,
            FkContactId = contact.PkContactId,
            ContactDetail = contact,
            GuestAccessTokenHash = hash,
            FkRegisteredUserId = null,
            OrderItems = new List<OrderItemModel>
            {
                new()
                {
                    FkOrderId = 42,
                    FkProductId = 1,
                    Product = _context.Products.Single(p => p.PkProductId == 1),
                    Quantity = 1,
                    UnitPrice = 15.99m
                }
            }
        };

        _context.ContactDetails.Add(contact);
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var result = await _service.GetGuestOrderByAccessTokenAsync(token);

        result.Should().NotBeNull();
        result!.PkOrderId.Should().Be(42);
        result.ContactDetail.Should().NotBeNull();
        result.OrderItems.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetGuestOrderByAccessTokenAsync_WithEmptyToken_ShouldReturnNull()
    {
        var result = await _service.GetGuestOrderByAccessTokenAsync(string.Empty);

        result.Should().BeNull();
    }

    private void ConfigureDefaults()
    {
        _mockUserService.Setup(u => u.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegisteredUserModel { PkRegisteredUserId = 1, Email = "test@example.com" });

        _mockCartRepo.Setup(r => r.GetByUserIdAsync(1))
            .ReturnsAsync(() =>
                _context.Carts.Include(c => c.Product).Where(c => c.FkRegisteredUserId == 1).ToList());

        _mockContactDetailRepo.Setup(r => r.GetAllByUserIdAsync(1))
            .ReturnsAsync(new List<ContactDetailModel>
            {
                new()
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
                }
            });

        _mockShippingService.Setup(s => s.GetAvailableShippingMethodsAsync())
            .ReturnsAsync(new List<ShippingMethodModel>
            {
                new() { PkShippingMethodId = 1, Name = "Standard", IsActive = true, BasePrice = 7.99m }
            });

        _mockShippingService.Setup(s => s.GetShippingMethodByIdAsync(1))
            .ReturnsAsync(new ShippingMethodModel { PkShippingMethodId = 1, Name = "Standard", IsActive = true, BasePrice = 7.99m });

        _mockShippingService.Setup(s => s.CalculateShippingCostAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>()))
            .ReturnsAsync((int _, decimal subtotal, decimal threshold) => subtotal >= threshold ? 0m : 7.99m);
    }

    private void SeedData()
    {
        _context.RegisteredUsers.Add(new RegisteredUserModel { PkRegisteredUserId = 1, Email = "test@example.com" });
        _context.Categories.Add(new CategoryModel { PkCategoryId = 1, CategoryName = "Stickers" });
        _context.Products.Add(new ProductModel
        {
            PkProductId = 1,
            Name = "Test Sticker",
            Price = 15.99m,
            StockQuantity = 5,
            IsActive = true,
            FkCategoryId = 1
        });
        _context.Products.Add(new ProductModel
        {
            PkProductId = 2,
            Name = "Test Sticker 2",
            Price = 25.99m,
            DiscountPercent = 10m,
            StockQuantity = 5,
            IsActive = true,
            FkCategoryId = 1
        });
        _context.Carts.Add(new CartModel
        {
            PkCartId = 1,
            FkRegisteredUserId = 1,
            FkProductID = 1,
            Quantity = 2,
            TotalPrice = 31.98m
        });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();
}