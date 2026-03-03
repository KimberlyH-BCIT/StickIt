using System.Net.Mail;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ELKH.Services
{
    /// <summary>
    /// Production SMTP email sender backed by <see cref="System.Net.Mail.SmtpClient"/>.
    /// Reads host, port, credentials, and SSL settings from <see cref="ELKH.Configuration.EmailOptions"/>.
    /// Degrades gracefully when the SMTP host is not configured (logs and returns without throwing).
    /// </summary>
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SmtpEmailSender> _logger;

        private readonly ELKH.Configuration.EmailOptions _options;

        /// <summary>
        /// Initializes a new instance of <see cref="SmtpEmailSender"/>.
        /// </summary>
        /// <param name="config">Application configuration (retained for potential future use).</param>
        /// <param name="options">Strongly-typed SMTP settings bound from <c>appsettings.json</c>.</param>
        /// <param name="logger">Logger for delivery diagnostics and graceful skip notifications.</param>
        public SmtpEmailSender(IConfiguration config, Microsoft.Extensions.Options.IOptions<ELKH.Configuration.EmailOptions> options, ILogger<SmtpEmailSender> logger)
        {
            _config = config;
            _logger = logger;
            _options = options.Value;
        }

        /// <summary>
        /// Send an email using configured SMTP settings. If the SMTP host is not
        /// configured the method logs and returns without error to allow non-critical
        /// notification failures to be non-fatal.
        /// </summary>
        public async Task SendEmailAsync(string[] to, string subject, string body, string? from = null)
        {
            var host = _options.Host;
            if (string.IsNullOrEmpty(host))
            {
                _logger.LogInformation("SMTP host not configured; skipping email notification.");
                return;
            }

            var port = _options.Port;
            var user = _options.User;
            var pass = _options.Pass;
            var fromAddr = from ?? _options.From;

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = _options.EnableSsl,
            };

            if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
            {
                client.Credentials = new NetworkCredential(user, pass);
            }

            var msg = new MailMessage();
            msg.From = new MailAddress(fromAddr);
            foreach (var a in to) msg.To.Add(a);
            msg.Subject = subject;
            msg.Body = body;

            await client.SendMailAsync(msg);
        }
    }
}
