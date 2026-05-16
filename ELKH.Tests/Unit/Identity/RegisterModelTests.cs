using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ELKH.Areas.Identity.Pages.Account;
using ELKH.Configuration;
using ELKH.Data;
using ELKH.Models;
using ELKH.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ELKH.Tests.Unit.Identity;

public class RegisterModelTests : IDisposable
{
    private readonly string _databaseName = $"RegisterModelTests_{Guid.NewGuid():N}";

    [Fact]
    public async Task OnPostAsync_WhenCustomerRoleDoesNotExist_ShouldCreateRoleAndAssignUser()
    {
        await using var context = CreateContext();
        var userStore = CreateMockUserStore();
        var userManager = CreateMockUserManager(userStore.Object);
        var roleManager = CreateMockRoleManager();
        var signInManager = CreateMockSignInManager(userManager.Object);
        var contactRepository = new Mock<IContactDetailRepo>();
        var emailSender = new Mock<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender>();
        var logger = new Mock<ILogger<RegisterModel>>();

        roleManager.Setup(r => r.RoleExistsAsync("Customer")).ReturnsAsync(false);
        roleManager.Setup(r => r.CreateAsync(It.Is<IdentityRole>(role => role.Name == "Customer")))
            .ReturnsAsync(IdentityResult.Success);

        userManager.SetupGet(u => u.SupportsUserEmail).Returns(true);
        userManager.Object.Options.SignIn.RequireConfirmedAccount = false;
        userManager.Setup(u => u.CreateAsync(It.IsAny<IdentityUser>(), "Password123!"))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(u => u.AddToRoleAsync(It.IsAny<IdentityUser>(), "Customer"))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(u => u.GetUserIdAsync(It.IsAny<IdentityUser>())).ReturnsAsync("user-id");
        userManager.Setup(u => u.GenerateEmailConfirmationTokenAsync(It.IsAny<IdentityUser>())).ReturnsAsync("confirmation-token");

        signInManager.Setup(s => s.GetExternalAuthenticationSchemesAsync())
            .ReturnsAsync(new List<AuthenticationScheme>());
        signInManager.Setup(s => s.SignInAsync(It.IsAny<IdentityUser>(), false, null))
            .Returns(Task.CompletedTask);

        contactRepository.Setup(r => r.AddAndSaveAsync(It.IsAny<ContactDetailModel>())).ReturnsAsync(true);
        emailSender.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(() => Task.CompletedTask);

        var model = CreateModel(
            context,
            userStore.Object,
            userManager.Object,
            roleManager.Object,
            signInManager.Object,
            contactRepository.Object,
            emailSender.Object,
            logger.Object);
        model.Input = CreateValidInput();

        var result = await model.OnPostAsync();

        result.Should().BeOfType<RedirectToActionResult>();
        roleManager.Verify(r => r.CreateAsync(It.Is<IdentityRole>(role => role.Name == "Customer")), Times.Once);
        userManager.Verify(u => u.AddToRoleAsync(It.IsAny<IdentityUser>(), "Customer"), Times.Once);
    }

    [Fact]
    public async Task OnPostAsync_WhenCustomerRoleExists_ShouldAssignUserWithoutCreatingRole()
    {
        await using var context = CreateContext();
        var userStore = CreateMockUserStore();
        var userManager = CreateMockUserManager(userStore.Object);
        var roleManager = CreateMockRoleManager();
        var signInManager = CreateMockSignInManager(userManager.Object);
        var contactRepository = new Mock<IContactDetailRepo>();
        var emailSender = new Mock<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender>();
        var logger = new Mock<ILogger<RegisterModel>>();

        roleManager.Setup(r => r.RoleExistsAsync("Customer")).ReturnsAsync(true);

        userManager.SetupGet(u => u.SupportsUserEmail).Returns(true);
        userManager.Object.Options.SignIn.RequireConfirmedAccount = false;
        userManager.Setup(u => u.CreateAsync(It.IsAny<IdentityUser>(), "Password123!"))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(u => u.AddToRoleAsync(It.IsAny<IdentityUser>(), "Customer"))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(u => u.GetUserIdAsync(It.IsAny<IdentityUser>())).ReturnsAsync("user-id");
        userManager.Setup(u => u.GenerateEmailConfirmationTokenAsync(It.IsAny<IdentityUser>())).ReturnsAsync("confirmation-token");

        signInManager.Setup(s => s.GetExternalAuthenticationSchemesAsync())
            .ReturnsAsync(new List<AuthenticationScheme>());
        signInManager.Setup(s => s.SignInAsync(It.IsAny<IdentityUser>(), false, null))
            .Returns(Task.CompletedTask);

        contactRepository.Setup(r => r.AddAndSaveAsync(It.IsAny<ContactDetailModel>())).ReturnsAsync(true);
        emailSender.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(() => Task.CompletedTask);

        var model = CreateModel(
            context,
            userStore.Object,
            userManager.Object,
            roleManager.Object,
            signInManager.Object,
            contactRepository.Object,
            emailSender.Object,
            logger.Object);
        model.Input = CreateValidInput();

        var result = await model.OnPostAsync();

        result.Should().BeOfType<RedirectToActionResult>();
        roleManager.Verify(r => r.CreateAsync(It.IsAny<IdentityRole>()), Times.Never);
        userManager.Verify(u => u.AddToRoleAsync(It.IsAny<IdentityUser>(), "Customer"), Times.Once);
    }

