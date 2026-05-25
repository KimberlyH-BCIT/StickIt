using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a customer review about the store/website itself (not a specific product).
    /// Used for displaying testimonials on the homepage and gathering general feedback.
    /// </summary>
    public class StoreReviewModel
    {
        /// <summary>
        /// Unique identifier for the store review (primary key).
        /// </summary>
        [Key]
        public int PkStoreReviewId { get; set; }

        /// <summary>
        /// Title or headline for the review (e.g., "Great service!" or "Best stickers ever!").
        /// </summary>
        [Required(ErrorMessage = "Review title is required.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 100 characters.")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Textual review or comment about the store experience.
        /// </summary>
        [Required(ErrorMessage = "Review description is required.")]
        [StringLength(1000, MinimumLength = 10, ErrorMessage = "Review must be between 10 and 1000 characters.")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Numeric rating value for the store (1-5 stars).
        /// </summary>
        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public int Rating { get; set; } = 0;

        /// <summary>
        /// Timestamp when the review was submitted.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp of the last edit (if any).
        /// </summary>
        public DateTime? LastEditedAt { get; set; }

        /// <summary>
        /// Whether the review is approved and visible to the public.
        /// All reviews require moderation approval before appearing on homepage.
        /// </summary>
        public bool Approved { get; set; }

        /// <summary>
        /// Whether the review is flagged for moderator attention.
        /// </summary>
        public bool IsFlagged { get; set; }

        /// <summary>
        /// Whether the reviewer is a verified buyer (has any completed order).
        /// Calculated at submission time based on user's order history.
        /// </summary>
        public bool IsVerifiedBuyer { get; set; }

        /// <summary>
        /// Optional moderator note explaining flags or rejection reasons.
        /// </summary>
        public string ModeratorNote { get; set; } = string.Empty;

        /// <summary>
        /// Whether the review has been soft-deleted.
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// Timestamp when the review was soft-deleted (if applicable).
        /// </summary>
        public DateTime? DeletedAt { get; set; }

        // Relationships

        /// <summary>
        /// Foreign key to the registered user who submitted the review.
        /// </summary>
        public int FkRegisteredUserId { get; set; }

        /// <summary>
        /// Navigation property to the registered user who submitted the review.
        /// </summary>
        public RegisteredUserModel RegisteredUser { get; set; } = null!;
    }
}
