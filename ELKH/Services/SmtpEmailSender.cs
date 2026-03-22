using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELKH.Services
{
    /// <summary>
    /// Production SMTP email sender backed by MailKit's <see cref="SmtpClient"/>.
    /// Reads host, port, credentials, and SSL settings from <see cref="ELKH.Configuration.EmailOptions"/>.
    /// Degrades gracefully when the SMTP host is not configured (logs and returns without throwing).
    /// </summary>
    public class SmtpEmailSender : IEmailSender
    {
        private readonly ILogger<SmtpEmailSender> _logger;
        private readonly ELKH.Configuration.EmailOptions _options;

        /// <summary>
        /// Initializes a new instance of <see cref="SmtpEmailSender"/>.
        /// </summary>
        /// <param name="options">Strongly-typed SMTP settings bound from <c>appsettings.json</c>.</param>
        /// <param name="logger">Logger for delivery diagnostics and graceful skip notifications.</param>
        public SmtpEmailSender(Microsoft.Extensions.Options.IOptions<ELKH.Configuration.EmailOptions> options, ILogger<SmtpEmailSender> logger)
        {
            _logger  = logger;
            _options = options.Value;
        }

        public async Task SendEmailAsync(string[] to, string subject, string body, string? from = null)
        {
            var host = _options.Host;
            if (string.IsNullOrWhiteSpace(host))
            {
                _logger.LogInformation("SMTP host not configured; skipping email notification.");
                return;
            }

            var fromAddr = from ?? _options.From;

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(fromAddr));
            foreach (var address in to)
                message.To.Add(MailboxAddress.Parse(address));
            message.Subject = subject;
            message.Body    = new TextPart("html") { Text = body };

            // Choose the correct socket security based on port convention:
            //   465  → implicit TLS (SslOnConnect)
            //   587  → STARTTLS upgrade (StartTls)
            //   other with SSL off → no encryption
            var socketOptions = _options.EnableSsl
                ? (_options.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls)
                : SecureSocketOptions.None;

            using var client = new SmtpClient();
            await client.ConnectAsync(host, _options.Port, socketOptions);

            if (!string.IsNullOrEmpty(_options.User) && !string.IsNullOrEmpty(_options.Pass))
                await client.AuthenticateAsync(_options.User, _options.Pass);

            await client.SendAsync(message);
            await client.DisconnectAsync(quit: true);
        }
    }
}