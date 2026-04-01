using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ELKH.Tests.Unit.Services;

/// <summary>
/// Unit tests for RatingService functionality.
/// Tests rating creation, updates, approvals, and business rules.
/// </summary>
public class RatingServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IUserService> _mockUserService;
    private readonly RatingService _ratingService;

    public RatingServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        // Setup mocks
        _mockUserService = new Mock<IUserService>();

        // Create service under test
        _ratingService = new RatingService(
            _context,
            _mockUserService.Object,
            NullLogger<RatingService>.Instance);

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

        var product = new ProductModel
        {
            PkProductId = 1,
            Name = "Test Product",
            Price = 19.99m
        };

        _context.RegisteredUsers.Add(user);
        _context.Products.Add(product);
        _context.SaveChanges();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task CreateRatingAsync_WithValidRating_ShouldCreateSuccessfully(int ratingValue)
    {
        // Arrange
        _mockUserService.Setup(u => u.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new RegisteredUserModel { PkRegisteredUserId = 1, Email = "test@example.com" });

        // Act
        var result = await _ratingService.CreateRatingAsync(
            "test@example.com", 
            1, 
            ratingValue, 
            "Great product!");

        // Assert
        result.Should().NotBeNull();
        result.Rating.Should().Be(ratingValue);
        result.Comment.Should().Be("Great product!");
        result.IsApproved.Should().BeFalse(); // New ratings start as unapproved
        
        var savedRating = await _context.ProductRatings.FirstOrDefaultAsync();
        savedRating.Should().NotBeNull();
        savedRating!.Rating.Should().Be(ratingValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task CreateRatingAsync_WithInvalidRating_ShouldThrowException(int ratingValue)
    {
        // Arrange
        _mockUserService.Setup(u => u.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new RegisteredUserModel { PkRegisteredUserId = 1, Email = "test@example.com" });

        // Act & Assert
        await _ratingService
            .Invoking(s => s.CreateRatingAsync("test@example.com", 1, ratingValue, "Comment"))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task CreateRatingAsync_WithDuplicateRating_ShouldReturnExisting()
    {
        // Arrange
        var existingRating = new ProductRatingModel
        {
            FkRegisteredUserId = 1,
            FkProductId = 1,
            Rating = 4,
            Comment = "Original comment",
            IsApproved = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.ProductRatings.Add(existingRating);
        await _context.SaveChangesAsync();

        _mockUserService.Setup(u => u.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new RegisteredUserModel { PkRegisteredUserId = 1, Email = "test@example.com" });

        // Act
        var result = await _ratingService.CreateRatingAsync(
            "test@example.com", 
            1, 
            5, 
            "New comment");

        // Assert
        result.Should().NotBeNull();
        result.Rating.Should().Be(4); // Should return the existing rating
        result.Comment.Should().Be("Original comment");
        
        var ratingCount = await _context.ProductRatings.CountAsync();
        ratingCount.Should().Be(1); // No duplicate should be created
    }

    [Fact]
    public async Task ApproveAsync_WithValidRating_ShouldApproveRating()
    {
        // Arrange
        var rating = new ProductRatingModel
        {
            PkRatingId = 1,
            FkRegisteredUserId = 1,
            FkProductId = 1,
            Rating = 4,
            Comment = "Test comment",
            IsApproved = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.ProductRatings.Add(rating);
        await _context.SaveChangesAsync();

        // Act
        var result = await _ratingService.ApproveAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.IsApproved.Should().BeTrue();
        
        var approvedRating = await _context.ProductRatings.FindAsync(1);
        approvedRating!.IsApproved.Should().BeTrue();
    }

    [Fact]
    public async Task GetRatingsForProductAsync_ShouldReturnOnlyApprovedRatings()
    {
        // Arrange
        var approvedRating = new ProductRatingModel
        {
            PkRatingId = 1,
            FkRegisteredUserId = 1,
            FkProductId = 1,
            Rating = 5,
            Comment = "Approved comment",
            IsApproved = true,
            CreatedAt = DateTime.UtcNow
        };

        var unapprovedRating = new ProductRatingModel
        {
            PkRatingId = 2,
            FkRegisteredUserId = 1,
            FkProductId = 1,
            Rating = 3,
            Comment = "Unapproved comment",
            IsApproved = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.ProductRatings.AddRange(approvedRating, unapprovedRating);
        await _context.SaveChangesAsync();

        // Act
        var result = await _ratingService.GetRatingsForProductAsync(1);

        // Assert
        result.Should().HaveCount(1);
        result.First().Comment.Should().Be("Approved comment");
        result.First().IsApproved.Should().BeTrue();
    }

    [Fact]
    public async Task GetAverageRatingAsync_ShouldCalculateCorrectAverage()
    {
        // Arrange
        var ratings = new[]
        {
            new ProductRatingModel { FkProductId = 1, Rating = 5, IsApproved = true },
            new ProductRatingModel { FkProductId = 1, Rating = 4, IsApproved = true },
            new ProductRatingModel { FkProductId = 1, Rating = 3, IsApproved = true },
            new ProductRatingModel { FkProductId = 1, Rating = 2, IsApproved = false } // Should be excluded
        };

        _context.ProductRatings.AddRange(ratings);
        await _context.SaveChangesAsync();

        // Act
        var result = await _ratingService.GetAverageRatingAsync(1);

        // Assert
        result.Should().Be(4.0); // (5 + 4 + 3) / 3 = 4.0, excluding unapproved rating
    }

    private void Dispose()
    {
        _context.Dispose();
    }
}