    [Fact]
    public async Task OnPostAsync_WhenRoleAssignmentFails_ShouldReturnPageAndDeleteUser()
    {
        await using var context = CreateContext();
        var userStore = CreateMockUserStore();
        var userManager = CreateMockUserManager(userStore.Object);
        var roleManager = CreateMockRoleManager();
        var signInManager = CreateMockSignInManager(userManager.Object);
        var contactRepository = new Mock<IContactDetailRepo>();
        var emailSender = new Mock<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender>();
        var logger = new Mock<ILogger<RegisterModel>>();

        roleManager.Setup(r => r.RoleExistsAsync("Customer")).ReturnsAsync(true);

        userManager.SetupGet(u => u.SupportsUserEmail).Returns(true);
        userManager.Object.Options.SignIn.RequireConfirmedAccount = false;
        var createdUser = new IdentityUser { Email = "customer@example.com", UserName = "customer@example.com" };
        userManager.Setup(u => u.CreateAsync(It.IsAny<IdentityUser>(), "Password123!"))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(u => u.AddToRoleAsync(It.IsAny<IdentityUser>(), "Customer"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Role assignment failed." }));
        userManager.Setup(u => u.DeleteAsync(It.IsAny<IdentityUser>())).ReturnsAsync(IdentityResult.Success);

        signInManager.Setup(s => s.GetExternalAuthenticationSchemesAsync())
            .ReturnsAsync(new List<AuthenticationScheme>());

        contactRepository.Setup(r => r.AddAndSaveAsync(It.IsAny<ContactDetailModel>())).ReturnsAsync(true);
        emailSender.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(() => Task.CompletedTask);

        var model = CreateModel(
            context,
            userStore.Object,
            userManager.Object,
            roleManager.Object,
            signInManager.Object,
            contactRepository.Object,
            emailSender.Object,
            logger.Object);
        model.Input = CreateValidInput();

        var result = await model.OnPostAsync();

        result.Should().BeOfType<PageResult>();
        userManager.Verify(u => u.DeleteAsync(It.IsAny<IdentityUser>()), Times.Once);
        model.ModelState[string.Empty]!.Errors.Should().ContainSingle(error => error.ErrorMessage == "Role assignment failed.");
    }

    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static RegisterModel CreateModel(
        ApplicationDbContext context,
        IUserStore<IdentityUser> userStore,
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        SignInManager<IdentityUser> signInManager,
        IContactDetailRepo contactRepository,
        Microsoft.AspNetCore.Identity.UI.Services.IEmailSender emailSender,
        ILogger<RegisterModel> logger)
    {
        var model = new RegisterModel(
            userManager,
            roleManager,
            userStore,
            signInManager,
            logger,
            emailSender,
            context,
            contactRepository,
            Options.Create(new ReCaptchaOptions()));

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";

        model.PageContext = new PageContext
        {
            HttpContext = httpContext,
            ViewData = new ViewDataDictionary(
                new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
        };
        model.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        var urlHelper = new Mock<IUrlHelper>();
        urlHelper.SetupGet(u => u.ActionContext)
            .Returns(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()));
        urlHelper.Setup(u => u.Content("~/")).Returns("/");
        urlHelper.Setup(u => u.RouteUrl(It.IsAny<Microsoft.AspNetCore.Mvc.Routing.UrlRouteContext>()))
            .Returns("https://localhost/Identity/Account/ConfirmEmail");
        model.Url = urlHelper.Object;

        return model;
    }

    private static RegisterModel.InputModel CreateValidInput() => new()
    {
        Email = "customer@example.com",
        Password = "Password123!",
        ConfirmPassword = "Password123!",
        FirstName = "Test",
        LastName = "Customer",
        PhoneNumber = "555-0100",
        Street = "100 Queen St W",
        City = "Toronto",
        Province = "Ontario",
        PostCode = "M5H 2N2",
        Country = "Canada"
    };

    private static Mock<IUserEmailStore<IdentityUser>> CreateMockUserStore()
    {
        var userStore = new Mock<IUserEmailStore<IdentityUser>>();
        userStore.Setup(s => s.SetUserNameAsync(It.IsAny<IdentityUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        userStore.Setup(s => s.SetEmailAsync(It.IsAny<IdentityUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return userStore;
    }

    private static Mock<UserManager<IdentityUser>> CreateMockUserManager(IUserStore<IdentityUser> userStore)
    {
        return new Mock<UserManager<IdentityUser>>(
            userStore, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static Mock<RoleManager<IdentityRole>> CreateMockRoleManager()
    {
        var store = new Mock<IRoleStore<IdentityRole>>();
        return new Mock<RoleManager<IdentityRole>>(
            store.Object, null!, null!, null!, null!);
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

    public void Dispose()
    {
        using var context = CreateContext();
        context.Database.EnsureDeleted();
    }
}
