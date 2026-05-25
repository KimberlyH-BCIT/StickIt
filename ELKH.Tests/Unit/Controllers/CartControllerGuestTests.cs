using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;
using ELKH.Controllers;
using ELKH.Services;
using ELKH.ViewModels;

namespace ELKH.Tests.Unit.Controllers;

// TABLE OF CONTENTS
// - Index tests
// - AddToCart tests
// - Update tests
// - Remove tests
// - Clear tests

/// <summary>
/// Unit tests for CartController guest checkout functionality.
/// Tests hybrid authentication detection and routing to appropriate cart services.
/// </summary>
/// <remarks>
/// 1. Index tests
/// 2. AddToCart tests
/// 3. Update tests
/// 4. Remove tests
/// 5. Clear tests
/// </remarks>
public class CartControllerGuestTests
{
    private readonly Mock<ICartService> _mockCartService;
    private readonly Mock<IGuestCartService> _mockGuestCartService;
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<ILogger<CartController>> _mockLogger;
    private readonly CartController _controller;

    public CartControllerGuestTests()
    {
        _mockCartService = new Mock<ICartService>();
        _mockGuestCartService = new Mock<IGuestCartService>();
        _mockUserService = new Mock<IUserService>();
        _mockLogger = new Mock<ILogger<CartController>>();

        _controller = new CartController(
            _mockCartService.Object,
            _mockGuestCartService.Object,
            _mockUserService.Object,
            _mockLogger.Object);

        SetupControllerContext();
    }

