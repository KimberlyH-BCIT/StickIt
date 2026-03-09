using System.Threading.Tasks;
using Xunit;
using ELKH.Data;
using ELKH.Models;
using ELKH.Services;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Tests;

/// <summary>
/// Unit tests for <see cref="RatingService"/> using an EF Core in-memory database.
/// Verifies that service mutations persist correctly without requiring a real database.
/// </summary>
public class RatingServiceTests
{
    /// <summary>
    /// ApproveAsync should set <c>Approved = true</c> and <c>IsFlagged = false</c>
    /// on the target rating, and those changes should be visible in a subsequent
    /// context instance (i.e. the save was actually committed).
    /// </summary>
    [Fact]
    public async Task ApproveAsync_SetsApprovedAndClearsFlag()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "ApproveRatingTest")
            .Options;

        using (var db = new ApplicationDbContext(options))
        {
            var rating = new ProductRatingModel
            {
                PkRatingId = 1,
                Approved = false,
                IsFlagged = true
            };
            db.ProductRatings.Add(rating);
            await db.SaveChangesAsync();
        }

        using (var db = new ApplicationDbContext(options))
        {
            var svc = new RatingService(db);
            var result = await svc.ApproveAsync(1);

            Assert.NotNull(result);
            Assert.True(result.Approved);
            Assert.False(result.IsFlagged);

            // verify persisted
            var persisted = await db.ProductRatings.FindAsync(1);
            Assert.NotNull(persisted);
            Assert.True(persisted.Approved);
            Assert.False(persisted.IsFlagged);
        }
    }
}
