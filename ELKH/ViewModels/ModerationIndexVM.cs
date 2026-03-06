using ELKH.Models;
using ELKH.Services;

namespace ELKH.ViewModels
{
    /// <summary>
    /// Strongly-typed model for the moderation console index page.
    /// Replaces the previous ViewBag-based approach.
    /// </summary>
    public class ModerationIndexVM
    {
        /// <summary>The page of ratings returned by the current query.</summary>
        public IEnumerable<ProductRatingModel> Items { get; set; } = [];
        /// <summary>The active filter/sort/paging state, used to rebuild form values and pagination links.</summary>
        public RatingQuery Query { get; set; } = new();
        /// <summary>Total number of pages for the current query, used to render pagination controls.</summary>
        public int TotalPages { get; set; }
    }
}
