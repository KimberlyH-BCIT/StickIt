using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    /// <summary>
    /// Represents the status of an order, such as "Pending", "Shipped", or "Delivered".
    /// </summary>
    public class OrderStatusModel
    {
        /// <summary>
        /// Unique identifier for the order status (primary key).
        /// </summary>
        [Key]
        public int OrderStatusId { get; set; }

        /// <summary>
        /// Name of the status (e.g., "Pending", "Shipped").
        /// </summary>
        public string StatusName { get; set; } = string.Empty;

        /// <summary>
        /// Foreign key to the order this status is associated with.
        /// </summary>
        public int FkOrderId { get; set; }
        public OrderModel? Order { get; set; }   
    }
}
