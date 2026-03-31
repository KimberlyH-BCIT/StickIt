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
using ELKH.Models;

namespace ELKH.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for OrderController functionality.
/// Tests order management, history, and tracking operations.
/// </summary>
public class OrderControllerTests
{
    private readonly Mock<IOrderManagementRepo> _mockOrderManagementRepo;
    private readonly Mock<IProductService> _mockProductService;
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<ApplicationDbContext> _mockDbContext;
    private readonly OrderController _controller;

    public OrderControllerTests()
    {
        // Setup mocks
        _mockOrderManagementRepo = new Mock<IOrderManagementRepo>();
        _mockProductService = new Mock<IProductService>();
        _mockUserService = new Mock<IUserService>();

        // Setup DbContext mock
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _mockDbContext = new Mock<ApplicationDbContext>(options);

        // Create controller under test
        _controller = new OrderController(
            _mockOrderManagementRepo.Object,
            _mockProductService.Object,
            _mockUserService.Object,
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
    public async Task Index_WithAdminRole_ShouldReturnViewWithAllOrders()
    {
        // Arrange
        SetupAdminUser("admin@example.com", "Admin");
        var orders = new List<OrderModel>
        {
            new OrderModel { PkOrderId = 1, TotalAmount = 25.99m, CreatedAt = DateTime.UtcNow },
            new OrderModel { PkOrderId = 2, TotalAmount = 45.99m, CreatedAt = DateTime.UtcNow.AddDays(-1) }
        };

        _mockOrderManagementRepo.Setup(o => o.GetAllOrdersAsync())
                               .ReturnsAsync(orders);

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableToType<IEnumerable<OrderModel>>().Subject;
        model.Should().HaveCount(2);
    }

    [Fact]
    public async Task MyHistory_ShouldReturnViewWithUserOrders()
    {
        // Arrange
        var orders = new List<OrderModel>
        {
            new OrderModel { PkOrderId = 1, TotalAmount = 25.99m, CreatedAt = DateTime.UtcNow },
            new OrderModel { PkOrderId = 2, TotalAmount = 45.99m, CreatedAt = DateTime.UtcNow.AddDays(-1) }
        };

        _mockOrderManagementRepo.Setup(o => o.GetUserOrdersAsync("test@example.com"))
                               .ReturnsAsync(orders);

        // Act
        var result = await _controller.MyHistory();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.ViewName.Should().Be("~/Views/OrderHistory/History.cshtml");
        var model = viewResult.Model.Should().BeOfType<OrderHistoryVM>().Subject;
        model.Orders.Should().HaveCount(2);
        model.CurrentSort.Should().Be("date_desc");
    }

    [Fact]
    public async Task MyHistory_WithSortParameter_ShouldReturnSortedOrders()
    {
        // Arrange
        var orders = new List<OrderModel>
        {
            new OrderModel { PkOrderId = 1, TotalAmount = 25.99m, CreatedAt = DateTime.UtcNow },
            new OrderModel { PkOrderId = 2, TotalAmount = 45.99m, CreatedAt = DateTime.UtcNow.AddDays(-1) }
        };

        _mockOrderManagementRepo.Setup(o => o.GetUserOrdersAsync("test@example.com"))
                               .ReturnsAsync(orders);

        // Act
        var result = await _controller.MyHistory("total_high");

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<OrderHistoryVM>().Subject;
        model.CurrentSort.Should().Be("total_high");
    }

    [Fact]
    public async Task Details_WithValidOrderId_ShouldReturnViewWithOrderDetails()
    {
        // Arrange
        var order = new OrderModel 
        { 
            PkOrderId = 1, 
            TotalAmount = 29.99m,
            RegisteredUser = new RegisteredUserProfile { Email = "test@example.com" }
        };

        _mockOrderManagementRepo.Setup(o => o.GetOrderWithDetailsAsync(1))
                               .ReturnsAsync(order);

        // Act
        var result = await _controller.Details(1);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<OrderModel>().Subject;
        model.PkOrderId.Should().Be(1);
        model.TotalAmount.Should().Be(29.99m);
    }

    [Fact]
    public async Task Details_WithInvalidOrderId_ShouldReturnNotFound()
    {
        // Arrange
        _mockOrderManagementRepo.Setup(o => o.GetOrderWithDetailsAsync(999))
                               .ReturnsAsync((OrderModel?)null);

        // Act
        var result = await _controller.Details(999);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Details_WithUnauthorizedUser_ShouldReturnForbid()
    {
        // Arrange
        var order = new OrderModel 
        { 
            PkOrderId = 1, 
            TotalAmount = 29.99m,
            RegisteredUser = new RegisteredUserProfile { Email = "other@example.com" }  // Different user
        };

        _mockOrderManagementRepo.Setup(o => o.GetOrderWithDetailsAsync(1))
                               .ReturnsAsync(order);

        // Act
        var result = await _controller.Details(1);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Details_WithInvalidId_ShouldReturnNotFound(int id)
    {
        // Act
        var result = await _controller.Details(id);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
        _mockOrderManagementRepo.Verify(o => o.GetOrderWithDetailsAsync(It.IsAny<int>()), Times.Never);
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

    private void SetupAdminUser(string email, string role)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, email),
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext.HttpContext.User = principal;
    }
}