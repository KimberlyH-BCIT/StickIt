using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ELKH.Models;
using ELKH.ViewModels;

namespace ELKH.Services
{
    /// <summary>
    /// Contract for product rating and review operations.
    /// Covers the full lifecycle: creation, editing, soft-deletion, moderation approval,
    /// and purchase-eligibility checks.
    /// </summary>
    public interface IRatingService
    {
        /// <summary>
        /// Returns a paged, filtered, and sorted list of ratings for the admin moderation view.
        /// All filter, sort, and pagination parameters are encapsulated in <paramref name="query"/>.
        /// </summary>
        Task<PagedResult<ProductRatingModel>> GetRatingsPagedAsync(RatingQuery query, CancellationToken ct = default);

        /// <summary>
        /// Returns a single rating by primary key, or <see langword="null"/> if not found.
        /// </summary>
        Task<ProductRatingModel?> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Marks a rating as approved and clears any active moderation flag.
        /// Returns the updated entity, or <see langword="null"/> if the rating does not exist.
        /// </summary>
        Task<ProductRatingModel?> ApproveAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Returns all non-deleted ratings submitted by the specified user, enriched with
        /// the purchase date of each associated order item for the user dashboard display.
        /// </summary>
        Task<List<UserRatingVM>> GetUserRatingsAsync(int userId, CancellationToken ct = default);

        /// <summary>
        /// Submits a new product rating on behalf of a user, subject to ownership and cooldown rules.
        /// </summary>
        /// <param name="productId">The product being rated.</param>
        /// <param name="orderItemId">The fulfilled order item that grants eligibility to rate.</param>
        /// <param name="rating">Star value between 1 and 5 inclusive.</param>
        /// <param name="description">Optional review text.</param>
        /// <param name="userId">Primary key of the submitting user.</param>
        /// <returns>
        /// <see cref="RatingOperationResult"/> with <c>Success = true</c> and the product ID on success,
        /// or <c>Success = false</c> with a human-readable rejection message.
        /// </returns>
        Task<RatingOperationResult> CreateRatingAsync(int productId, int orderItemId, int rating, string? description, int userId, CancellationToken ct = default);

        /// <summary>
        /// Updates the star value and description of an existing rating.
        /// Ownership is enforced (only the original author may edit) and a 7-day cooldown applies.
        /// </summary>
        /// <returns>
        /// <see cref="RatingOperationResult"/> carrying the product ID for post-edit redirect.
        /// <c>ProductId = 0</c> signals the rating was not found.
        /// </returns>
        Task<RatingOperationResult> EditRatingAsync(int ratingId, int rating, string? description, int userId, CancellationToken ct = default);

        /// <summary>
        /// Soft-deletes a rating by setting <c>IsDeleted = true</c> and recording the deletion timestamp.
        /// Ownership is enforced: only the original author may delete.
        /// </summary>
        /// <returns>
        /// <see cref="RatingOperationResult"/> carrying the product ID for post-delete redirect.
        /// <c>ProductId = 0</c> signals the rating was not found.
        /// </returns>
        Task<RatingOperationResult> DeleteRatingAsync(int ratingId, int userId, CancellationToken ct = default);

        /// <summary>
        /// Returns all approved, non-deleted reviews for a product, ordered newest-first.
        /// </summary>
        Task<List<Models.ProductRatingModel>> GetApprovedReviewsAsync(int productId, CancellationToken ct = default);

        /// <summary>
        /// Returns a page of approved reviews enriched with reviewer profile data
        /// (first name, avatar flag) together with pagination metadata and the
        /// aggregate average rating computed across <em>all</em> approved reviews.
        /// </summary>
        Task<ViewModels.ReviewPageVM> GetPagedApprovedReviewsAsync(int productId, int page, CancellationToken ct = default);

        /// <summary>
        /// Determines whether the user is eligible to submit a new rating for a product
        /// based on their purchase history and any existing ratings.
        /// </summary>
        /// <returns>
        /// A <see cref="ViewModels.RatingEligibilityVM"/> containing the list of eligible order items
        /// (for the create-form dropdown) and any existing non-deleted rating (for the edit form).
        /// </returns>
        Task<ViewModels.RatingEligibilityVM> GetRatingEligibilityAsync(int productId, int userId, CancellationToken ct = default);
        Task<List<ProductRatingModel>> GetAllRatingsAsync(CancellationToken ct = default);
        Task<bool> MarkAsReadAsync(int id, CancellationToken ct = default);
    }
}
