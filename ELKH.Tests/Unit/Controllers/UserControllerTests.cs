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
using ELKH.Repositories;
using ELKH.ViewModels;
using ELKH.Models;

namespace ELKH.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for UserController functionality.
/// Tests user profile management, address book, and account operations.
/// </summary>
public class UserControllerTests
{
    private readonly Mock<IRegisteredUserProfileRepo> _mockUserProfileRepo;
    private readonly Mock<IRegisteredUserLogRepo> _mockUserLogRepo;
    private readonly Mock<IContactDetailRepo> _mockContactDetailRepo;
    private readonly Mock<IRatingService> _mockRatingService;
    private readonly Mock<IStoreReviewService> _mockStoreReviewService;
    private readonly UserController _controller;

    public UserControllerTests()
    {
        // Setup mocks
        _mockUserProfileRepo = new Mock<IRegisteredUserProfileRepo>();
        _mockUserLogRepo = new Mock<IRegisteredUserLogRepo>();
        _mockContactDetailRepo = new Mock<IContactDetailRepo>();
        _mockRatingService = new Mock<IRatingService>();
        _mockStoreReviewService = new Mock<IStoreReviewService>();

        // Create controller under test
        _controller = new UserController(
            _mockUserProfileRepo.Object,
            _mockUserLogRepo.Object,
            _mockContactDetailRepo.Object,
            _mockRatingService.Object,
            _mockStoreReviewService.Object,
            NullLogger<UserController>.Instance);

        // Setup controller context
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
        _controller.ControllerContext = new ControllerContext(actionContext);
        _controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        
        // Setup authenticated user
        SetupAuthenticatedUser("test@example.com");
    }

    [Fact]
    public async Task Index_ShouldReturnViewWithUserDashboard()
    {
        // Arrange
        var userProfile = new RegisteredUserProfile
        {
            Id = 1,
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe"
        };

        _mockUserProfileRepo.Setup(u => u.GetByEmailAsync("test@example.com"))
                           .ReturnsAsync(userProfile);

        // Act
        var result = await _controller.Index();

        // Assert
        result.Should().BeOfType<ViewResult>();
        _mockUserProfileRepo.Verify(u => u.GetByEmailAsync("test@example.com"), Times.Once);
    }

    [Fact]
    public async Task Profile_Get_ShouldReturnViewWithUserProfile()
    {
        // Arrange
        var userProfile = new RegisteredUserProfile
        {
            Id = 1,
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe"
        };

        _mockUserProfileRepo.Setup(u => u.GetByEmailAsync("test@example.com"))
                           .ReturnsAsync(userProfile);

        // Act
        var result = await _controller.Profile();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<RegisteredUserProfile>().Subject;
        model.Email.Should().Be("test@example.com");
        model.FirstName.Should().Be("John");
    }

    [Fact]
    public async Task Profile_Post_WithValidModel_ShouldUpdateProfile()
    {
        // Arrange
        var profileVM = new UserProfileVM
        {
            FirstName = "John Updated",
            LastName = "Doe Updated",
            PhoneNumber = "123-456-7890"
        };

        _mockUserProfileRepo.Setup(u => u.UpdateProfileAsync("test@example.com", It.IsAny<UserProfileVM>()))
                           .ReturnsAsync(true);

        // Act
        var result = await _controller.Profile(profileVM);

        // Assert
        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be("Profile");
        
        _mockUserProfileRepo.Verify(u => u.UpdateProfileAsync("test@example.com", 
            It.Is<UserProfileVM>(p => p.FirstName == "John Updated")), Times.Once);
    }

    [Fact]
    public async Task Profile_Post_WithInvalidModel_ShouldReturnViewWithErrors()
    {
        // Arrange
        var profileVM = new UserProfileVM(); // Invalid model
        _controller.ModelState.AddModelError("FirstName", "First name is required");

        // Act
        var result = await _controller.Profile(profileVM);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.Model.Should().Be(profileVM);
        _mockUserProfileRepo.Verify(u => u.UpdateProfileAsync(It.IsAny<string>(), It.IsAny<UserProfileVM>()), 
                                   Times.Never);
    }

    [Fact]
    public async Task Addresses_ShouldReturnViewWithUserAddresses()
    {
        // Arrange
        var addresses = new List<ContactDetailModel>
        {
            new ContactDetailModel { PkContactId = 1, Street = "123 Test St", City = "Vancouver" },
            new ContactDetailModel { PkContactId = 2, Street = "456 Main Ave", City = "Surrey" }
        };

        _mockContactDetailRepo.Setup(c => c.GetByEmailAsync("test@example.com"))
                             .ReturnsAsync(addresses);

        // Act
        var result = await _controller.Addresses();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableToType<IEnumerable<ContactDetailModel>>().Subject;
        model.Should().HaveCount(2);
        model.First().Address.Should().Be("123 Test St");
    }

    [Fact]
    public async Task AddAddress_Post_WithValidAddress_ShouldCreateAddress()
    {
        // Arrange
        var addressVM = new ContactDetailVM
        {
            Address = "789 New St",
            City = "Vancouver",
            PostalCode = "V1A 2B3"
        };

        _mockContactDetailRepo.Setup(c => c.CreateAsync(It.IsAny<ContactDetailModel>()))
                             .ReturnsAsync(new ContactDetailModel { PkContactId = 1 });

        // Act
        var result = await _controller.AddAddress(addressVM);

        // Assert
        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be("Addresses");
        
        _mockContactDetailRepo.Verify(c => c.CreateAsync(It.Is<ContactDetailModel>(cd => 
            cd.Street == "789 New St")), Times.Once);
    }

    [Fact]
    public async Task DeleteAddress_WithValidId_ShouldReturnConfirmationView()
    {
        // Arrange
        var address = new ContactDetailModel 
        { 
            PkContactId = 1, 
            Street = "123 Test St", 
            FkRegisteredUserId = 1 
        };

        _mockContactDetailRepo.Setup(c => c.GetByIdAsync(1))
                             .ReturnsAsync(address);

        // Act
        var result = await _controller.DeleteAddress(1);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<ContactDetailModel>().Subject;
        model.Street.Should().Be("123 Test St");
    }

    [Fact]
    public async Task DeleteAddress_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        _mockContactDetailRepo.Setup(c => c.GetByIdAsync(999))
                             .ReturnsAsync((ContactDetailModel?)null);

        // Act
        var result = await _controller.DeleteAddress(999);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteAddress_WithUnauthorizedUser_ShouldReturnUnauthorized()
    {
        // Arrange
        var address = new ContactDetailModel 
        { 
            PkContactId = 1, 
            Street = "123 Test St", 
            FkRegisteredUserId = 2  // Different user
        };

        _mockContactDetailRepo.Setup(c => c.GetByIdAsync(1))
                             .ReturnsAsync(address);

        // Act
        var result = await _controller.DeleteAddress(1);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
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