using FluentAssertions;
using ELKH.Controllers;
using ELKH.Models;
using ELKH.Services;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;
using Xunit;

namespace ELKH.Tests.Unit.Controllers;

public class CartControllerTests
{
    private readonly Mock<ICartService> _mockCartService = new();
    private readonly Mock<IGuestCartService> _mockGuestCartService = new();
    private readonly Mock<IUserService> _mockUserService = new();
    private readonly CartController _controller;

    public CartControllerTests()
    {
        _controller = new CartController(
            _mockCartService.Object,
            _mockGuestCartService.Object,
            _mockUserService.Object,
            NullLogger<CartController>.Instance);

        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
        _controller.ControllerContext = new ControllerContext(actionContext);
        _controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        SetupAuthenticatedUser("test@example.com");
    }

    [Fact]
    public async Task Index_ShouldReturnViewWithCartVmForAuthenticatedUser()
    {
        var cartItems = new List<CartModel>
        {
            new()
            {
                PkCartId = 10,
                FkProductID = 1,
                Quantity = 2,
                TotalPrice = 21.98m,
                Product = new ProductModel { Name = "Product 1", Price = 10.99m }
            },
            new()
            {
                PkCartId = 11,
                FkProductID = 2,
                Quantity = 1,
                TotalPrice = 15.99m,
                Product = new ProductModel { Name = "Product 2", Price = 15.99m }
            }
        };

        _mockCartService.Setup(c => c.GetCartItemsAsync("test@example.com"))
            .ReturnsAsync(cartItems);

        var result = await _controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<CartVM>().Subject;
        model.Items.Should().HaveCount(2);
        model.Items[0].ProductName.Should().Be("Product 1");
        model.Items[0].Quantity.Should().Be(2);
    }

    [Fact]
    public async Task Index_ShouldReturnViewWithCartVmForGuestUser()
    {
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var guestItems = new List<CartItemVM>
        {
            new()
            {
                ProductId = 1,
                ProductName = "Guest Product",
                UnitPrice = 9.99m,
                Quantity = 3,
                LineTotal = 29.97m
            }
        };

        _mockGuestCartService.Setup(c => c.GetCartItemsAsync())
            .ReturnsAsync(guestItems);

        var result = await _controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<CartVM>().Subject;
        model.Items.Should().HaveCount(1);
        model.Items[0].ProductName.Should().Be("Guest Product");
        model.Items[0].Quantity.Should().Be(3);
        _mockCartService.Verify(c => c.GetCartItemsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AddToCart_WithValidDataAndAjaxRequest_ShouldReturnSuccessJson()
    {
        _controller.ControllerContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        _controller.ControllerContext.HttpContext.User = BuildAuthenticatedPrincipal("test@example.com");

        _mockCartService.Setup(c => c.AddToCartAsync("test@example.com", 1, 2))
            .Returns(Task.CompletedTask);
        _mockCartService.Setup(c => c.GetCartItemsAsync("test@example.com"))
            .ReturnsAsync(new List<CartModel>
            {
                new() { Quantity = 2 }
            });

        var result = await _controller.AddToCart(1, 2);

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();
        value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
        value.GetType().GetProperty("cartCount")!.GetValue(value).Should().Be(2);
    }

    [Fact]
    public async Task AddToCart_WithValidDataAndGuestAjaxRequest_ShouldReturnSuccessJson()
    {
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        _controller.ControllerContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        _mockGuestCartService.Setup(c => c.AddToCartAsync(1, 2))
            .Returns(Task.CompletedTask);
        _mockGuestCartService.Setup(c => c.GetCartItemsAsync())
            .ReturnsAsync(new List<CartItemVM>
            {
                new() { Quantity = 2 }
            });

        var result = await _controller.AddToCart(1, 2);

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();
        value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
        value.GetType().GetProperty("cartCount")!.GetValue(value).Should().Be(2);
        _mockCartService.Verify(c => c.AddToCartAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AddToCart_WithInvalidQuantity_ShouldReturnBadRequest(int quantity)
    {
        var result = await _controller.AddToCart(1, quantity);

        result.Should().BeOfType<BadRequestObjectResult>();
        _mockCartService.Verify(c => c.AddToCartAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Index_WithAuthenticatedUserMissingEmail_ShouldRedirectToIdentityLogin()
    {
        var anonymousClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1")
        }, "Test"));

        _controller.ControllerContext.HttpContext.User = anonymousClaims;

        var result = await _controller.Index();

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Login");
        redirect.ControllerName.Should().Be("Account");
        redirect.RouteValues.Should().ContainKey("area");
        _mockCartService.Verify(c => c.GetCartItemsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Update_ShouldRedirectToIndex()
    {
        _controller.ControllerContext.HttpContext.User = BuildAuthenticatedPrincipal("test@example.com");

        var result = await _controller.Update(1, 3);

        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be(nameof(CartController.Index));
        _mockCartService.Verify(c => c.UpdateQuantityAsync("test@example.com", 1, 3), Times.Once);
    }

    [Fact]
    public async Task Remove_ShouldRedirectToIndex()
    {
        _controller.ControllerContext.HttpContext.User = BuildAuthenticatedPrincipal("test@example.com");

        var result = await _controller.Remove(1);

        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be(nameof(CartController.Index));
        _mockCartService.Verify(c => c.RemoveFromCartAsync("test@example.com", 1), Times.Once);
    }

    [Fact]
    public async Task Clear_ShouldRedirectToIndexAndSetMessage()
    {
        _controller.ControllerContext.HttpContext.User = BuildAuthenticatedPrincipal("test@example.com");

        var result = await _controller.Clear();

        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be(nameof(CartController.Index));
        _controller.TempData["Message"].Should().Be("success,Cart cleared.");
        _mockCartService.Verify(c => c.ClearCartAsync("test@example.com"), Times.Once);
    }

    private void SetupAuthenticatedUser(string email)
    {
        _controller.ControllerContext.HttpContext.User = BuildAuthenticatedPrincipal(email);
    }

    private static ClaimsPrincipal BuildAuthenticatedPrincipal(string email)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, email),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.NameIdentifier, "1")
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
