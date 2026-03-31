
namespace ELKH.ViewModels
{
    /// <summary>
    /// View model for displaying order details in admin and user views.
    /// </summary>
    public class OrderDetailsVM
    {
        /// <summary>Order unique identifier.</summary>
        public int OrderId { get; set; }

        /// <summary>Email of the user who placed the order.</summary>
        public string UserEmail { get; set; } = string.Empty;

        /// <summary>Current delivery status of the order.</summary>
        public string DeliveryStatus { get; set; } = string.Empty;

        /// <summary>Primary key of the associated payment transaction.</summary>
        public int TransactionId { get; set; }

        /// <summary>Name of the product in the order.</summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>Quantity of the product ordered.</summary>
        public int Quantity { get; set; } = 1;

        /// <summary>Unit price of the product at the time of order.</summary>
        public decimal UnitPrice { get; set; } = 0;

        /// <summary>Line-item breakdown of all products in the order.</summary>
        public List<OrderItemVM> OrderItems { get; set; } = new List<OrderItemVM>();

        public decimal TotalPrice => OrderItems?.Sum(product => product.Quantity * product.ProductPrice) ?? 0;
    }
}
