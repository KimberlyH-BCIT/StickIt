namespace ELKH.ViewModels
{
    /// <summary>
    /// ViewModel for the reusable Empty State Card component.
    /// Provides all necessary properties for displaying empty states with icons, messages, and call-to-action buttons.
    /// </summary>
    public class EmptyStateCardVM
    {
        /// <summary>
        /// The main title/heading for the empty state
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Optional description text below the title
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
        /// Primary action button text
        /// </summary>
        public string? ActionText { get; set; }

        /// <summary>
        /// Controller for the primary action
        /// </summary>
        public string? ActionController { get; set; }

        /// <summary>
        /// Action method for the primary action
        /// </summary>
        public string? ActionAction { get; set; }

        /// <summary>
        /// Optional route parameter for the primary action
        /// </summary>
        public object? ActionRouteId { get; set; }

        /// <summary>
        /// CSS classes for the primary action button
        /// </summary>
        public string ActionButtonClass { get; set; } = "btn btn-primary";

        /// <summary>
        /// Optional icon for the primary action button
        /// </summary>
        public string? ActionIcon { get; set; }

        /// <summary>
        /// Secondary action button text (optional)
        /// </summary>
        public string? SecondaryActionText { get; set; }

        /// <summary>
        /// Controller for the secondary action
        /// </summary>
        public string? SecondaryActionController { get; set; }

        /// <summary>
        /// Action method for the secondary action
        /// </summary>
        public string? SecondaryActionAction { get; set; }

        /// <summary>
        /// CSS classes for the secondary action button
        /// </summary>
        public string SecondaryActionButtonClass { get; set; } = "btn btn-outline-secondary";
    }
}