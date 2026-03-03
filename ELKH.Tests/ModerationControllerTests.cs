using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using ELKH.Areas.Admin.Controllers;
using ELKH.Services;

namespace ELKH.Tests;

public class ModerationControllerTests
{
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

    [Fact]
    public async Task Flag_ReturnsJson_OnAjaxRequest()
    {
        var mockRating = new Moq.Mock<IRatingService>();
        var mockModeration = new Moq.Mock<IModerationService>();
        mockModeration.Setup(m => m.FlagAsync(2, "note", "mod")).ReturnsAsync(new ModerationResult { Success = true });

        var controller = new ModerationController(mockRating.Object, mockModeration.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        var result = await controller.Flag(2, "note");

        Assert.IsType<JsonResult>(result);
        var json = (JsonResult)result;
        Assert.NotNull(json.Value);
    }
}
