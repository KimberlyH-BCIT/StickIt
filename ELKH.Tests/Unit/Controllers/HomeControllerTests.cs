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
using ELKH.Models;

namespace ELKH.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for HomeController functionality.
/// Tests public homepage display with store reviews.
/// </summary>
public class HomeControllerTests
{
    private readonly Mock<IStoreReviewService> _mockStoreReviewService;
    private readonly HomeController _controller;

    public HomeControllerTests()
    {
        // Setup mocks
        _mockStoreReviewService = new Mock<IStoreReviewService>();

        // Create controller under test
        _controller = new HomeController(_mockStoreReviewService.Object);

        // Setup controller context
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
        _controller.ControllerContext = new ControllerContext(actionContext);
        _controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    [Fact]
    public async Task Index_ShouldReturnViewWithStoreReviews()
    {
        // Arrange
        var reviews = new List<StoreReview>
        {
            new StoreReview { Id = 1, ReviewText = "Great store!", Rating = 5, IsApproved = true },
            new StoreReview { Id = 2, ReviewText = "Good service!", Rating = 4, IsApproved = true }
        };

        _mockStoreReviewService.Setup(s => s.GetApprovedReviewsAsync(10))
                              .ReturnsAsync(reviews);

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.ViewData["StoreReviews"].Should().BeEquivalentTo(reviews);
        _mockStoreReviewService.Verify(s => s.GetApprovedReviewsAsync(10), Times.Once);
    }

    [Fact]
    public async Task Index_WithNoReviews_ShouldReturnViewWithEmptyReviews()
    {
        // Arrange
        var emptyReviews = new List<StoreReview>();
        _mockStoreReviewService.Setup(s => s.GetApprovedReviewsAsync(10))
                              .ReturnsAsync(emptyReviews);

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.ViewData["StoreReviews"].Should().BeEquivalentTo(emptyReviews);
    }

    [Fact]
    public void Privacy_ShouldReturnView()
    {
        // Act
        var result = _controller.Privacy();

        // Assert
        result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public void Error_ShouldReturnViewWithErrorViewModel()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        _controller.ControllerContext.HttpContext = httpContext;

        // Act
        var result = _controller.Error();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<ErrorViewModel>().Subject;
        model.RequestId.Should().Be("test-trace-id");
    }

    [Fact]
    public void Error_WithNullTraceIdentifier_ShouldReturnViewWithNullRequestId()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = null;
        _controller.ControllerContext.HttpContext = httpContext;

        // Act
        var result = _controller.Error();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<ErrorViewModel>().Subject;
        model.RequestId.Should().BeNull();
    }
}