    private void SetupControllerContext(bool isAuthenticated = false, string email = "")
    {
        var httpContext = new DefaultHttpContext();

        if (isAuthenticated && !string.IsNullOrEmpty(email))
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Name, email)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            httpContext.User = principal;
        }

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    #region Index Tests

    [Fact]
    public async Task Index_AsAuthenticatedUser_ShouldUseCartService()
    {
        // Arrange
        SetupControllerContext(isAuthenticated: true, email: "test@example.com");

        var cartItems = new List<ELKH.Models.CartModel>
        {
            new ELKH.Models.CartModel
            {
                PkCartId = 1,
                FkProductID = 1,
                Quantity = 2,
                Product = new ELKH.Models.ProductModel
                {
                    PkProductId = 1,
                    Name = "Test Product",
                    Price = 10.00m,
                    DiscountPercent = 0
                }
            }
        };

        _mockCartService.Setup(c => c.GetCartItemsAsync("test@example.com"))
            .ReturnsAsync(cartItems);

        // Act
        var result = await _controller.Index();

        // Assert
        result.Should().BeOfType<ViewResult>();
        var viewResult = result as ViewResult;
        var model = viewResult!.Model as CartVM;

        model.Should().NotBeNull();
        model!.Items.Should().HaveCount(1);

        _mockCartService.Verify(c => c.GetCartItemsAsync("test@example.com"), Times.Once);
        _mockGuestCartService.Verify(c => c.GetCartItemsAsync(), Times.Never);
    }

    [Fact]
    public async Task Index_AsGuest_ShouldUseGuestCartService()
    {
        // Arrange
        SetupControllerContext(isAuthenticated: false);

        var guestCartItems = new List<CartItemVM>
        {
            new CartItemVM
            {
                ProductId = 1,
                ProductName = "Test Product",
                UnitPrice = 10.00m,
                Quantity = 2,
                LineTotal = 20.00m
            }
        };

        _mockGuestCartService.Setup(g => g.GetCartItemsAsync())
            .ReturnsAsync(guestCartItems);

        // Act
        var result = await _controller.Index();

        // Assert
        result.Should().BeOfType<ViewResult>();
        var viewResult = result as ViewResult;
        var model = viewResult!.Model as CartVM;

        model.Should().NotBeNull();
        model!.Items.Should().HaveCount(1);
        model.Items.First().ProductName.Should().Be("Test Product");

        _mockGuestCartService.Verify(g => g.GetCartItemsAsync(), Times.Once);
        _mockCartService.Verify(c => c.GetCartItemsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Index_AsGuest_ShouldCalculateTaxAndShipping()
    {
        // Arrange
        SetupControllerContext(isAuthenticated: false);

        var guestCartItems = new List<CartItemVM>
        {
            new CartItemVM { ProductId = 1, UnitPrice = 30.00m, Quantity = 1, LineTotal = 30.00m }
        };

        _mockGuestCartService.Setup(g => g.GetCartItemsAsync())
            .ReturnsAsync(guestCartItems);

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = result as ViewResult;
        var model = viewResult!.Model as CartVM;

        model!.Subtotal.Should().Be(30.00m);
        model.Tax.Should().Be(3.60m); // 12%
        model.ShippingCost.Should().Be(5.99m); // Under $50 threshold
        model.Total.Should().Be(39.59m);
    }

    [Fact]
    public async Task Index_AsGuest_WithFreeShipping_ShouldNotChargeShipping()
    {
        // Arrange
        SetupControllerContext(isAuthenticated: false);

        var guestCartItems = new List<CartItemVM>
        {
            new CartItemVM { ProductId = 1, UnitPrice = 60.00m, Quantity = 1, LineTotal = 60.00m }
        };

        _mockGuestCartService.Setup(g => g.GetCartItemsAsync())
            .ReturnsAsync(guestCartItems);

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = result as ViewResult;
        var model = viewResult!.Model as CartVM;

        model!.Subtotal.Should().Be(60.00m);
        model.ShippingCost.Should().Be(0m); // Free shipping over $50
        model.Total.Should().Be(67.20m); // 60 + (60 * 0.12)
    }

    #endregion

    #region AddToCart Tests

    [Fact]
    public async Task AddToCart_AsAuthenticatedUser_ShouldUseCartService()
    {
        // Arrange
        SetupControllerContext(isAuthenticated: true, email: "test@example.com");

        var cartItems = new List<ELKH.Models.CartModel>
        {
            new ELKH.Models.CartModel { FkProductID = 1, Quantity = 3 }
        };

        _mockCartService.Setup(c => c.AddToCartAsync("test@example.com", 1, 2))
            .Returns(Task.CompletedTask);
        _mockCartService.Setup(c => c.GetCartItemsAsync("test@example.com"))
            .ReturnsAsync(cartItems);

        // Act
        var result = await _controller.AddToCart(1, 2);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();

        _mockCartService.Verify(c => c.AddToCartAsync("test@example.com", 1, 2), Times.Once);
        _mockGuestCartService.Verify(g => g.AddToCartAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task AddToCart_AsGuest_ShouldUseGuestCartService()
    {
        // Arrange
        SetupControllerContext(isAuthenticated: false);

        var guestCartItems = new List<CartItemVM>
        {
            new CartItemVM { ProductId = 1, Quantity = 2 }
        };

        _mockGuestCartService.Setup(g => g.AddToCartAsync(1, 2))
            .Returns(Task.CompletedTask);
        _mockGuestCartService.Setup(g => g.GetCartItemsAsync())
            .ReturnsAsync(guestCartItems);

        // Act
        var result = await _controller.AddToCart(1, 2);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();

        _mockGuestCartService.Verify(g => g.AddToCartAsync(1, 2), Times.Once);
        _mockCartService.Verify(c => c.AddToCartAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task AddToCart_AsGuest_WithAjaxRequest_ShouldReturnJson()
    {
        // Arrange
        SetupControllerContext(isAuthenticated: false);
        _controller.ControllerContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        var guestCartItems = new List<CartItemVM>
        {
            new CartItemVM { ProductId = 1, Quantity = 2 }
        };

        _mockGuestCartService.Setup(g => g.AddToCartAsync(1, 2))
            .Returns(Task.CompletedTask);
        _mockGuestCartService.Setup(g => g.GetCartItemsAsync())
            .ReturnsAsync(guestCartItems);

        // Act
        var result = await _controller.AddToCart(1, 2);

        // Assert
        result.Should().BeOfType<JsonResult>();
        var jsonResult = result as JsonResult;
        var value = jsonResult!.Value;

        value.Should().NotBeNull();
        // Check that response contains success and cartCount properties
        value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
        value.GetType().GetProperty("cartCount")!.GetValue(value).Should().Be(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AddToCart_WithInvalidQuantity_ShouldReturnBadRequest(int quantity)
    {
        // Act
        var result = await _controller.AddToCart(1, quantity);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("Quantity must be positive.");
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Update_AsAuthenticatedUser_ShouldUseCartService()
    {
        // Arrange
        SetupControllerContext(isAuthenticated: true, email: "test@example.com");

        _mockCartService.Setup(c => c.UpdateQuantityAsync("test@example.com", 1, 5))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Update(1, 5);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        var redirect = result as RedirectToActionResult;
        redirect!.ActionName.Should().Be("Index");

        _mockCartService.Verify(c => c.UpdateQuantityAsync("test@example.com", 1, 5), Times.Once);
        _mockGuestCartService.Verify(g => g.UpdateQuantityAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Update_AsGuest_ShouldUseGuestCartService()
    {
        // Arrange
        SetupControllerContext(isAuthenticated: false);

        _mockGuestCartService.Setup(g => g.UpdateQuantityAsync(1, 5))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Update(1, 5);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();

        _mockGuestCartService.Verify(g => g.UpdateQuantityAsync(1, 5), Times.Once);
        _mockCartService.Verify(c => c.UpdateQuantityAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    public async Task Update_WithInvalidQuantity_ShouldEnforceMinimumOfOne(int inputQuantity, int expectedQuantity)
    {
        // Arrange
        SetupControllerContext(isAuthenticated: false);

        _mockGuestCartService.Setup(g => g.UpdateQuantityAsync(1, expectedQuantity))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Update(1, inputQuantity);

        // Assert
        _mockGuestCartService.Verify(g => g.UpdateQuantityAsync(1, expectedQuantity), Times.Once);
    }

    #endregion

    #region Remove Tests

    [Fact]
    public async Task Remove_AsAuthenticatedUser_ShouldUseCartService()
    {
        // Arrange
        SetupControllerContext(isAuthenticated: true, email: "test@example.com");

        _mockCartService.Setup(c => c.RemoveFromCartAsync("test@example.com", 1))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Remove(1);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();

        _mockCartService.Verify(c => c.RemoveFromCartAsync("test@example.com", 1), Times.Once);
        _mockGuestCartService.Verify(g => g.RemoveFromCartAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Remove_AsGuest_ShouldUseGuestCartService()
    {
        // Arrange
        SetupControllerContext(isAuthenticated: false);

        _mockGuestCartService.Setup(g => g.RemoveFromCartAsync(1))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Remove(1);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();

        _mockGuestCartService.Verify(g => g.RemoveFromCartAsync(1), Times.Once);
        _mockCartService.Verify(c => c.RemoveFromCartAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    #endregion

    #region Clear Tests

    [Fact]
    public async Task Clear_AsAuthenticatedUser_ShouldUseCartService()
    {
        // Arrange
        SetupControllerContext(isAuthenticated: true, email: "test@example.com");

        _mockCartService.Setup(c => c.ClearCartAsync("test@example.com"))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Clear();

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        _controller.TempData["Message"].Should().Be("success,Cart cleared.");

        _mockCartService.Verify(c => c.ClearCartAsync("test@example.com"), Times.Once);
        _mockGuestCartService.Verify(g => g.ClearCartAsync(), Times.Never);
    }

    [Fact]
    public async Task Clear_AsGuest_ShouldUseGuestCartService()
    {
        // Arrange
        SetupControllerContext(isAuthenticated: false);

        _mockGuestCartService.Setup(g => g.ClearCartAsync())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Clear();

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        _controller.TempData["Message"].Should().Be("success,Cart cleared.");

        _mockGuestCartService.Verify(g => g.ClearCartAsync(), Times.Once);
        _mockCartService.Verify(c => c.ClearCartAsync(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region BuyNow and PlaceOrder Tests

    [Fact]
    public async Task BuyNow_AsGuest_ShouldRedirectToLogin()
    {
        // Arrange
        SetupControllerContext(isAuthenticated: false);

        // Act
        var result = await _controller.BuyNow(1, 2);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        var redirect = result as RedirectToActionResult;
        redirect!.ActionName.Should().Be("Login");
        redirect.ControllerName.Should().Be("Account");
    }

    [Fact]
    public async Task PlaceOrder_AsGuest_ShouldRedirectToGuestCheckout()
    {
        // Arrange
        SetupControllerContext(isAuthenticated: false);

        // Act
        var result = await _controller.PlaceOrder();

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        var redirect = result as RedirectToActionResult;
        redirect!.ActionName.Should().Be("Guest");
        redirect.ControllerName.Should().Be("Checkout");
    }

    #endregion
}
