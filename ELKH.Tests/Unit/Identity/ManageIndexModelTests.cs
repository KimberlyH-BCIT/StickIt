using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ELKH.Areas.Identity.Pages.Account.Manage;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ELKH.Tests.Unit.Identity;

/// <summary>
/// Unit tests for the Identity manage index page model.
/// </summary>
public class ManageIndexModelTests
{
    [Fact]
    public async Task OnPostAsync_WhenPhoneNumberUpdateFails_ShouldSetErrorStatusMessageAndRedirect()
    {
        var userManager = CreateMockUserManager();
        var signInManager = CreateMockSignInManager(userManager.Object);
        var identityUser = new IdentityUser { Email = "user@example.com", UserName = "user@example.com" };

        userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(identityUser);
        userManager.Setup(u => u.GetPhoneNumberAsync(identityUser)).ReturnsAsync("555-0100");
        userManager.Setup(u => u.SetPhoneNumberAsync(identityUser, "555-0199"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Update failed" }));

        var model = new IndexModel(userManager.Object, signInManager.Object)
        {
            Input = new IndexModel.InputModel { PhoneNumber = "555-0199" }
        };

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-id")
            }, "Test"))
        };

        model.PageContext = new PageContext
        {
            HttpContext = httpContext,
            ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
                new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
        };
        model.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        var result = await model.OnPostAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        model.StatusMessage.Should().Be("Unexpected error when trying to set phone number.");
        signInManager.Verify(s => s.RefreshSignInAsync(It.IsAny<IdentityUser>()), Times.Never);
    }

    private static Mock<UserManager<IdentityUser>> CreateMockUserManager()
    {
        var store = new Mock<IUserStore<IdentityUser>>();
        return new Mock<UserManager<IdentityUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static Mock<SignInManager<IdentityUser>> CreateMockSignInManager(UserManager<IdentityUser> userManager)
    {
        return new Mock<SignInManager<IdentityUser>>(
            userManager,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<IdentityUser>>(),
            null!,
            null!,
            null!,
            null!);
    }
}
