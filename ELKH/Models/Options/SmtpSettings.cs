namespace ELKH.Models.Options
{
    /// <summary>
    /// SMTP server configuration settings for email delivery.
    /// Contains connection details and authentication credentials for outbound email.
    /// </summary>
    /// <remarks>
    /// This configuration enables the application to send transactional emails
    /// such as order confirmations, password resets, and account notifications
    /// through an SMTP server (e.g., SendGrid, Mailgun, or corporate mail server).
    /// 
    /// <para><strong>Security Requirements:</strong></para>
    /// <list type="bullet">
    /// <item>Username and Password must be configured via secure methods</item>
    /// <item>Use app passwords or API keys instead of personal passwords</item>
    /// <item>Enable TLS/SSL encryption for secure email transmission</item>
    /// <item>Validate server certificates to prevent man-in-the-middle attacks</item>
    /// </list>
    /// 
    /// <para><strong>Common SMTP Providers:</strong></para>
    /// <list type="bullet">
    /// <item>SendGrid - smtp.sendgrid.net:587 (TLS)</item>
    /// <item>Mailgun - smtp.mailgun.org:587 (TLS)</item>
    /// <item>Gmail - smtp.gmail.com:587 (TLS with app password)</item>
    /// <item>Outlook/Hotmail - smtp-mail.outlook.com:587 (TLS)</item>
    /// </list>
    /// 
    /// <para><strong>Configuration Example:</strong></para>
    /// <code>
    /// {
    ///   "Smtp": {
    ///     "Server": "smtp.sendgrid.net",
    ///     "Port": 587,
    ///     "SenderName": "ELKH Store",
    ///     "SenderEmail": "noreply@elkh.com",
    ///     "Username": "apikey",
    ///     "Password": "your-sendgrid-api-key"
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public class SmtpSettings
    {
        /// <summary>
        /// SMTP server hostname or IP address.
        /// </summary>
        /// <remarks>
        /// The fully qualified domain name or IP address of the SMTP server.
        /// Examples: smtp.sendgrid.net, smtp.gmail.com, mail.company.com
        /// </remarks>
        public string Server { get; set; } = "";

        /// <summary>
        /// SMTP server port number.
        /// </summary>
        /// <remarks>
        /// Common ports:
        /// - 25: Standard SMTP (often blocked by ISPs)
        /// - 587: SMTP with STARTTLS (recommended)
        /// - 465: SMTP over SSL (legacy)
        /// </remarks>
        public int Port { get; set; }

        /// <summary>
        /// Display name for outgoing emails.
        /// </summary>
        /// <remarks>
        /// The friendly name shown in email clients as the sender.
        /// Example: "ELKH Store", "Customer Service", "ELKH Notifications"
        /// </remarks>
        public string SenderName { get; set; } = "";

        /// <summary>
        /// Email address used as the sender for outgoing emails.
        /// </summary>
        /// <remarks>
        /// Must be an email address authorized to send through the SMTP server.
        /// Often configured as noreply@domain.com for transactional emails.
        /// </remarks>
        public string SenderEmail { get; set; } = "";

        /// <summary>
        /// Username for SMTP authentication.
        /// </summary>
        /// <remarks>
        /// Authentication credentials for the SMTP server.
        /// For API-based services like SendGrid, this is often "apikey".
        /// Must be configured via secure configuration providers.
        /// </remarks>
        public string Username { get; set; } = "";

        /// <summary>
        /// Password for SMTP authentication.
        /// </summary>
        /// <remarks>
        /// Authentication password or API key for the SMTP server.
        /// Must be kept confidential and configured via user secrets or environment variables.
        /// For Gmail, use app-specific passwords instead of account passwords.
        /// </remarks>
        public string Password { get; set; } = "";
    }
}