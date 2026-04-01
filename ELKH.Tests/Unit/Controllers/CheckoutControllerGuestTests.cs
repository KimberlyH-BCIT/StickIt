using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
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

        _controller = new CheckoutController(
            _context,
            _mockCartRepo.Object,
            _mockContactDetailRepo.Object,
            _mockCartService.Object,
            _mockGuestCartService.Object,
            _mockConfiguration.Object);

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
        _controller.TempData["Message"].Should().Contain("empty");
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
        _controller.TempData["Message"].Should().Contain("empty");
    }

    [Fact]
    public async Task ProcessGuestPayment_WithOutOfStockItem_ShouldRedirectWithError()
    {
        // Arrange
        var validModel = CreateValidGuestCheckoutVM();

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
        _controller.TempData["Message"].Should().Contain("out of stock");
    }

    [Fact]
    public async Task ProcessGuestPayment_WithValidData_ShouldCreateOrderAndRedirect()
    {
        // Arrange
        var validModel = CreateValidGuestCheckoutVM();

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

        // Verify order was created
        var order = await _context.Orders.FirstOrDefaultAsync();
        order.Should().NotBeNull();
        order!.FkRegisteredUserId.Should().Be(0); // Guest order
        order.OrderStatus.Should().Be("Paid");
        order.DeliveryStatus.Should().Be("Pending");

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
    }

    [Fact]
    public async Task ProcessGuestPayment_ShouldCalculateTotalsServerSide()
    {
        // Arrange
        var validModel = CreateValidGuestCheckoutVM();

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
    public async Task ProcessGuestPayment_ShouldStoreGuestEmailInTempData()
    {
        // Arrange
        var validModel = CreateValidGuestCheckoutVM();

        var cartItems = new List<CartItemVM>
        {
            new CartItemVM { ProductId = 1, UnitPrice = 15.99m, Quantity = 1, LineTotal = 15.99m }
        };

        _mockGuestCartService.Setup(g => g.GetCartItemsAsync())
            .ReturnsAsync(cartItems);

        // Act
        await _controller.ProcessGuestPayment(validModel);

        // Assert
        _controller.TempData["GuestOrderEmail"].Should().Be("jane.doe@example.com");
    }

    #endregion

    #region GuestConfirmation Tests

    [Fact]
    public async Task GuestConfirmation_WithInvalidOrderId_ShouldRedirectToHome()
    {
        // Act
        var result = await _controller.GuestConfirmation(999, "test@example.com");

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        var redirect = result as RedirectToActionResult;
        redirect!.ActionName.Should().Be("Index");
        redirect.ControllerName.Should().Be("Home");
        _controller.TempData["Message"].Should().Contain("not found");
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
            FkRegisteredUserId = 0, // Guest order
            OrderStatus = "Paid",
            TotalAmount = 43.81m,
            CreatedAt = DateTime.UtcNow,
            DeliveryStatus = "Pending"
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

        _controller.TempData["GuestOrderEmail"] = "jane.doe@example.com";

        // Act
        var result = await _controller.GuestConfirmation(1, "jane.doe@example.com");

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
    public async Task GuestConfirmation_WithMismatchedEmail_ShouldRedirectToHome()
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
            FkRegisteredUserId = 0,
            OrderStatus = "Paid",
            TotalAmount = 43.81m
        };

        _context.ContactDetails.Add(contact);
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        _controller.TempData["GuestOrderEmail"] = "correct@example.com";

        // Act
        var result = await _controller.GuestConfirmation(1, "wrong@example.com");

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        _controller.TempData["Message"].Should().Contain("Unauthorized");
    }

    #endregion

    #region Helper Methods

    private GuestCheckoutVM CreateValidGuestCheckoutVM()
    {
        return new GuestCheckoutVM
        {
            Email = "jane.doe@example.com",
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

    #endregion

    public void Dispose()
    {
        _context?.Dispose();
    }
}
