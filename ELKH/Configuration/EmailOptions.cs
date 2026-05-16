namespace ELKH.Configuration
{
    /// <summary>
    /// SMTP email server configuration options.
    /// Provides settings for connecting to an email server and sending notifications.
    /// </summary>
    public class EmailOptions
    {
        /// <summary>SMTP server hostname (e.g., "smtp.gmail.com")</summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// SMTP server port number.
        /// Common ports: 587 (TLS), 465 (SSL), 25 (unencrypted - not recommended)
        /// </summary>
        public int Port { get; set; } = 587;

        /// <summary>
        /// Enable SSL/TLS encryption for secure email transmission.
        /// Default: true (secure by default)
        /// </summary>
        /// <remarks>
        /// Security: Always use SSL/TLS in production to protect credentials.
        /// Set to false only for local development/testing with trusted servers.
        /// </remarks>
        public bool EnableSsl { get; set; } = true;

        /// <summary>
        /// SMTP authentication username (optional for anonymous servers)
        /// </summary>
        /// <remarks>
        /// Security: Store in environment variables or Azure Key Vault, not appsettings.json
        /// </remarks>
        public string? User { get; set; }

        /// <summary>
        /// SMTP authentication password (optional for anonymous servers)
        /// </summary>
        /// <remarks>
        /// Security: Use user-secrets (development) or Azure Key Vault (production).
        /// NEVER commit passwords to source control!
        /// </remarks>
        public string? Pass { get; set; }

        /// <summary>
        /// Email address to use as sender for outgoing emails
        /// </summary>
        public string From { get; set; } = "no-reply@example.com";
    }
}
