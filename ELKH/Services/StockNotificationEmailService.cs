using System;
using System.Linq;
using System.Threading.Tasks;
using ELKH.Data;
using ELKH.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ELKH.Services
{
    /// <summary>
    /// Service responsible for sending back-in-stock notification emails to waiting customers.
    /// Called when inventory is updated and products become available again.
    /// </summary>
    /// <remarks>
/// <para><strong>Table of Contents:</strong></para>
/// <list type="number">
/// <item>Section 1: Service Setup &amp; Dependencies</item>
/// <item>Section 2: Notification Processing Logic</item>
/// <item>Section 3: Email Generation &amp; Delivery</item>
/// <item>Section 4: Notification State Management</item>
/// <item>Section 5: Error Handling &amp; Resilience</item>
/// </list>
/// 
    /// <para><strong>Key Features:</strong></para>
    /// <list type="bullet">
    /// <item>Automated customer notifications when products are restocked</item>
    /// <item>Configurable cooldown periods to prevent notification spam</item>
    /// <item>Professional HTML email templates with product details</item>
    /// <item>Comprehensive error handling and audit logging</item>
    /// <item>Individual notification failure isolation</item>
    /// </list>
    /// 
    /// <para><strong>Usage Pattern:</strong></para>
    /// This service is typically called from inventory management operations when
    /// products transition from out-of-stock to available status. The service
    /// operates asynchronously to prevent blocking inventory updates.
    /// 
    /// <para><strong>Cooldown Management:</strong></para>
    /// Default 24-hour cooldown prevents customers from receiving duplicate
    /// notifications for the same product. This can be configured per call
    /// to accommodate different business requirements.
    /// </remarks>
    public class StockNotificationEmailService
    {
        #region Section 1: Service Setup & Dependencies

        // ===================================================================
        // Section 1: Service Setup & Dependencies
        // ===================================================================

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<StockNotificationEmailService> _logger;

        public StockNotificationEmailService(
            IServiceScopeFactory scopeFactory,
            ILogger<StockNotificationEmailService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        #endregion

        #region Section 2: Notification Processing Logic

        // ===================================================================
        // Section 2: Notification Processing Logic
        // ===================================================================

        /// <summary>
        /// Processes all pending notifications for a product that is now back in stock.
        /// Sends emails to all waiting customers and marks notifications as sent.
        /// </summary>
        /// <param name="productId">The product ID that is now back in stock.</param>
        /// <param name="cooldownHours">Minimum hours to wait before sending another batch of notifications for this product. Default is 24 hours.</param>
        public async Task ProcessRestockNotificationsAsync(int productId, int cooldownHours = 24)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            try
            {
                // Check if notifications were recently sent for this product
                var product = await db.Products.FindAsync(productId);
                if (product == null)
                {
                    _logger.LogWarning("Product {ProductId} not found", productId);
                    return;
                }

                // If notifications were sent within the cooldown period, skip
                if (product.LastNotificationSent.HasValue)
                {
                    var hoursSinceLastNotification = (DateTime.UtcNow - product.LastNotificationSent.Value).TotalHours;
                    if (hoursSinceLastNotification < cooldownHours)
                    {
                        _logger.LogInformation(
                            "Skipping notifications for product {ProductId} - last sent {Hours:F1} hours ago (cooldown: {Cooldown} hours)",
                            productId, hoursSinceLastNotification, cooldownHours);
                        return;
                    }
                }

                // Get all pending notifications for this product
                var notifications = await db.StockNotifications
                    .Include(sn => sn.RegisteredUser)
                    .Include(sn => sn.Product)
                    .Where(sn => sn.FkProductId == productId
                              && !sn.NotificationSent
                              && !sn.IsCancelled)
                    .ToListAsync();

                if (!notifications.Any())
                {
                    _logger.LogInformation("No pending notifications for product {ProductId}", productId);
                    return;
                }

                _logger.LogInformation("Processing {Count} stock notifications for product {ProductId}",
                    notifications.Count, productId);

                foreach (var notification in notifications)
                {
                    #region Section 3: Email Generation & Delivery

                    // ===================================================================
                    // Section 3: Email Generation & Delivery
                    // ===================================================================

                    try
                    {
                        // Send email notification
                        var notificationProduct = notification.Product;
                        var user = notification.RegisteredUser;

                        if (user?.Email == null || notificationProduct == null)
                        {
                            _logger.LogWarning("Skipping notification {NotificationId} - missing user or product",
                                notification.PkStockNotificationId);
                            continue;
                        }

                        var subject = $"🎉 {notificationProduct.Name} is Back in Stock!";
                        var productUrl = $"/Product/Details/{notificationProduct.PkProductId}";

                        var htmlBody = $@"
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; }}
        .product-box {{ background: white; padding: 20px; border-radius: 8px; margin: 20px 0; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
        .cta-button {{ display: inline-block; background: #667eea; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🎉 Great News!</h1>
            <p>The product you've been waiting for is back in stock!</p>
        </div>
        <div class='content'>
            <div class='product-box'>
                <h2>{notificationProduct.Name}</h2>
                <p>{notificationProduct.Description}</p>
                <p style='font-size: 24px; color: #667eea; font-weight: bold;'>
                    ${notificationProduct.Price:F2}
                    {(notificationProduct.DiscountPercent > 0 ? $"<span style='font-size: 14px; color: #e74c3c;'>({notificationProduct.DiscountPercent}% OFF!)</span>" : "")}
                </p>
                <p style='color: #27ae60; font-weight: bold;'>✓ In Stock Now - Limited Quantity!</p>
            </div>
            <p>Don't miss out! This popular item is back and ready to ship.</p>
            <center>
                <a href='{productUrl}' class='cta-button'>Shop Now</a>
            </center>
            <p style='margin-top: 30px; font-size: 12px; color: #666;'>
                You received this email because you requested to be notified when this product became available.
                <br>If you're no longer interested, you can safely ignore this email.
            </p>
        </div>
        <div class='footer'>
            <p>© {DateTime.UtcNow.Year} StickIt - Your Sticker Store</p>
        </div>
    </div>
</body>
</html>";

                        await emailSender.SendEmailAsync(new[] { user.Email }, subject, htmlBody);

                        #endregion

                        #region Section 4: Notification State Management

                        // ===================================================================
                        // Section 4: Notification State Management
                        // ===================================================================

                        // Mark notification as sent
                        notification.NotificationSent = true;
                        notification.SentAt = DateTime.UtcNow;

                        _logger.LogInformation("Sent stock notification to {Email} for product {ProductName}",
                            user.Email, notificationProduct.Name);

                        #endregion
                    }
                    #region Section 5: Error Handling & Resilience

                    // ===================================================================
                    // Section 5: Error Handling & Resilience
                    // ===================================================================

                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send notification {NotificationId}",
                            notification.PkStockNotificationId);
                    }

                    #endregion
                }

                // Update product's last notification timestamp to prevent duplicates
                product.LastNotificationSent = DateTime.UtcNow;

                await db.SaveChangesAsync();
                _logger.LogInformation("Completed processing stock notifications for product {ProductId}", productId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing stock notifications for product {ProductId}", productId);
                throw;
            }
        }

        #endregion
    }
}
