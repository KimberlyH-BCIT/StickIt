using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a customer order with line items, delivery tracking, and payment information.
    /// Orders are created when customers complete checkout and track the entire fulfillment lifecycle.
    /// </summary>
    public class OrderModel
    {
        /// <summary>
        /// Unique identifier for the order (primary key).
        /// </summary>
        [Key]
        public int PkOrderId { get; set; }

        /// <summary>
        /// Current status of the order (e.g., "Pending", "Processing", "Shipped", "Delivered", "Cancelled").
        /// Updated throughout the order lifecycle.
        /// </summary>
        [Display(Name = "Order Status")]
        public string OrderStatus { get; set; } = string.Empty;

        /// <summary>
        /// Total order amount including all line items and any applicable taxes/fees.
        /// Calculated from order items at time of order placement.
        /// </summary>
        [Display(Name = "Total Amount")]
        public decimal TotalAmount { get; set; } = 0;

        /// <summary>
        /// Timestamp when the order was created (UTC).
        /// Used for order history, sorting, and analytics.
        /// </summary>
        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Delivery/shipping status tracking (e.g., "Pending", "Shipped", "In Transit", "Delivered").
        /// Separate from order status to track fulfillment independently.
        /// </summary>
        [Display(Name ="Delivery Status")]
        public string DeliveryStatus { get; set; } = string.Empty;

        // =====================================================================
        // Relationships
        // =====================================================================

        /// <summary>
        /// Foreign key to the customer who placed the order.
        /// </summary>
        public int FkRegisteredUserId { get; set; }

        /// <summary>
        /// Navigation property to the customer who placed the order.
        /// Used for order history and customer analytics.
        /// </summary>
        public RegisteredUserModel RegisteredUser { get; set; } = null!;

        /// <summary>
        /// Collection of line items included in this order.
        /// Each item represents a product, quantity, and price at time of purchase.
        /// </summary>
        public ICollection<OrderItemModel> OrderItems { get; set; } = new List<OrderItemModel>();

        //Relationship with Transaction
        public TransactionModel Transaction { get; set; } = new TransactionModel();

        //Order Status Relationship
        public OrderStatusModel OrderStatuses { get; set; } = new OrderStatusModel();

        /// <summary>
        /// Foreign key to the shipping/delivery address for this order.
        /// </summary>
        public int FkContactId { get; set; }
        public ContactDetailModel ContactDetail { get; set; } = new ContactDetailModel();
        public OrderStatusModel OrderStatusDetail { get; set; } = new OrderStatusModel();
    }
}
