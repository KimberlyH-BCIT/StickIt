using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a single line item in an order, linking a product and quantity to an order.
    /// </summary>
    public class OrderItemModel
    {
        /// <summary>
        /// Unique identifier for the order item (primary key).
        /// </summary>
        [Key]
        public int PkOrderItemId { get; set; }

        /// <summary>
        /// Quantity of the product ordered.
        /// </summary>
        public int Quantity { get; set; } = 1;

        /// <summary>
        /// Foreign key to the order this item belongs to.
        /// </summary>
        public int FkOrderId { get; set; }

        /// <summary>
        /// Navigation property to the order this item belongs to.
        /// </summary>
        public OrderModel Order { get; set; } = null!;

        /// <summary>
        /// Foreign key to the product in this order item.
        /// </summary>
        public int FkProductId { get; set; }

        /// <summary>
        /// Navigation property to the product in this order item.
        /// </summary>
        public ProductModel Product { get; set; } = null!;
    }
}
