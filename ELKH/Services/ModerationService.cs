using System;
using System.Linq;
using System.Threading.Tasks;
using ELKH.Data;
using ELKH.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Mail;
using System.Net;

namespace ELKH.Services
{
    /// <summary>
    /// Implementation of <see cref="IModerationService"/> that flags product ratings,
    /// enforces per-moderator rate limiting via in-memory cache, and dispatches
    /// notification emails to configured admin recipients.
    /// </summary>
    public class ModerationService : IModerationService
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailSender _emailSender;
        private readonly ELKH.Configuration.ModerationOptions _options;
        private readonly ILogger<ModerationService> _logger;
        private readonly IMemoryCache _cache;
        private readonly ELKH.Configuration.CacheOptions _cacheOptions;

        /// <summary>
        /// Initializes a new instance of <see cref="ModerationService"/>.
        /// </summary>
        /// <param name="db">EF Core context for rating queries and updates.</param>
        /// <param name="emailSender">Email sender for admin flag notifications.</param>
        /// <param name="cacheOptions">Cache settings including the flag rate-limit window (seconds).</param>
        /// <param name="options">Moderation settings: admin email list and validated base URL.</param>
        /// <param name="logger">Logger for notification and operational diagnostics.</param>
        /// <param name="cache">In-memory cache used to track per-moderator flag timestamps.</param>
        public ModerationService(ApplicationDbContext db, IEmailSender emailSender, Microsoft.Extensions.Options.IOptions<ELKH.Configuration.CacheOptions> cacheOptions, Microsoft.Extensions.Options.IOptions<ELKH.Configuration.ModerationOptions> options, ILogger<ModerationService> logger, IMemoryCache cache)
        {
            _db = db;
            _emailSender = emailSender;
            _logger = logger;
            _cache = cache;
            _cacheOptions = cacheOptions.Value;
            _options = options.Value;
        }

        /// <summary>
        /// Flag a product rating for moderator review.
        ///
        /// The method enforces a per-moderator rate limit stored in-memory to mitigate abuse.
        /// If the rating is found, it is marked as flagged and the optional moderator note is saved.
        /// A notification email to configured admins is assembled and sent; failures here are logged
        /// but do not fail the overall operation.
        /// </summary>
        public async Task<ModerationResult> FlagAsync(int ratingId, string note, string moderator)
        {
            // rate-limit flag operations per moderator to prevent abuse
            var cacheKey = $"flag_rate_{moderator}";
            var windowSeconds = _cacheOptions.FlagRateLimitSeconds > 0 ? _cacheOptions.FlagRateLimitSeconds : 5;

            // If the moderator has performed a flag recently and is within the window, reject.
            if (_cache.TryGetValue(cacheKey, out DateTime last) && (DateTime.UtcNow - last).TotalSeconds < windowSeconds)
            {
                return new ModerationResult { Success = false, Message = "Rate limit: please wait before flagging again" };
            }

            // Record the latest flag timestamp for the moderator with sliding expiration equal to the window.
            _cache.Set(cacheKey, DateTime.UtcNow, TimeSpan.FromSeconds(windowSeconds));

            var r = await _db.ProductRatings.FindAsync(ratingId);
            if (r is null) return new ModerationResult { Success = false, Message = "NotFound" };

            // Update fields on the rating to reflect the flag action.
            r.IsFlagged = true;
            r.ModeratorNote = note ?? string.Empty;
            await _db.SaveChangesAsync();

            try
            {
                // Prepare a helpful notification including direct links into the moderation console.
                var product = await _db.Product.FindAsync(r.FkProductId);
                var productName = product?.Name ?? r.FkProductId.ToString();
                var subject = $"Review flagged for product '{productName}' (Id: {r.PkRatingId})";

                // Security: Get validated base URL from configuration
                // GetSafeBaseUrl ensures the URL is from trusted config, not user input
                var baseUrl = ELKH.Constants.ModerationRoutes.GetSafeBaseUrl(_options.BaseUrl);

                // Compose absolute links using validated base URL
                var moderationUrl = ELKH.Constants.ModerationRoutes.WithBase(baseUrl, ELKH.Constants.ModerationRoutes.ConsolePath);
                var approveLink = ELKH.Constants.ModerationRoutes.WithBase(baseUrl, ELKH.Constants.ModerationRoutes.ApprovePath(r.PkRatingId));
                var flagLink = ELKH.Constants.ModerationRoutes.WithBase(baseUrl, ELKH.Constants.ModerationRoutes.FlagPath(r.PkRatingId));

                var body = $@"
A review was flagged

Product: {productName}
ProductId: {r.FkProductId}
UserId: {r.FkRegisteredUserId}
Rating: {r.Rating}
Comment: {r.Description}
Moderator Note: {r.ModeratorNote}

Moderation Console: {moderationUrl}
Direct Approve Link: {approveLink}
Direct Flag Link: {flagLink}
";

                var admins = _options.AdminEmails ?? Array.Empty<string>();
                if (admins.Length > 0)
                {
                    await _emailSender.SendEmailAsync(admins, subject, body);
                }
            }
            catch (Exception ex)
            {
                // Do not fail the flag operation due to notification errors; log for diagnostics.
                _logger.LogError(ex, "Failed to send flag notification");
            }

            return new ModerationResult { Success = true, Message = "Review flagged" };
        }

        
    }
}
