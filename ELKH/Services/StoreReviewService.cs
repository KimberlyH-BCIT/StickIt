using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ELKH.Data;
using ELKH.Models;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Services
{
    /// <summary>
    /// Service for managing store reviews (reviews about the website/store itself).
    /// Handles submission, retrieval, verified buyer validation, and review updates.
    /// </summary>
    public class StoreReviewService : IStoreReviewService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<StoreReviewService> _logger;

        public StoreReviewService(ApplicationDbContext db, ILogger<StoreReviewService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<bool> SubmitReviewAsync(int userId, string title, int rating, string description, CancellationToken ct = default)
        {
            try
            {
                // Check if user already has a non-deleted review
                var existingReview = await _db.StoreReviews
                    .Where(sr => sr.FkRegisteredUserId == userId && !sr.IsDeleted)
                    .FirstOrDefaultAsync(ct);

                if (existingReview != null)
                {
                    _logger.LogWarning("User {UserId} attempted to submit duplicate store review", userId);
                    return false; // User can only have one active review
                }

                // Check if user is a verified buyer (has any completed orders)
                var isVerified = await IsVerifiedBuyerAsync(userId, ct);

                var review = new StoreReviewModel
                {
                    FkRegisteredUserId = userId,
                    Title = title,
                    Rating = rating,
                    Description = description,
                    CreatedAt = DateTime.UtcNow,
                    Approved = false, // Requires moderation
                    IsVerifiedBuyer = isVerified,
                    IsDeleted = false,
                    IsFlagged = false
                };

                _db.StoreReviews.Add(review);
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("Store review submitted by user {UserId} (Verified: {IsVerified})", userId, isVerified);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting store review for user {UserId}", userId);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<List<StoreReviewModel>> GetApprovedReviewsAsync(int count = 10, CancellationToken ct = default)
        {
            return await _db.StoreReviews
                .AsNoTracking()
                .Include(sr => sr.RegisteredUser)
                .Where(sr => sr.Approved && !sr.IsDeleted)
                .OrderByDescending(sr => sr.CreatedAt)
                .Take(count)
                .ToListAsync(ct);
        }

        /// <inheritdoc/>
        public async Task<bool> IsVerifiedBuyerAsync(int userId, CancellationToken ct = default)
        {
            // User is verified if they have at least one completed/delivered order
            return await _db.Orders
                .AsNoTracking()
                .Where(o => o.FkRegisteredUserId == userId)
                .Where(o => o.OrderStatus == OrderStatus.Shipped || o.DeliveryStatus == DeliveryStatus.Delivered)
                .AnyAsync(ct);
        }

        /// <inheritdoc/>
        public async Task<StoreReviewModel?> GetUserReviewAsync(int userId, CancellationToken ct = default)
        {
            return await _db.StoreReviews
                .Where(sr => sr.FkRegisteredUserId == userId && !sr.IsDeleted)
                .FirstOrDefaultAsync(ct);
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateReviewAsync(int reviewId, int userId, string title, int rating, string description, CancellationToken ct = default)
        {
            try
            {
                var review = await _db.StoreReviews
                    .Where(sr => sr.PkStoreReviewId == reviewId && sr.FkRegisteredUserId == userId && !sr.IsDeleted)
                    .FirstOrDefaultAsync(ct);

                if (review == null)
                {
                    _logger.LogWarning("Review {ReviewId} not found or user {UserId} doesn't own it", reviewId, userId);
                    return false;
                }

                review.Title = title;
                review.Rating = rating;
                review.Description = description;
                review.LastEditedAt = DateTime.UtcNow;
                review.Approved = false; // Require re-moderation after edit

                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("Store review {ReviewId} updated by user {UserId}", reviewId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating store review {ReviewId} for user {UserId}", reviewId, userId);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteReviewAsync(int reviewId, int userId, CancellationToken ct = default)
        {
            try
            {
                var review = await _db.StoreReviews
                    .Where(sr => sr.PkStoreReviewId == reviewId && sr.FkRegisteredUserId == userId && !sr.IsDeleted)
                    .FirstOrDefaultAsync(ct);

                if (review == null)
                {
                    _logger.LogWarning("Review {ReviewId} not found or user {UserId} doesn't own it", reviewId, userId);
                    return false;
                }

                review.IsDeleted = true;
                review.DeletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("Store review {ReviewId} soft-deleted by user {UserId}", reviewId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting store review {ReviewId} for user {UserId}", reviewId, userId);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<List<StoreReviewModel>> GetPendingReviewsAsync(int count = 50, CancellationToken ct = default)
        {
            return await _db.StoreReviews
                .AsNoTracking()
                .Include(sr => sr.RegisteredUser)
                .Where(sr => !sr.Approved && !sr.IsDeleted)
                .OrderBy(sr => sr.CreatedAt)
                .Take(count)
                .ToListAsync(ct);
        }

        /// <inheritdoc/>
        public async Task<bool> ApproveAsync(int reviewId, CancellationToken ct = default)
        {
            var review = await _db.StoreReviews
                .Where(sr => sr.PkStoreReviewId == reviewId && !sr.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (review == null) return false;

            review.Approved = true;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Store review {ReviewId} approved", reviewId);
            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> AdminDeleteAsync(int reviewId, CancellationToken ct = default)
        {
            var review = await _db.StoreReviews
                .Where(sr => sr.PkStoreReviewId == reviewId && !sr.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (review == null) return false;

            review.IsDeleted = true;
            review.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Store review {ReviewId} admin-deleted", reviewId);
            return true;
        }
    }
}
