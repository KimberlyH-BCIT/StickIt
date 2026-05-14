using System.Collections.Generic;
using System.Threading.Tasks;
using ELKH.Areas.Identity.Pages.Account;
using ELKH.Configuration;
using ELKH.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ELKH.Tests.Unit.Identity;

public class LoginModelTests
{
    [Fact]
    public async Task OnPostAsync_InProduction_ShouldEnableLockoutOnFailure()
    {
        var userManager = CreateMockUserManager();
        var signInManager = CreateMockSignInManager(userManager.Object);
        var logger = new Mock<ILogger<LoginModel>>();
        var recaptcha = new Mock<IReCaptchaService>();
        recaptcha.Setup(r => r.VerifyAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        signInManager.Setup(s => s.GetExternalAuthenticationSchemesAsync())
            .ReturnsAsync(new List<Microsoft.AspNetCore.Authentication.AuthenticationScheme>());
        signInManager.Setup(s => s.PasswordSignInAsync("user@example.com", "password", true, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);

        var model = CreateModel(signInManager.Object, logger.Object, recaptcha.Object, environment.Object);
        model.Input = new LoginModel.InputModel
        {
            Email = "user@example.com",
            Password = "password",
            RememberMe = true
        };

        var result = await model.OnPostAsync();

        result.Should().BeOfType<PageResult>();
        signInManager.Verify(s => s.PasswordSignInAsync("user@example.com", "password", true, true), Times.Once);
    }

    [Fact]
    public async Task OnPostAsync_InDevelopment_ShouldDisableLockoutOnFailure()
    {
        var userManager = CreateMockUserManager();
        var signInManager = CreateMockSignInManager(userManager.Object);
        var logger = new Mock<ILogger<LoginModel>>();
        var recaptcha = new Mock<IReCaptchaService>();
        recaptcha.Setup(r => r.VerifyAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        signInManager.Setup(s => s.GetExternalAuthenticationSchemesAsync())
            .ReturnsAsync(new List<Microsoft.AspNetCore.Authentication.AuthenticationScheme>());
        signInManager.Setup(s => s.PasswordSignInAsync("user@example.com", "password", false, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);

        var model = CreateModel(signInManager.Object, logger.Object, recaptcha.Object, environment.Object);
        model.Input = new LoginModel.InputModel
        {
            Email = "user@example.com",
            Password = "password",
            RememberMe = false
        };

        var result = await model.OnPostAsync();

        result.Should().BeOfType<PageResult>();
        signInManager.Verify(s => s.PasswordSignInAsync("user@example.com", "password", false, false), Times.Once);
    }

    private static LoginModel CreateModel(
        SignInManager<IdentityUser> signInManager,
        ILogger<LoginModel> logger,
        IReCaptchaService reCaptcha,
        IWebHostEnvironment environment)
    {
        var model = new LoginModel(
            signInManager,
            logger,
            reCaptcha,
            Options.Create(new ReCaptchaOptions()),
            environment);

        var httpContext = new DefaultHttpContext();
        model.PageContext = new PageContext
        {
            HttpContext = httpContext,
            ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
                new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
        };
        model.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        model.Url = new Mock<Microsoft.AspNetCore.Mvc.IUrlHelper>().Object;
        httpContext.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["g-recaptcha-response"] = "token"
        });

        return model;
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
