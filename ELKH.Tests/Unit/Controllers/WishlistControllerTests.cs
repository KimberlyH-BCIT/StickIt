using FluentAssertions;
using ELKH.Controllers;
using ELKH.Data;
using ELKH.Models;
using ELKH.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;
using Xunit;

namespace ELKH.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for wishlist controller actions and Ajax responses.
/// </summary>
public class WishlistControllerTests
{
    private readonly Mock<IWishlistService> _mockWishlistService = new();
    private readonly Mock<IUserService> _mockUserService = new();
    private readonly ApplicationDbContext _db;
    private readonly WishlistController _controller;

    public WishlistControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);

        _controller = new WishlistController(
            _mockWishlistService.Object,
            _mockUserService.Object,
            NullLogger<WishlistController>.Instance,
            _db);

        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
        _controller.ControllerContext = new ControllerContext(actionContext);
        _controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        SetupAuthenticatedUser("test@example.com");
    }

    [Fact]
    public async Task Index_ShouldReturnViewWithWishlistItems()
    {
        var wishlistItems = new List<WishListItemModel>
        {
            new()
            {
                FkProductId = 1,
                Product = new ProductModel { Name = "Product 1", Price = 10.99m }
            },
            new()
            {
                FkProductId = 2,
                Product = new ProductModel { Name = "Product 2", Price = 15.99m }
            }
        };

        _mockWishlistService.Setup(w => w.GetItemsAsync("test@example.com", "date_desc"))
            .ReturnsAsync(wishlistItems);

        var result = await _controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableTo<IEnumerable<WishListItemModel>>().Subject;
        model.Should().HaveCount(2);
        model.First().Product.Name.Should().Be("Product 1");
    }

    [Fact]
    public async Task AddAjax_WithValidProductId_ShouldReturnSuccessJson()
    {
        _mockWishlistService.Setup(w => w.AddAsync("test@example.com", 1))
            .ReturnsAsync(new WishlistResult { Success = true, Message = "Added", Count = 1 });

        var result = await _controller.AddAjax(1);

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value.Should().NotBeNull().Subject;
        value.GetType().GetProperty("Success")!.GetValue(value).Should().Be(true);
        value.GetType().GetProperty("Count")!.GetValue(value).Should().Be(1);
    }

    [Fact]
    public async Task RemoveAjax_WithValidProductId_ShouldReturnSuccessJson()
    {
        _mockWishlistService.Setup(w => w.RemoveAsync("test@example.com", 1))
            .ReturnsAsync(new WishlistResult { Success = true, Message = "Removed", Count = 0 });

        var result = await _controller.RemoveAjax(1);

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value.Should().NotBeNull().Subject;
        value.GetType().GetProperty("Success")!.GetValue(value).Should().Be(true);
        value.GetType().GetProperty("Count")!.GetValue(value).Should().Be(0);
    }

    [Fact]
    public async Task Add_ShouldRedirectAndSetSuccessMessage()
    {
        _controller.ControllerContext.HttpContext.Request.Headers["Referer"] = "/Product/Details/1";
        _mockWishlistService.Setup(w => w.AddAsync("test@example.com", 1))
            .ReturnsAsync(new WishlistResult { Success = true, Count = 1 });

        var result = await _controller.Add(1);

        var redirectResult = result.Should().BeOfType<RedirectResult>().Subject;
        redirectResult.Url.Should().Be("/Product/Details/1");
        _controller.TempData["Message"].Should().Be("success, Product added to your wishlist");
    }

    [Fact]
    public async Task Remove_ShouldRedirectToIndexAndSetSuccessMessage()
    {
        _mockWishlistService.Setup(w => w.RemoveAsync("test@example.com", 1))
            .ReturnsAsync(new WishlistResult { Success = true, Count = 0 });

        var result = await _controller.Remove(1);

        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be(nameof(WishlistController.Index));
        _controller.TempData["Message"].Should().Be("success, Product removed from your wishlist");
    }

    private void SetupAuthenticatedUser(string email)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, email),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.NameIdentifier, "1")
        };

        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
