namespace ELKH.Services
{
    /// <summary>
    /// Encapsulates all filter, sort, and paging parameters for a moderation rating query.
    /// Passed to IRatingService.GetRatingsPagedAsync() to keep IQueryable inside the service.
    /// </summary>
    public class RatingQuery
    {
        /// <summary>"all" | "flagged" | "unapproved" — see <see cref="ELKH.Constants.RatingFilter"/></summary>
        public string Filter { get; set; } = Constants.RatingFilter.All;
        public int? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? UserEmail { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? RatingMin { get; set; }
        public int? RatingMax { get; set; }
        /// <summary>"date_desc" | "date_asc" | "rating_desc" | "rating_asc" — see <see cref="ELKH.Constants.RatingSort"/></summary>
        public string Sort { get; set; } = Constants.RatingSort.DateDesc;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
