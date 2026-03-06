namespace ELKH.ViewModels
{
    /// <summary>
    /// Projection of a single approved review for display on the product details page.
    /// Carries reviewer profile data so the view does not need extra DB calls.
    /// </summary>
    public class ReviewDisplayVM
    {
        public int RatingId { get; set; }
        public int Rating { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastEditedAt { get; set; }
        public string ReviewerFirstName { get; set; } = string.Empty;
        public bool HasAvatar { get; set; }
        public int ReviewerUserId { get; set; }
    }

    /// <summary>
    /// A single page of approved reviews together with pagination metadata and
    /// the aggregate average rating computed from all approved reviews (not just
    /// this page), so the product header can display accurate stats.
    /// </summary>
    public class ReviewPageVM
    {
        public const int PageSize = 5;

        public List<ReviewDisplayVM> Reviews { get; set; } = [];
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        /// <summary>Average star rating across all approved reviews (0 when no reviews exist).</summary>
        public double AverageRating { get; set; }
    }
}
