using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;
using Xunit;
using ELKH.Controllers;
using ELKH.Services;
using ELKH.ViewModels;

namespace ELKH.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for WishlistController functionality.
/// Tests wishlist operations and user interactions.
/// </summary>
public class WishlistControllerTests
{
    private readonly Mock<IWishlistService> _mockWishlistService;
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<ApplicationDbContext> _mockDbContext;
    private readonly Mock<ILogger<WishlistController>> _mockLogger;
    private readonly WishlistController _controller;

    public WishlistControllerTests()
    {
        // Setup mocks
        _mockWishlistService = new Mock<IWishlistService>();
        _mockUserService = new Mock<IUserService>();
        _mockLogger = new Mock<ILogger<WishlistController>>();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _mockDbContext = new Mock<ApplicationDbContext>(options);

        // Create controller under test
        _controller = new WishlistController(
            _mockWishlistService.Object,
            _mockUserService.Object,
            _mockLogger.Object,
            _mockDbContext.Object);

        // Setup controller context
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
        _controller.ControllerContext = new ControllerContext(actionContext);
        _controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        
        // Setup authenticated user
        SetupAuthenticatedUser("test@example.com");
    }

    [Fact]
    public async Task Index_ShouldReturnViewWithWishlistItems()
    {
        // Arrange
        var wishlistItems = new List<WishlistVM>
        {
            new WishlistVM { ProductId = 1, ProductName = "Product 1", Price = 10.99m },
            new WishlistVM { ProductId = 2, ProductName = "Product 2", Price = 15.99m }
        };

        _mockWishlistService.Setup(w => w.GetWishlistItemsAsync("test@example.com"))
                           .ReturnsAsync(wishlistItems);

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableToType<IEnumerable<WishlistVM>>().Subject;
        model.Should().HaveCount(2);
        model.First().ProductName.Should().Be("Product 1");
    }

    [Fact]
    public async Task AddToWishlist_WithValidProductId_ShouldReturnSuccessJson()
    {
        // Arrange
        _mockWishlistService.Setup(w => w.AddToWishlistAsync("test@example.com", 1))
                           .ReturnsAsync(true);

        // Act
        var result = await _controller.AddToWishlist(1);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();
        
        var successProperty = value!.GetType().GetProperty("success");
        successProperty.Should().NotBeNull();
        successProperty!.GetValue(value).Should().Be(true);
    }

    [Fact]
    public async Task AddToWishlist_WithInvalidProductId_ShouldReturnFailureJson()
    {
        // Arrange
        _mockWishlistService.Setup(w => w.AddToWishlistAsync("test@example.com", 999))
                           .ReturnsAsync(false);

        // Act
        var result = await _controller.AddToWishlist(999);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();
        
        var successProperty = value!.GetType().GetProperty("success");
        successProperty!.GetValue(value).Should().Be(false);
    }

    [Fact]
    public async Task RemoveFromWishlist_WithValidId_ShouldReturnSuccessJson()
    {
        // Arrange
        _mockWishlistService.Setup(w => w.RemoveFromWishlistAsync(1))
                           .ReturnsAsync(true);

        // Act
        var result = await _controller.RemoveFromWishlist(1);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();
        
        var successProperty = value!.GetType().GetProperty("success");
        successProperty!.GetValue(value).Should().Be(true);
    }

    [Fact]
    public async Task RemoveFromWishlist_WithInvalidId_ShouldReturnFailureJson()
    {
        // Arrange
        _mockWishlistService.Setup(w => w.RemoveFromWishlistAsync(999))
                           .ReturnsAsync(false);

        // Act
        var result = await _controller.RemoveFromWishlist(999);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();
        
        var successProperty = value!.GetType().GetProperty("success");
        successProperty!.GetValue(value).Should().Be(false);
    }

    [Fact]
    public async Task CheckIsInWishlist_WithProductInWishlist_ShouldReturnTrue()
    {
        // Arrange
        _mockWishlistService.Setup(w => w.IsInWishlistAsync("test@example.com", 1))
                           .ReturnsAsync(true);

        // Act
        var result = await _controller.CheckIsInWishlist(1);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();
        
        var isInWishlistProperty = value!.GetType().GetProperty("isInWishlist");
        isInWishlistProperty.Should().NotBeNull();
        isInWishlistProperty!.GetValue(value).Should().Be(true);
    }

    [Fact]
    public async Task CheckIsInWishlist_WithProductNotInWishlist_ShouldReturnFalse()
    {
        // Arrange
        _mockWishlistService.Setup(w => w.IsInWishlistAsync("test@example.com", 1))
                           .ReturnsAsync(false);

        // Act
        var result = await _controller.CheckIsInWishlist(1);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();
        
        var isInWishlistProperty = value!.GetType().GetProperty("isInWishlist");
        isInWishlistProperty!.GetValue(value).Should().Be(false);
    }

    [Fact]
    public async Task GetWishlistCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var wishlistItems = new List<WishlistVM>
        {
            new WishlistVM(),
            new WishlistVM(),
            new WishlistVM()
        };

        _mockWishlistService.Setup(w => w.GetWishlistItemsAsync("test@example.com"))
                           .ReturnsAsync(wishlistItems);

        // Act
        var result = await _controller.GetWishlistCount();

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();
        
        var countProperty = value!.GetType().GetProperty("count");
        countProperty!.GetValue(value).Should().Be(3);
    }

    private void SetupAuthenticatedUser(string email)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, email),
            new Claim(ClaimTypes.NameIdentifier, "1")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        
        _controller.ControllerContext.HttpContext.User = principal;
    }
}