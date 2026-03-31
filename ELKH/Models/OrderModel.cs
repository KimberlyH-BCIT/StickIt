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
        [MaxLength(50)]
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
        [MaxLength(50)]
        public string DeliveryStatus { get; set; } = string.Empty;

        /// <summary>
        /// Name of the selected shipping method (e.g., "Standard Shipping", "Express Delivery").
        /// Denormalized for order history display even if shipping method is later modified.
        /// </summary>
        [Display(Name = "Shipping Method")]
        [MaxLength(100)]
        public string? ShippingMethodName { get; set; }

        /// <summary>
        /// Shipping cost charged for this order.
        /// Separate from product subtotal to allow reporting and refund calculations.
        /// </summary>
        [Display(Name = "Shipping Cost")]
        public decimal ShippingCost { get; set; } = 0;

        /// <summary>
        /// Total discount amount applied from coupons.
        /// Calculated from all applied coupons and stored for easy order summary display.
        /// </summary>
        [Display(Name = "Coupon Discount")]
        public decimal CouponDiscount { get; set; } = 0;

        // =====================================================================
        // Relationships
        // =====================================================================

        /// <summary>
        /// Foreign key to the customer who placed the order.
        /// </summary>
        public int FkRegisteredUserId { get; set; }
        public RegisteredUserModel? RegisteredUser { get; set; }

        /// <summary>
        /// Foreign key to the selected shipping method.
        /// Nullable to support legacy orders placed before shipping options were available.
        /// </summary>
        public int? FkShippingMethodId { get; set; }
        public ShippingMethodModel? ShippingMethod { get; set; }

        /// <summary>
        /// Collection of line items included in this order.
        /// Each item represents a product, quantity, and price at time of purchase.
        /// </summary>
        public ICollection<OrderItemModel> OrderItems { get; set; } = new List<OrderItemModel>();

        /// <summary>
        /// Collection of coupons applied to this order (many-to-many via OrderCouponModel).
        /// </summary>
        public ICollection<OrderCouponModel> OrderCoupons { get; set; } = new List<OrderCouponModel>();

        //Relationship with Transaction
        public TransactionModel? Transaction { get; set; }

        /// <summary>
        /// Foreign key to the shipping/delivery address for this order.
        /// </summary>
        public int FkContactId { get; set; }
        public ContactDetailModel? ContactDetail { get; set; }
    }
}
