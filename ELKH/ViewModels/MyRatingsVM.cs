namespace ELKH.ViewModels
{
    /// <summary>
    /// A single rating row as shown on the "My Ratings" page.
    /// </summary>
    public class UserRatingVM
    {
        /// <summary>Primary key of this rating record.</summary>
        public int RatingId { get; set; }

        /// <summary>Primary key of the product this rating belongs to - used to build the detail link.</summary>
        public int ProductId { get; set; }

        /// <summary>Display name of the rated product.</summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>1-5 star value submitted by the user.</summary>
        public int Rating { get; set; }

        /// <summary>Written review text.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>When the rating was submitted.</summary>
        public DateTime RatedTime { get; set; }

        /// <summary>Date of the originating purchase (null if order info is unavailable).</summary>
        public DateTime? PurchaseDate { get; set; }

        /// <summary>Whether a moderator has approved this review for public display.</summary>
        public bool Approved { get; set; }

        /// <summary>Whether a moderator has flagged this review for further review.</summary>
        public bool IsFlagged { get; set; }
    }

    /// <summary>
    /// View model for the "My Ratings" page - the full sorted list plus the active sort key.
    /// </summary>
    public class MyRatingsVM
    {
        /// <summary>All ratings submitted by the current user, in the order specified by <see cref="CurrentSort"/>.</summary>
        public List<UserRatingVM> Ratings { get; set; } = [];

        /// <summary>
        /// Active sort key. Accepted values:
        /// purchase_desc (default), purchase_asc,
        /// name_asc, name_desc,
        /// rating_high, rating_low
        /// </summary>
        public string CurrentSort { get; set; } = "purchase_desc";

        /// <summary>Products from user's order history that haven't been rated yet.</summary>
        public List<ProductToReviewVM> ProductsToReview { get; set; } = [];
    }

    /// <summary>
    /// Lightweight product suggestion for products waiting to be reviewed.
    /// </summary>
    public class ProductToReviewVM
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public DateTime PurchaseDate { get; set; }
        public int OrderId { get; set; }
    }
}
