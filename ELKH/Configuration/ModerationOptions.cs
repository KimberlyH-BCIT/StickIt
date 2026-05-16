namespace ELKH.Configuration
{
    /// <summary>
    /// Moderation system configuration options.
    /// Configures email notifications and base URLs for moderation links.
    /// </summary>
    public class ModerationOptions
    {
        /// <summary>
        /// List of admin email addresses to receive moderation notifications.
        /// Notifications are sent when content is flagged for review.
        /// </summary>
        /// <example>
        /// ["admin@example.com", "moderator@example.com"]
        /// </example>
        public string[] AdminEmails { get; set; } = [];

        /// <summary>
        /// Base URL for the application, used to generate absolute links in email notifications.
        /// Must include protocol (http/https) and domain.
        /// </summary>
        /// <remarks>
        /// Security: This value MUST come from trusted configuration only.
        /// Never accept user input for BaseUrl (open redirect vulnerability).
        /// Value is validated by ModerationRoutes.GetSafeBaseUrl() before use.
        /// </remarks>
        /// <example>
        /// "https://myapp.com"
        /// </example>
        public string BaseUrl { get; set; } = string.Empty;
    }
}
