namespace ELKH.ViewModels
{
    /// <summary>
    /// ViewModel for the kawaii hero banner component
    /// Supports customizable content and theming
    /// </summary>
    public class HeroBannerVM
    {
        /// <summary>
        /// Main title displayed in the cloud-style heading
        /// </summary>
        public string Title { get; set; } = "Welcome to ELKH";
        
        /// <summary>
        /// Subtitle displayed in the pill-style badge
        /// </summary>
        public string Subtitle { get; set; } = "Your favorite kawaii sticker store";
        
        /// <summary>
        /// Optional description text
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// Call-to-action button text
        /// </summary>
        public string? CtaText { get; set; }
        
        /// <summary>
        /// Controller for the CTA button link
        /// </summary>
        public string CtaController { get; set; } = "Product";
        
        /// <summary>
        /// Action for the CTA button link
        /// </summary>
        public string CtaAction { get; set; } = "Index";
        
        /// <summary>
        /// Whether to show sparkle effects
        /// </summary>
        public bool ShowSparkles { get; set; } = true;
        
        /// <summary>
        /// Background theme: mint, sky, or lavender
        /// </summary>
        public string BackgroundTheme { get; set; } = "mint";
        
        /// <summary>
        /// Optional feature highlights to display below the hero
        /// </summary>
        public List<HeroFeatureVM>? Features { get; set; }
    }
    
    /// <summary>
    /// Feature highlight for hero banner
    /// </summary>
    public class HeroFeatureVM
    {
        /// <summary>
        /// Icon class (e.g., "bi bi-heart")
        /// </summary>
        public string? Icon { get; set; }
        
        /// <summary>
        /// Feature title
        /// </summary>
        public string Title { get; set; } = string.Empty;
        
        /// <summary>
        /// Feature description
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }
}