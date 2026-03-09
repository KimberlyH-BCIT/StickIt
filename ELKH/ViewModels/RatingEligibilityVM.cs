namespace ELKH.ViewModels
{
    /// <summary>
    /// Represents which order items a user can still rate for a product
    /// and their existing active rating (if any).
    /// Returned by IRatingService.GetRatingEligibilityAsync().
    /// </summary>
    public class RatingEligibilityVM
    {
        /// <summary>Order items the user is still permitted to rate for this product.</summary>
        public List<EligibleOrderItemVM> EligibleItems { get; set; } = [];
        /// <summary>The user's first active (non-deleted) rating for this product, or null.</summary>
        public ELKH.Models.ProductRatingModel? ExistingRating { get; set; }
    }

    /// <summary>An order item the user is still eligible to rate.</summary>
    public class EligibleOrderItemVM
    {
        public int Id { get; set; }
        /// <summary>Human-readable label shown in the rating form dropdown.</summary>
        public string Label { get; set; } = string.Empty;
    }
}
