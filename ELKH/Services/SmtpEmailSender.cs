using System.Net;
using System.Net.Mail;
using ELKH.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELKH.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly ELKH.Configuration.EmailOptions _options;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(
            IOptions<ELKH.Configuration.EmailOptions> options,
            ILogger<SmtpEmailSender> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string[] to, string subject, string body, string? from = null)
        {
            var host = _options.Host;
            if (string.IsNullOrWhiteSpace(host))
            {
                _logger.LogInformation("SMTP host not configured; skipping email notification.");
                return;
            }

            using var client = new SmtpClient(host, _options.Port)
            {
                EnableSsl = _options.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(_options.User) && !string.IsNullOrWhiteSpace(_options.Pass))
            {
                client.Credentials = new NetworkCredential(_options.User, _options.Pass);
            }

            using var msg = new MailMessage
            {
                From = new MailAddress(from ?? _options.From),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            foreach (var address in to)
            {
                msg.To.Add(address);
            }

            await client.SendMailAsync(msg);
        }
    }
}