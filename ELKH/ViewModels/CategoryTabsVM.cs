namespace ELKH.ViewModels
{
    /// <summary>
    /// ViewModel for the kawaii category tabs component
    /// Supports customizable categories with icons and product counts
    /// </summary>
    public class CategoryTabsVM
    {
        /// <summary>
        /// Optional title for the category section
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Optional description text
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// List of categories to display as tabs
        /// </summary>
        public List<CategoryTabItem> Categories { get; set; } = new List<CategoryTabItem>();

        /// <summary>
        /// Whether to show active filters indicator
        /// </summary>
        public bool ShowActiveFilters { get; set; } = true;

        /// <summary>
        /// Whether to show product counts in tabs
        /// </summary>
        public bool ShowProductCounts { get; set; } = true;

        /// <summary>
        /// Whether to show the promotions tab
        /// </summary>
        public bool ShowPromotionsTab { get; set; } = true;

        /// <summary>
        /// Whether to show the "All Products" tab
        /// </summary>
        public bool ShowAllProductsTab { get; set; } = true;

        /// <summary>
        /// Whether to show the "View All Categories" tab
        /// </summary>
        public bool ShowViewAllTab { get; set; } = true;

        /// <summary>
        /// Maximum number of categories to show (others will be in mobile dropdown)
        /// </summary>
        public int MaxVisibleCategories { get; set; } = 8;
    }

    /// <summary>
    /// Individual category tab item
    /// </summary>
    public class CategoryTabItem
    {
        /// <summary>
        /// Category ID for routing
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Category name to display
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional icon class (e.g., "bi bi-heart")
        /// </summary>
        public string? IconClass { get; set; }

        /// <summary>
        /// Number of products in this category
        /// </summary>
        public int ProductCount { get; set; }

        /// <summary>
        /// Whether this category is currently active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Optional custom CSS class
        /// </summary>
        public string? CustomClass { get; set; }

        /// <summary>
        /// Optional description for accessibility
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Category URL slug for SEO-friendly URLs
        /// </summary>
        public string? UrlSlug { get; set; }

        /// <summary>
        /// Whether to show this category in mobile view
        /// </summary>
        public bool ShowInMobile { get; set; } = true;

        /// <summary>
        /// Sort order for display
        /// </summary>
        public int SortOrder { get; set; }
    }
}
