using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ELKH.Services
{
    /// <summary>
    /// Development-only email sender that writes email content to <c>.eml.txt</c> files on disk
    /// instead of delivering via SMTP. Allows developers to inspect all outgoing emails without
    /// requiring a mail server. Implements both <see cref="IEmailSender"/> and the ASP.NET Core
    /// Identity <see cref="Microsoft.AspNetCore.Identity.UI.Services.IEmailSender"/> interface.
    /// </summary>
    public class FileEmailSender : IEmailSender, Microsoft.AspNetCore.Identity.UI.Services.IEmailSender
    {
        private readonly ILogger<FileEmailSender> _logger;
        private readonly string _directory;

        /// <summary>
        /// Initializes a new instance of <see cref="FileEmailSender"/> and resolves the
        /// output directory from configuration, falling back to <c>ContentRoot/SavedEmails</c>
        /// and then to <see cref="Path.GetTempPath"/> if the directory cannot be created.
        /// </summary>
        /// <param name="env">Hosting environment used to resolve relative paths from content root.</param>
        /// <param name="config">Application configuration; reads <c>Email:SaveDirectory</c>.</param>
        /// <param name="logger">Logger for directory-creation warnings and file-save confirmations.</param>
        public FileEmailSender(IHostEnvironment env, IConfiguration config, ILogger<FileEmailSender> logger)
        {
            _logger = logger;

            // Prefer the configured directory; treat relative paths as relative to ContentRoot.
            var configured = config.GetValue<string>("Email:SaveDirectory");
            if (!string.IsNullOrEmpty(configured))
            {
                _directory = Path.IsPathRooted(configured) ? configured : Path.Combine(env.ContentRootPath, configured);
            }
            else
            {
                _directory = Path.Combine(env.ContentRootPath, "SavedEmails");
            }

            try
            {
                Directory.CreateDirectory(_directory);
            }
            catch (Exception ex)
            {
                // If directory creation fails (e.g., permission denied), fall back to the OS temp
                // path so the service remains functional in restricted environments.
                _logger.LogWarning(ex, "Failed to create email save directory {Dir}. Falling back to temp path.", _directory);
                _directory = Path.GetTempPath();
            }
        }

        public async Task SendEmailAsync(string[] recipients, string subject, string body, string? from = null)
        {
            var fileName = Path.Combine(_directory, $"email_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid()}.eml.txt");
            var sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture, $"From: {from ?? "no-reply@example.com"}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"To: {string.Join(", ", recipients)}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"Subject: {subject}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"Date: {DateTime.UtcNow:O}");
            sb.AppendLine();
            sb.AppendLine(body ?? string.Empty);

            await File.WriteAllTextAsync(fileName, sb.ToString());
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Saved email to {Path}", fileName);
            }
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            return SendEmailAsync(new[] { email }, subject, htmlMessage, null);
        }
    }
}
