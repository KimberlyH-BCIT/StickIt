using Microsoft.AspNetCore.Html;

namespace ELKH.ViewModels
{
    /// <summary>
    /// ViewModel for the reusable Data Table component.
    /// Supports sortable headers, custom styling, and no-data states.
    /// </summary>
    public class DataTableVM
    {
        /// <summary>
        /// Optional title for the table
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// CSS classes for the title
        /// </summary>
        public string TitleClass { get; set; } = "mb-3";

        /// <summary>
        /// CSS classes for the container
        /// </summary>
        public string ContainerClass { get; set; } = string.Empty;

        /// <summary>
        /// CSS classes for the table element
        /// </summary>
        public string TableClass { get; set; } = "table table-borderless mb-0";

        /// <summary>
        /// CSS classes for the table header
        /// </summary>
        public string HeaderClass { get; set; } = "table-light";

        /// <summary>
        /// CSS classes for the table body
        /// </summary>
        public string BodyClass { get; set; } = string.Empty;

        /// <summary>
        /// Table headers configuration
        /// </summary>
        public List<DataTableHeaderVM> Headers { get; set; } = new();

        /// <summary>
        /// The body content (rows) of the table - this should be the HTML content
        /// </summary>
        public IHtmlContent BodyContent { get; set; } = HtmlString.Empty;

        /// <summary>
        /// Whether the table has data
        /// </summary>
        public bool HasData { get; set; } = true;

        /// <summary>
        /// Whether to show no data message
        /// </summary>
        public bool ShowNoDataMessage { get; set; } = true;

        /// <summary>
        /// Message to show when there's no data
        /// </summary>
        public string NoDataMessage { get; set; } = "No data available.";

        /// <summary>
        /// Icon to show when there's no data
        /// </summary>
        public string NoDataIcon { get; set; } = "table";
    }

    /// <summary>
    /// ViewModel representing a table header column.
    /// </summary>
    public class DataTableHeaderVM
    {
        /// <summary>
        /// Display text for the header
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// CSS classes for the header cell
        /// </summary>
        public string CssClass { get; set; } = string.Empty;

        /// <summary>
        /// Controller for sortable headers
        /// </summary>
        public string? SortController { get; set; }

        /// <summary>
        /// Action for sortable headers
        /// </summary>
        public string? SortAction { get; set; }

        /// <summary>
        /// Sort value parameter
        /// </summary>
        public string? SortValue { get; set; }

        /// <summary>
        /// Additional route values for sorting
        /// </summary>
        public Dictionary<string, object>? SortRouteValues { get; set; }

        /// <summary>
        /// Whether this is the currently active sort column
        /// </summary>
        public bool IsActiveSortColumn { get; set; }

        /// <summary>
        /// Sort direction for active column ("asc" or "desc")
        /// </summary>
        public string SortDirection { get; set; } = "asc";
    }
}
