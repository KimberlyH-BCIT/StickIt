using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a customer rating and review for a product.
    /// Includes moderation flags, links to product, user, and order item.
    /// </summary>
    public class ProductRatingModel
    {
        /// <summary>
        /// Unique identifier for the rating (primary key).
        /// </summary>
        [Key]
        public int PkRatingId { get; set; }

        /// <summary>
        /// Textual review or comment left by the customer.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Numeric rating value (1-5 stars).
        /// </summary>
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public int Rating { get; set; } = 0;

        /// <summary>
        /// Timestamp when the rating was submitted.
        /// </summary>
        public DateTime RatedTime { get; set; } = DateTime.UtcNow;

        // Moderation flags

        /// <summary>
        /// Whether the rating is approved and visible to the public.
        /// </summary>
        public bool Approved { get; set; } = false;
        public bool IsRead { get; set; } = false;
        /// <summary>
        /// Whether the rating is flagged for moderator review.
        /// </summary>
        public bool IsFlagged { get; set; } = false;

        /// <summary>
        /// Optional note left by a moderator explaining why the rating was flagged.
        /// </summary>
        public string ModeratorNote { get; set; } = string.Empty;

        // Relationships

        /// <summary>
        /// Foreign key to the rated product.
        /// </summary>
        public int FkProductId { get; set; }
        public ProductModel? Products { get; set; }

        /// <summary>
        /// Foreign key to the registered user who submitted the rating.
        /// </summary>
        public int FkRegisteredUserId { get; set; }

        /// <summary>
        /// Navigation property to the registered user who submitted the rating.
        /// </summary>
        public RegisteredUserModel RegisteredUser { get; set; } = null!;

        /// <summary>
        /// Foreign key to the order item this rating is about (one rating per order item).
        /// </summary>
        public int? FkOrderItemId { get; set; }

        // Editing/deletion metadata

        /// <summary>
        /// Timestamp of the last edit (if any).
        /// </summary>
        public DateTime? LastEditedAt { get; set; }

        /// <summary>
        /// Whether the rating has been soft-deleted by the user.
        /// </summary>
        public bool IsDeleted { get; set; } = false;

        /// <summary>
        /// Timestamp when the rating was deleted (if deleted).
        /// </summary>
        public DateTime? DeletedAt { get; set; }
        public string UserId { get; set; }
       
    }
}
