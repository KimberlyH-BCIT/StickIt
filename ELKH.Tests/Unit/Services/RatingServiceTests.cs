using ELKH.Data;
using ELKH.Models;
using ELKH.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ELKH.Tests.Unit.Services;

public class RatingServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly RatingService _ratingService;

    public RatingServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _ratingService = new RatingService(_context, _context);
    }

    [Fact]
    public async Task ApproveAsync_WithValidRating_ShouldApproveRating()
    {
        var rating = new ProductRatingModel
        {
            PkRatingId = 1,
            FkRegisteredUserId = 1,
            FkProductId = 1,
            Rating = 4,
            Description = "Test comment",
            Approved = false,
            RatedTime = DateTime.UtcNow
        };

        _context.ProductRatings.Add(rating);
        await _context.SaveChangesAsync();

        var result = await _ratingService.ApproveAsync(1);

        result.Should().NotBeNull();
        result!.Approved.Should().BeTrue();

        var approvedRating = await _context.ProductRatings.FindAsync(1);
        approvedRating.Should().NotBeNull();
        approvedRating!.Approved.Should().BeTrue();
    }

    [Fact]
    public async Task GetApprovedReviewsAsync_ShouldReturnOnlyApprovedRatings()
    {
        var approvedRating = new ProductRatingModel
        {
            PkRatingId = 1,
            FkRegisteredUserId = 1,
            FkProductId = 1,
            Rating = 5,
            Description = "Approved comment",
            Approved = true,
            RatedTime = DateTime.UtcNow
        };

        var unapprovedRating = new ProductRatingModel
        {
            PkRatingId = 2,
            FkRegisteredUserId = 1,
            FkProductId = 1,
            Rating = 3,
            Description = "Unapproved comment",
            Approved = false,
            RatedTime = DateTime.UtcNow.AddMinutes(-5)
        };

        _context.ProductRatings.AddRange(approvedRating, unapprovedRating);
        await _context.SaveChangesAsync();

        var result = await _ratingService.GetApprovedReviewsAsync(1);

        result.Should().HaveCount(1);
        result.First().Description.Should().Be("Approved comment");
        result.First().Approved.Should().BeTrue();
    }

    [Fact]
    public async Task GetPagedApprovedReviewsAsync_ShouldCalculateAverageFromApprovedRatings()
    {
        _context.ProductRatings.AddRange(
            new ProductRatingModel { FkProductId = 1, FkRegisteredUserId = 1, Rating = 5, Description = "A", Approved = true, RatedTime = DateTime.UtcNow, RegisteredUser = new RegisteredUserModel { PkRegisteredUserId = 1, Email = "user1@test.com" } },
            new ProductRatingModel { FkProductId = 1, FkRegisteredUserId = 2, Rating = 4, Description = "B", Approved = true, RatedTime = DateTime.UtcNow.AddMinutes(-1), RegisteredUser = new RegisteredUserModel { PkRegisteredUserId = 2, Email = "user2@test.com" } },
            new ProductRatingModel { FkProductId = 1, FkRegisteredUserId = 3, Rating = 2, Description = "C", Approved = false, RatedTime = DateTime.UtcNow.AddMinutes(-2), RegisteredUser = new RegisteredUserModel { PkRegisteredUserId = 3, Email = "user3@test.com" } }
        );
        _context.UserProfiles.AddRange(
            new UserProfileModel { PkEmail = "user1@test.com", FirstName = "User1" },
            new UserProfileModel { PkEmail = "user2@test.com", FirstName = "User2" },
            new UserProfileModel { PkEmail = "user3@test.com", FirstName = "User3" }
        );
        await _context.SaveChangesAsync();

        var result = await _ratingService.GetPagedApprovedReviewsAsync(1, 1);

        result.Reviews.Should().HaveCount(2);
        result.AverageRating.Should().Be(4.5);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
