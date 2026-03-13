using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a financial transaction associated with an order.
    /// Tracks payment status, amount, delivery fees, and links to order and shipping details.
    /// </summary>
    public class TransactionModel
    {
        /// <summary>Primary key for this transaction.</summary>
        [Key]
        public int PkTransactionId { get; set; }

        /// <summary>Current payment status (e.g. <c>Pending</c>, <c>Completed</c>, <c>Refunded</c>).</summary>
        [Display(Name = "Transaction Status")]
        public string TransactionStatus { get; set; } = string.Empty;

        /// <summary>Total amount charged in this transaction, in the store's configured currency.</summary>
        [DisplayFormat(DataFormatString = "{0:F2}")]
        public decimal Amount { get; set; } = 0;

        /// <summary>UTC timestamp when the transaction was recorded.</summary>
        [Display(Name = "Transaction Time")]
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

        /// <summary>Shipping/delivery fee included in the transaction amount.</summary>
        [Display(Name = "Delivery Fee")]
        public decimal DeliveryFee { get; set; } = 0;

        /// <summary>Foreign key to the order this transaction settles.</summary>
        public int FkOrderId { get; set; }
        public OrderModel? Order { get; set; }

        /// <summary>Foreign key to the shipping address used for this transaction.</summary>
        public int FkContactId { get; set; }
        public ContactDetailModel? ContactDetail { get; set; }

    }
}