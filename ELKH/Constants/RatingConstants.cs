namespace ELKH.Constants
{
    /// <summary>
    /// Canonical string constants for rating filter and sort query parameters.
    /// Use these instead of inline string literals throughout the application.
    /// </summary>
    public static class RatingFilter
    {
        public const string All = "all";
        public const string Flagged = "flagged";
        public const string Unapproved = "unapproved";
    }

    /// <summary>
    /// Canonical string constants for rating sort query parameters.
    /// </summary>
    public static class RatingSort
    {
        public const string DateDesc = "date_desc";
        public const string DateAsc = "date_asc";
        public const string RatingDesc = "rating_desc";
        public const string RatingAsc = "rating_asc";
    }
}
