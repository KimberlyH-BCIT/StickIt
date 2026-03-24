using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ELKH.Models;

namespace ELKH.Services
{
    /// <summary>
    /// Contract for managing back-in-stock notifications for out-of-stock products.
    /// Allows customers to request notifications when products become available.
    /// </summary>
    public interface IStockNotificationService
    {
        /// <summary>
        /// Creates a new stock notification request for a user and product.
        /// </summary>
        /// <param name="userId">The registered user ID requesting notification.</param>
        /// <param name="productId">The product ID to watch.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the notification was created, false if it already exists.</returns>
        Task<bool> RequestNotificationAsync(int userId, int productId, CancellationToken ct = default);

        /// <summary>
        /// Checks if a user has already requested notification for a specific product.
        /// </summary>
        /// <param name="userId">The registered user ID.</param>
        /// <param name="productId">The product ID.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if a notification request exists and is not cancelled.</returns>
        Task<bool> HasPendingNotificationAsync(int userId, int productId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all pending notification requests for a product that is now back in stock.
        /// </summary>
        /// <param name="productId">The product ID that is back in stock.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of notification requests to process.</returns>
        Task<List<StockNotificationModel>> GetPendingNotificationsAsync(int productId, CancellationToken ct = default);

        /// <summary>
        /// Marks a notification as sent after email dispatch.
        /// </summary>
        /// <param name="notificationId">The notification ID.</param>
        /// <param name="ct">Cancellation token.</param>
        Task MarkAsSentAsync(int notificationId, CancellationToken ct = default);

        /// <summary>
        /// Cancels a notification request (soft delete).
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="productId">The product ID.</param>
        /// <param name="ct">Cancellation token.</param>
        Task<bool> CancelNotificationAsync(int userId, int productId, CancellationToken ct = default);

        /// <summary>
        /// Gets all active (pending) notification requests for a user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of products the user is waiting for.</returns>
        Task<List<StockNotificationModel>> GetUserNotificationsAsync(int userId, CancellationToken ct = default);
    }
}
