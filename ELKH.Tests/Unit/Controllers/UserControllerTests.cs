using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;
using ELKH.Controllers;
using ELKH.Data;
using ELKH.Repositories;
using ELKH.Services;
using ELKH.ViewModels;
using ELKH.Models;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Tests.Unit.Controllers;

// TABLE OF CONTENTS
// - Index tests
// - EditProfile tests
// - Addresses tests
// - AddAddress tests
// - DeleteAddress tests

/// <summary>
/// Unit tests for user dashboard and profile management workflows.
/// </summary>
/// <remarks>
/// 1. Index tests
/// 2. EditProfile tests
/// 3. Addresses tests
/// 4. AddAddress tests
/// 5. DeleteAddress tests
/// </remarks>
public class UserControllerTests
{
    private readonly Mock<IRegisteredUserProfileRepo> _mockUserProfileRepo;
    private readonly Mock<IRegisteredUserLogRepo> _mockUserLogRepo;
    private readonly Mock<IContactDetailRepo> _mockContactDetailRepo;
    private readonly Mock<IRatingService> _mockRatingService;
    private readonly Mock<IStoreReviewService> _mockStoreReviewService;
    private readonly Mock<IUserService> _mockUserService;
    private readonly ApplicationDbContext _db;
    private readonly UserController _controller;

    public UserControllerTests()
    {
        _mockUserProfileRepo = new Mock<IRegisteredUserProfileRepo>();
        _mockUserLogRepo = new Mock<IRegisteredUserLogRepo>();
        _mockContactDetailRepo = new Mock<IContactDetailRepo>();
        _mockRatingService = new Mock<IRatingService>();
        _mockStoreReviewService = new Mock<IStoreReviewService>();
        _mockUserService = new Mock<IUserService>();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);

