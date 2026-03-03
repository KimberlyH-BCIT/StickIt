using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a financial transaction associated with an order.
    /// Tracks payment status, amount, delivery fees, and links to order and shipping details.
    /// </summary>
    public class TransactionModel
    {
        [Key]
        public int PkTransactionId { get; set; }

        [Display(Name = "Transaction Status")]
        public string TransactionStatus { get; set; } = string.Empty;

        [DisplayFormat(DataFormatString = "{0:F2}")]
        public decimal Amount { get; set; } = 0;

        [Display(Name = "Transaction Time")]
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Delivery Fee")]
        public decimal DeliveryFee { get; set; } = 0;

        // Relationship with Order
        public int FkOrderId { get; set; }
        public OrderModel Order { get; set; } = null!;

        // Relationship with ContactDetail
        public int FkContactId { get; set; }
        public ContactDetailModel ContactDetail { get; set; } = null!;
    }
}
