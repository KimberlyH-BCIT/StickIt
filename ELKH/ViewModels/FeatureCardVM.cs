namespace ELKH.ViewModels
{
    /// <summary>
    /// ViewModel for the reusable Feature Card component.
    /// Used for creating feature cards with icons, titles, descriptions, and optional actions.
    /// Commonly used in dashboards, landing pages, and feature showcases.
    /// </summary>
    public class FeatureCardVM
    {
        /// <summary>
        /// Bootstrap column size (e.g., "md-4", "lg-3", "12")
        /// </summary>
        public string ColumnSize { get; set; } = "md-4";

        /// <summary>
        /// The main title of the feature card
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Optional description text
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Icon to display (either Bootstrap icon name or emoji)
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// Type of icon: "bootstrap" or "emoji" (default: "bootstrap")
        /// </summary>
        public string IconType { get; set; } = "bootstrap";

        /// <summary>
        /// Size of the icon in rem units (default: 2.4)
        /// </summary>
        public double IconSize { get; set; } = 2.4;

        /// <summary>
        /// CSS classes for the icon wrapper (e.g., "bg-primary bg-opacity-10 rounded-4 p-3")
        /// </summary>
        public string IconWrapperClass { get; set; } = "bg-primary bg-opacity-10 rounded-4 p-3";

        /// <summary>
        /// Optional badge text
        /// </summary>
        public string? Badge { get; set; }

        /// <summary>
        /// CSS classes for the badge
        /// </summary>
        public string BadgeCssClass { get; set; } = "badge bg-primary bg-opacity-10 text-primary px-3 py-1 rounded-pill small";

        /// <summary>
        /// Optional footer text
        /// </summary>
        public string? FooterText { get; set; }

        /// <summary>
        /// CSS classes for the footer
        /// </summary>
        public string FooterCssClass { get; set; } = "bg-primary text-white text-center py-2 rounded-bottom-4 fw-semibold small";

        /// <summary>
        /// Additional CSS classes for the card
        /// </summary>
        public string CardCssClass { get; set; } = string.Empty;

        /// <summary>
        /// Controller for the card action (makes entire card clickable)
        /// </summary>
        public string? ActionController { get; set; }

        /// <summary>
        /// Action method for the card action
        /// </summary>
        public string? ActionAction { get; set; }

        /// <summary>
        /// Direct URL for the card action (alternative to controller/action)
        /// </summary>
        public string? ActionUrl { get; set; }

        /// <summary>
        /// Route values for the action
        /// </summary>
        public Dictionary<string, object>? ActionRouteValues { get; set; }
    }
}
