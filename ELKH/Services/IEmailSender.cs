using System.Threading.Tasks;

namespace ELKH.Services
{
    /// <summary>
    /// Abstraction over email delivery, decoupling callers from any specific transport
    /// (SMTP, file-based dev sender, etc.).
    /// </summary>
    public interface IEmailSender
    {
        /// <summary>
        /// Sends an email to the specified recipients.
        /// Implementations should degrade gracefully when transport is not configured
        /// (e.g., log and return rather than throw).
        /// </summary>
        Task SendEmailAsync(string[] to, string subject, string body, string? from = null);
    }
}
