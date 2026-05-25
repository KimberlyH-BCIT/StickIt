using System.Threading.Tasks;

namespace ELKH.Services
{
    /// <summary>
    /// Adapter that implements both the custom IEmailSender interface and the
    /// ASP.NET Core Identity IEmailSender interface, allowing a single email
    /// sender implementation to be used throughout the application.
    /// </summary>
    public class EmailSenderAdapter :
        ELKH.Services.IEmailSender,
        Microsoft.AspNetCore.Identity.UI.Services.IEmailSender
    {
        private readonly SmtpEmailSender _sender;

        /// <summary>
        /// Initializes a new instance of <see cref="EmailSenderAdapter"/>.
        /// </summary>
        /// <param name="sender">The concrete SMTP sender that performs the actual delivery.</param>
        public EmailSenderAdapter(SmtpEmailSender sender)
        {
            _sender = sender;
        }

        /// <summary>
        /// Implementation for ASP.NET Core Identity's IEmailSender interface.
        /// Converts the single email address to an array for the underlying sender.
        /// </summary>
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            return _sender.SendEmailAsync(new[] { email }, subject, htmlMessage, null);
        }

        /// <summary>
        /// Implementation for the custom IEmailSender interface that supports
        /// multiple recipients and custom from addresses.
        /// </summary>
        public Task SendEmailAsync(string[] recipients, string subject, string body, string? from = null)
        {
            return _sender.SendEmailAsync(recipients, subject, body, from);
        }
    }
}