        _controller = new UserController(
            _mockUserProfileRepo.Object,
            _mockUserLogRepo.Object,
            _mockContactDetailRepo.Object,
            _mockRatingService.Object,
            _mockStoreReviewService.Object,
            _mockUserService.Object,
            Mock.Of<ILogger<UserController>>(),
            _db);

        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
        _controller.ControllerContext = new ControllerContext(actionContext);
        _controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        SetupAuthenticatedUser("test@example.com");
        _mockUserService.Setup(u => u.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegisteredUserModel { PkRegisteredUserId = 1, Email = "test@example.com" });
    }

    [Fact]
    public async Task Index_ShouldReturnViewWithUserDashboard()
    {
        var profile = new UserProfileModel
        {
            PkEmail = "test@example.com",
            FirstName = "John",
            LastName = "Doe"
        };

        var dashboard = new DashboardData(
            2,
            new WishlistSectionVM(),
            new OrderSectionVM(),
            new OrderSectionVM());

        _mockUserProfileRepo.Setup(r => r.GetById("test@example.com")).Returns(profile);
        _mockUserService.Setup(u => u.GetDashboardDataAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(dashboard);

        var result = await _controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<UserDashboardVM>().Subject;
        model.Profile.Should().NotBeNull();
        model.Profile!.FirstName.Should().Be("John");
        model.WishlistCount.Should().Be(2);
    }

    [Fact]
    public async Task EditProfile_Get_ShouldReturnViewWithUserProfilePageModel()
    {
        var profile = new UserProfileModel
        {
            PkEmail = "test@example.com",
            FirstName = "John",
            LastName = "Doe"
        };

        _mockUserProfileRepo.Setup(r => r.GetById("test@example.com")).Returns(profile);
        _mockContactDetailRepo.Setup(r => r.GetAllByUserIdAsync(1)).ReturnsAsync(new List<ContactDetailModel>());

        var result = await _controller.EditProfile();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<UserProfilePageVM>().Subject;
        model.Profile.PkEmail.Should().Be("test@example.com");
        model.Profile.FirstName.Should().Be("John");
    }

    [Fact]
    public async Task EditProfile_Post_WithValidModel_ShouldUpdateProfile()
    {
        var existingProfile = new UserProfileModel
        {
            PkEmail = "test@example.com",
            FirstName = "John",
            LastName = "Doe"
        };

        var vm = new UserProfilePageVM
        {
            Profile = new UserProfileVM
            {
                PkEmail = "test@example.com",
                FirstName = "John Updated",
                LastName = "Doe Updated"
            }
        };

        _mockUserProfileRepo.Setup(r => r.GetById("test@example.com")).Returns(existingProfile);
        _mockUserProfileRepo.Setup(r => r.UpdateAndSaveAsync(existingProfile)).ReturnsAsync(true);

        var result = await _controller.EditProfile(vm);

        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be("EditProfile");
        existingProfile.FirstName.Should().Be("John Updated");
        _mockUserProfileRepo.Verify(r => r.UpdateAndSaveAsync(existingProfile), Times.Once);
    }

    [Fact]
    public async Task EditProfile_Post_WithInvalidModel_ShouldReturnViewWithErrors()
    {
        var vm = new UserProfilePageVM
        {
            Profile = new UserProfileVM()
        };
        _controller.ModelState.AddModelError("Profile.FirstName", "First name is required");
        _mockContactDetailRepo.Setup(r => r.GetAllByUserIdAsync(1)).ReturnsAsync(new List<ContactDetailModel>());

        var result = await _controller.EditProfile(vm);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.Model.Should().Be(vm);
        _mockUserProfileRepo.Verify(r => r.UpdateAndSaveAsync(It.IsAny<UserProfileModel>()), Times.Never);
    }

    [Fact]
    public async Task Addresses_ShouldReturnViewWithUserAddresses()
    {
        var addresses = new List<ContactDetailModel>
        {
            new() { PkContactId = 1, FirstName = "John", LastName = "Doe", Street = "123 Test St", City = "Vancouver", Province = "BC", PostCode = "V1A 2B3", Country = "Canada" },
            new() { PkContactId = 2, FirstName = "John", LastName = "Doe", Street = "456 Main Ave", City = "Surrey", Province = "BC", PostCode = "V2B 3C4", Country = "Canada" }
        };

        _mockContactDetailRepo.Setup(r => r.GetAllByUserIdAsync(1)).ReturnsAsync(addresses);

        var result = await _controller.Addresses();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableTo<IEnumerable<ContactDetailVM>>().Subject;
        model.Should().HaveCount(2);
        model.First().Street.Should().Be("123 Test St");
    }

    [Fact]
    public async Task AddAddress_Post_WithValidAddress_ShouldCreateAddress()
    {
        var addressVM = new ContactDetailVM
        {
            FirstName = "John",
            LastName = "Doe",
            PhoneNumber = "123-456-7890",
            Street = "789 New St",
            City = "Vancouver",
            Province = "BC",
            PostalCode = "V1A 2B3",
            Country = "Canada"
        };

        _mockContactDetailRepo.Setup(r => r.AddAndSaveAsync(It.IsAny<ContactDetailModel>())).ReturnsAsync(true);

        var result = await _controller.AddAddress(addressVM);

        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be("EditProfile");
        _mockContactDetailRepo.Verify(r => r.AddAndSaveAsync(It.Is<ContactDetailModel>(c => c.Street == "789 New St" && c.FkRegisteredUserId == 1)), Times.Once);
    }

    [Fact]
    public async Task DeleteAddress_WithValidId_ShouldReturnConfirmationView()
    {
        var address = new ContactDetailModel
        {
            PkContactId = 1,
            FirstName = "John",
            LastName = "Doe",
            PhoneNumber = "123-456-7890",
            Street = "123 Test St",
            City = "Vancouver",
            Province = "BC",
            PostCode = "V1A 2B3",
            Country = "Canada",
            FkRegisteredUserId = 1
        };

        _mockContactDetailRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(address);

        var result = await _controller.DeleteAddress(1);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<ContactDetailVM>().Subject;
        model.Street.Should().Be("123 Test St");
    }

    [Fact]
    public async Task DeleteAddress_WithInvalidId_ShouldRedirectToAddresses()
    {
        _mockContactDetailRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((ContactDetailModel?)null);

        var result = await _controller.DeleteAddress(999);

        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be("Addresses");
    }

    [Fact]
    public async Task DeleteAddress_WithUnauthorizedUser_ShouldRedirectToAddresses()
    {
        var address = new ContactDetailModel
        {
            PkContactId = 1,
            Street = "123 Test St",
            FkRegisteredUserId = 2
        };

        _mockContactDetailRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(address);

        var result = await _controller.DeleteAddress(1);

        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be("Addresses");
    }

    private void SetupAuthenticatedUser(string email)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, email),
            new(ClaimTypes.NameIdentifier, "1")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext.HttpContext.User = principal;
    }
}
