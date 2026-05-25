using System;
using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a customer's request to be notified when an out-of-stock product becomes available.
    /// Customers can add products to their watchlist and receive email notifications when restocked.
    /// </summary>
    public class StockNotificationModel
    {
        /// <summary>
        /// Unique identifier for this notification request (primary key).
        /// </summary>
        [Key]
        public int PkStockNotificationId { get; set; }

        /// <summary>
        /// Foreign key to the product being watched.
        /// </summary>
        [Required]
        public int FkProductId { get; set; }

        /// <summary>
        /// Navigation property to the product being watched.
        /// </summary>
        public ProductModel? Product { get; set; }

        /// <summary>
        /// Foreign key to the registered user requesting notification.
        /// </summary>
        [Required]
        public int FkRegisteredUserId { get; set; }

        /// <summary>
        /// Navigation property to the user requesting notification.
        /// </summary>
        public RegisteredUserModel? RegisteredUser { get; set; }

        /// <summary>
        /// Date and time when the notification request was created.
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the notification has been sent to the user.
        /// Set to true after email is dispatched.
        /// </summary>
        public bool NotificationSent { get; set; }

        /// <summary>
        /// Date and time when the notification email was sent.
        /// Null if not yet sent.
        /// </summary>
        public DateTime? SentAt { get; set; }

        /// <summary>
        /// Whether the user has cancelled this notification request.
        /// Allows soft deletion without removing historical data.
        /// </summary>
        public bool IsCancelled { get; set; }
    }
}
