using System.ComponentModel.DataAnnotations;
using ELKH.Models;

namespace ELKH.ViewModels
{
    /// <summary>
    /// View model for store review submission and editing.
    /// Used on the /User/LeaveReview page to display form and handle submissions.
    /// </summary>
    public class StoreReviewViewModel
    {
        /// <summary>
        /// Existing review if user has already submitted one (for editing).
        /// Null if this is a new review.
        /// </summary>
        public StoreReviewModel? ExistingReview { get; set; }

        /// <summary>
        /// Review ID when editing an existing review.
        /// Null when creating a new review.
        /// </summary>
        public int? ReviewId { get; set; }

        /// <summary>
        /// Whether the user is a verified buyer (has completed orders).
        /// </summary>
        public bool IsVerifiedBuyer { get; set; }

        /// <summary>
        /// Title or headline for the review.
        /// </summary>
        [Required(ErrorMessage = "Please enter a title for your review.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 100 characters.")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Star rating for the store (1-5).
        /// </summary>
        [Required(ErrorMessage = "Please select a rating.")]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
        public int Rating { get; set; } = 5;

        /// <summary>
        /// Review description/text.
        /// </summary>
        [Required(ErrorMessage = "Please write your review.")]
        [StringLength(1000, MinimumLength = 10, ErrorMessage = "Review must be between 10 and 1000 characters.")]
        [DataType(DataType.MultilineText)]
        public string Description { get; set; } = string.Empty;
    }
}
