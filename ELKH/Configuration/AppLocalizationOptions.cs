namespace ELKH.Configuration
{
    /// <summary>
    /// Localization defaults bound from the "Localization" appsettings section.
    /// Exposes only the fields the application needs so views never touch IConfiguration directly.
    /// </summary>
    public class AppLocalizationOptions
    {
        /// <summary>Default culture code (e.g. "en-CA").</summary>
        public string DefaultCulture { get; set; } = "en-CA";

        /// <summary>ISO 4217 currency code used when no user preference is set (e.g. "CAD").</summary>
        public string DefaultCurrency { get; set; } = "CAD";

        /// <summary>All culture codes the application supports.</summary>
        public string[] SupportedCultures { get; set; } = ["en-CA"];
    }
}
