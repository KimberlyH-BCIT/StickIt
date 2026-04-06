using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ELKH.Models;

namespace ELKH.Services
{
    /// <summary>
    /// Contract for store review operations (reviews about the website/store itself, not individual products).
    /// Handles submission, retrieval, and verified buyer validation.
    /// </summary>
    public interface IStoreReviewService
    {
        /// <summary>
        /// Submits a new store review from a user.
        /// Automatically determines verified buyer status based on user's order history.
        /// </summary>
        /// <param name="userId">Primary key of the user submitting the review.</param>
        /// <param name="title">Review title/headline (3-100 characters).</param>
        /// <param name="rating">Star rating between 1 and 5.</param>
        /// <param name="description">Review text (10-1000 characters).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// True if the review was created successfully and awaiting approval, false if user already has a pending/approved review.
        /// </returns>
        Task<bool> SubmitReviewAsync(int userId, string title, int rating, string description, CancellationToken ct = default);

        /// <summary>
        /// Retrieves approved store reviews for homepage display.
        /// Returns most recent reviews first, excludes deleted and unapproved reviews.
        /// </summary>
        /// <param name="count">Maximum number of reviews to return (default 10).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of approved store reviews with user information.</returns>
        Task<List<StoreReviewModel>> GetApprovedReviewsAsync(int count = 10, CancellationToken ct = default);

        /// <summary>
        /// Checks if user has any completed orders (for verified buyer badge).
        /// </summary>
        /// <param name="userId">Primary key of the user.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if user has at least one completed order.</returns>
        Task<bool> IsVerifiedBuyerAsync(int userId, CancellationToken ct = default);

        /// <summary>
        /// Gets a user's existing store review (if any) for editing.
        /// </summary>
        /// <param name="userId">Primary key of the user.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The user's store review or null if none exists.</returns>
        Task<StoreReviewModel?> GetUserReviewAsync(int userId, CancellationToken ct = default);

        /// <summary>
        /// Updates an existing store review.
        /// </summary>
        /// <param name="reviewId">Primary key of the review to update.</param>
        /// <param name="userId">User ID for ownership verification.</param>
        /// <param name="title">New review title.</param>
        /// <param name="rating">New star rating.</param>
        /// <param name="description">New review text.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if updated successfully, false if review not found or user doesn't own it.</returns>
        Task<bool> UpdateReviewAsync(int reviewId, int userId, string title, int rating, string description, CancellationToken ct = default);

        /// <summary>
        /// Soft-deletes a store review. Only the review owner may delete their own review.
        /// </summary>
        /// <param name="reviewId">Primary key of the review to delete.</param>
        /// <param name="userId">User ID for ownership verification.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if deleted successfully, false if not found or user doesn't own it.</returns>
        Task<bool> DeleteReviewAsync(int reviewId, int userId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves unapproved, non-deleted store reviews for staff moderation.
        /// </summary>
        /// <param name="count">Maximum number of reviews to return.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of pending store reviews ordered oldest-first.</returns>
        Task<List<StoreReviewModel>> GetPendingReviewsAsync(int count = 50, CancellationToken ct = default);

        /// <summary>
        /// Approves a store review, making it visible on the homepage.
        /// </summary>
        /// <param name="reviewId">Primary key of the review to approve.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if approved successfully, false if not found.</returns>
        Task<bool> ApproveAsync(int reviewId, CancellationToken ct = default);

        /// <summary>
        /// Staff/admin hard soft-delete of any review regardless of ownership.
        /// </summary>
        /// <param name="reviewId">Primary key of the review to delete.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if deleted successfully, false if not found.</returns>
        Task<bool> AdminDeleteAsync(int reviewId, CancellationToken ct = default);
    }
}
