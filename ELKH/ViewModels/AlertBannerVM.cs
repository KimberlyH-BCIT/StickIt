namespace ELKH.ViewModels
{
    /// <summary>
    /// ViewModel for the reusable Alert Banner component.
    /// Supports various alert types including info, success, warning, and danger alerts.
    /// </summary>
    public class AlertBannerVM
    {
        /// <summary>
        /// The main message content (can include HTML)
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Optional title/heading for the alert
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Bootstrap alert type (alert-info, alert-success, alert-warning, alert-danger)
        /// </summary>
        public string AlertType { get; set; } = "alert-info";

        /// <summary>
        /// Icon to display (either Bootstrap icon name or emoji)
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// Type of icon: "bootstrap" or "emoji" (default: "bootstrap")
        /// </summary>
        public string IconType { get; set; } = "bootstrap";

        /// <summary>
        /// Size of the icon in pixels (default: 24)
        /// </summary>
        public int IconSize { get; set; } = 24;

        /// <summary>
        /// Aria label for the icon (accessibility)
        /// </summary>
        public string IconAriaLabel { get; set; } = "Info:";

        /// <summary>
        /// Whether the alert can be dismissed
        /// </summary>
        public bool Dismissible { get; set; } = false;

        /// <summary>
        /// Additional CSS classes
        /// </summary>
        public string CssClasses { get; set; } = "d-flex align-items-center mb-3";

        /// <summary>
        /// Whether the message should take full width
        /// </summary>
        public bool FullWidth { get; set; } = true;

        /// <summary>
        /// Optional action button text
        /// </summary>
        public string? ActionText { get; set; }

        /// <summary>
        /// Controller for the action
        /// </summary>
        public string? ActionController { get; set; }

        /// <summary>
        /// Action method for the action
        /// </summary>
        public string? ActionAction { get; set; }

        /// <summary>
        /// Optional route parameter for the action
        /// </summary>
        public object? ActionRouteId { get; set; }

        /// <summary>
        /// CSS classes for the action button
        /// </summary>
        public string ActionButtonClass { get; set; } = "btn btn-sm btn-outline-primary";
    }
}