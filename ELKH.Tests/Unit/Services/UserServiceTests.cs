using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ELKH.Tests.Unit.Services;

/// <summary>
/// Unit tests for UserService with mocked dependencies.
/// Tests user lookup, caching, and wishlist operations.
/// </summary>
public class UserServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IMemoryCache> _mockCache;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        // Setup mocks
        _mockCache = new Mock<IMemoryCache>();

        // Create service under test
        _userService = new UserService(
            _context,
            _mockCache.Object,
            NullLogger<UserService>.Instance);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var user = new RegisteredUserModel
        {
            PkRegisteredUserId = 1,
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe"
        };

        var wishlist = new WishListModel
        {
            PkWishListId = 1,
            FkUserId = 1,
            CreatedAt = DateTime.UtcNow
        };

        var product = new ProductModel
        {
            PkProductId = 1,
            Name = "Test Product",
            Price = 19.99m
        };

        var wishlistItem = new WishListItemModel
        {
            FkWishListId = 1,
            FkProductId = 1,
            CreatedAt = DateTime.UtcNow
        };

        _context.RegisteredUsers.Add(user);
        _context.WishLists.Add(wishlist);
        _context.Products.Add(product);
        _context.WishListItems.Add(wishlistItem);
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetByEmailAsync_WithValidEmail_ShouldReturnUser()
    {
        // Arrange
        var email = "test@example.com";

        // Act
        var result = await _userService.GetByEmailAsync(email);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be(email);
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
    }

    [Fact]
    public async Task GetByEmailAsync_WithInvalidEmail_ShouldReturnNull()
    {
        // Arrange
        var email = "nonexistent@example.com";

        // Act
        var result = await _userService.GetByEmailAsync(email);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldReturnUser()
    {
        // Act
        var result = await _userService.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.PkRegisteredUserId.Should().Be(1);
        result.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Act
        var result = await _userService.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetWishlistCountAsync_ShouldReturnCorrectCount()
    {
        // Act
        var result = await _userService.GetWishlistCountAsync(1);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task GetWishlistCountAsync_WithNoItems_ShouldReturnZero()
    {
        // Act
        var result = await _userService.GetWishlistCountAsync(999);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void InvalidateCache_ShouldCallCacheRemove()
    {
        // Arrange
        var email = "test@example.com";

        // Act
        _userService.InvalidateCache(email);

        // Assert
        _mockCache.Verify(c => c.Remove(It.Is<string>(key => key.Contains(email))), Times.Once);
    }

    private void Dispose()
    {
        _context.Dispose();
    }
}