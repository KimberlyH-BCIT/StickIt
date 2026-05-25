using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ELKH.Data;
using ELKH.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELKH.Services
{
    /// <summary>
    /// Service for managing back-in-stock notification requests.
    /// Handles creation, retrieval, and cancellation of stock watching.
    /// </summary>
    public class StockNotificationService : IStockNotificationService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<StockNotificationService> _logger;

        public StockNotificationService(ApplicationDbContext db, ILogger<StockNotificationService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<bool> RequestNotificationAsync(int userId, int productId, CancellationToken ct = default)
        {
            try
            {
                // Check if notification request already exists (including cancelled ones)
                var existing = await _db.StockNotifications
                    .FirstOrDefaultAsync(sn => sn.FkRegisteredUserId == userId
                                            && sn.FkProductId == productId
                                            && !sn.IsCancelled
                                            && !sn.NotificationSent, ct);

                if (existing != null)
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("User {UserId} already has pending notification for product {ProductId}", userId, productId);
                    }
                    return false; // Already exists
                }

                // Create new notification request
                var notification = new StockNotificationModel
                {
                    FkRegisteredUserId = userId,
                    FkProductId = productId,
                    CreatedAt = DateTime.UtcNow,
                    NotificationSent = false,
                    IsCancelled = false
                };

                _db.StockNotifications.Add(notification);
                await _db.SaveChangesAsync(ct);

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Created stock notification request for user {UserId}, product {ProductId}", userId, productId);
                }
                return true;
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, "Error creating stock notification for user {UserId}, product {ProductId}", userId, productId);
                }
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> HasPendingNotificationAsync(int userId, int productId, CancellationToken ct = default)
        {
            return await _db.StockNotifications
                .AnyAsync(sn => sn.FkRegisteredUserId == userId
                             && sn.FkProductId == productId
                             && !sn.IsCancelled
                             && !sn.NotificationSent, ct);
        }

        /// <inheritdoc/>
        public async Task<List<StockNotificationModel>> GetPendingNotificationsAsync(int productId, CancellationToken ct = default)
        {
            return await _db.StockNotifications
                .Include(sn => sn.RegisteredUser)
                .Include(sn => sn.Product)
                .Where(sn => sn.FkProductId == productId
                          && !sn.NotificationSent
                          && !sn.IsCancelled)
                .ToListAsync(ct);
        }

        /// <inheritdoc/>
        public async Task MarkAsSentAsync(int notificationId, CancellationToken ct = default)
        {
            var notification = await _db.StockNotifications.FindAsync(new object[] { notificationId }, ct);
            if (notification != null)
            {
                notification.NotificationSent = true;
                notification.SentAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Marked stock notification {NotificationId} as sent", notificationId);
                }
            }
        }

        /// <inheritdoc/>
        public async Task<bool> CancelNotificationAsync(int userId, int productId, CancellationToken ct = default)
        {
            var notification = await _db.StockNotifications
                .FirstOrDefaultAsync(sn => sn.FkRegisteredUserId == userId
                                        && sn.FkProductId == productId
                                        && !sn.IsCancelled
                                        && !sn.NotificationSent, ct);

            if (notification == null)
                return false;

            notification.IsCancelled = true;
            await _db.SaveChangesAsync(ct);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Cancelled stock notification for user {UserId}, product {ProductId}", userId, productId);
            }
            return true;
        }

        /// <inheritdoc/>
        public async Task<List<StockNotificationModel>> GetUserNotificationsAsync(int userId, CancellationToken ct = default)
        {
            return await _db.StockNotifications
                .Include(sn => sn.Product)
                .Where(sn => sn.FkRegisteredUserId == userId
                          && !sn.IsCancelled
                          && !sn.NotificationSent)
                .OrderByDescending(sn => sn.CreatedAt)
                .ToListAsync(ct);
        }
    }
}
