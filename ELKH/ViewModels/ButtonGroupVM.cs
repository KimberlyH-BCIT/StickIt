namespace ELKH.ViewModels
{
    /// <summary>
    /// ViewModel for the reusable Button Group component.
    /// Supports creating groups of buttons for sorting, filtering, navigation, etc.
    /// </summary>
    public class ButtonGroupVM
    {
        /// <summary>
        /// Optional title for the button group
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// CSS classes for the title
        /// </summary>
        public string TitleClass { get; set; } = "me-2 fw-semibold";

        /// <summary>
        /// CSS classes for the container
        /// </summary>
        public string ContainerClass { get; set; } = "mb-3";

        /// <summary>
        /// CSS classes for the button group
        /// </summary>
        public string ButtonGroupClass { get; set; } = "btn-group";

        /// <summary>
        /// CSS classes applied to active buttons
        /// </summary>
        public string ActiveButtonClass { get; set; } = "active";

        /// <summary>
        /// Aria label for accessibility
        /// </summary>
        public string AriaLabel { get; set; } = "Button group";

        /// <summary>
        /// List of buttons to display
        /// </summary>
        public List<ButtonItemVM> Buttons { get; set; } = new();
    }

    /// <summary>
    /// ViewModel representing a single button in a button group.
    /// </summary>
    public class ButtonItemVM
    {
        /// <summary>
        /// Display text for the button
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Optional Bootstrap icon name
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// CSS classes for the button
        /// </summary>
        public string CssClass { get; set; } = "btn btn-outline-primary";

        /// <summary>
        /// Whether this button is currently active/selected
        /// </summary>
        public bool IsActive { get; set; } = false;

        /// <summary>
        /// Controller for link buttons
        /// </summary>
        public string? Controller { get; set; }

        /// <summary>
        /// Action for link buttons
        /// </summary>
        public string? Action { get; set; }

        /// <summary>
        /// Route values for link buttons
        /// </summary>
        public Dictionary<string, object>? RouteValues { get; set; }

        /// <summary>
        /// Button type for form buttons (button, submit, reset)
        /// </summary>
        public string ButtonType { get; set; } = "button";

        /// <summary>
        /// JavaScript onclick handler
        /// </summary>
        public string? OnClick { get; set; }

        /// <summary>
        /// Additional data attributes (e.g., data-id="123")
        /// </summary>
        public string? DataAttributes { get; set; }

        /// <summary>
        /// Title attribute for tooltip
        /// </summary>
        public string? Title { get; set; }
    }
}