namespace ELKH.ViewModels
{
    /// <summary>
    /// ViewModel for the kawaii badge component
    /// Supports different badge types with customizable styling
    /// </summary>
    public class BadgeVM
    {
        /// <summary>
        /// Text displayed in the badge
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Badge type: new, sale, best, featured, limited, promo, category, hot, default
        /// </summary>
        public string Type { get; set; } = "default";

        /// <summary>
        /// Whether to show the icon associated with the badge type
        /// </summary>
        public bool ShowIcon { get; set; } = true;

        /// <summary>
        /// Additional CSS classes to apply
        /// </summary>
        public string? CustomClass { get; set; }

        /// <summary>
        /// Data attributes for JavaScript interactions
        /// </summary>
        public Dictionary<string, string>? DataAttributes { get; set; }

        /// <summary>
        /// Size variation: sm, default, lg
        /// </summary>
        public string Size { get; set; } = "default";

        /// <summary>
        /// Whether the badge is clickable/interactive
        /// </summary>
        public bool IsClickable { get; set; }

        /// <summary>
        /// URL for clickable badges
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Controller for MVC routing (for clickable badges)
        /// </summary>
        public string? Controller { get; set; }

        /// <summary>
        /// Action for MVC routing (for clickable badges)
        /// </summary>
        public string? Action { get; set; }

        /// <summary>
        /// Route values for MVC routing (for clickable badges)
        /// </summary>
        public object? RouteValues { get; set; }
    }
}
