using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using ELKH.Controllers;
using ELKH.Services;

namespace ELKH.Tests;

/// <summary>
/// Integration-style unit tests for <see cref="ModerationController"/>.
/// Verifies that AJAX requests receive JSON responses and standard requests redirect.
/// Uses Moq to isolate the controller from <see cref="IRatingService"/> and <see cref="IModerationService"/>.
/// </summary>
public class ModerationControllerTests
{
    /// <summary>
    /// An AJAX Approve request (X-Requested-With: XMLHttpRequest) should return
    /// a <see cref="JsonResult"/> containing a non-null value.
    /// </summary>
    [Fact]
    public async Task Approve_ReturnsJson_OnAjaxRequest()
    {
        var mockRating = new Moq.Mock<IRatingService>();
        mockRating.Setup(r => r.ApproveAsync(1)).ReturnsAsync(new ELKH.Models.ProductRatingModel { PkRatingId = 1 });

        var mockModeration = new Moq.Mock<IModerationService>();

        var controller = new ModerationController(mockRating.Object, mockModeration.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        var result = await controller.Approve(1);

        Assert.IsType<JsonResult>(result);
        var json = (JsonResult)result;
        // Expect a JSON object with success = true
        Assert.NotNull(json.Value);
    }

    /// <summary>
    /// An AJAX Flag request should return a <see cref="JsonResult"/> with a non-null value
    /// when the moderation service reports success.
    /// </summary>
    [Fact]
    public async Task Flag_ReturnsJson_OnAjaxRequest()
    {
        var mockRating = new Moq.Mock<IRatingService>();
        var mockModeration = new Moq.Mock<IModerationService>();
        mockModeration.Setup(m => m.FlagAsync(2, "note", It.IsAny<string>())).ReturnsAsync(new ModerationResult { Success = true });

        var controller = new ModerationController(mockRating.Object, mockModeration.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        var result = await controller.Flag(2, "note");

        Assert.IsType<JsonResult>(result);
        var json = (JsonResult)result;
        Assert.NotNull(json.Value);
    }
}
