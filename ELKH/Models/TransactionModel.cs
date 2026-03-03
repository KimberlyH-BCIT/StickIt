using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    public class TransactionModel
    {
        [Key]
        public int PkTransactionId { get; set; }
        [Display(Name = "Transaction Status")]
        public string TransactionStatus { get; set; } = string.Empty;
        [DisplayFormat(DataFormatString = "{0:F2}")]
        public decimal Amount { get; set; } = 0;
        [Display(Name ="Transaction Time")]
        public DateTime TransactionDate { get; set; } = DateTime.Now;
        [Display(Name = "Delivery Fee")]
        public decimal DeliberyFee { get; set; } = 0;

        //Relationship with Order
        public int FkOrderId { get; set; }
        public OrderModel Order { get; set; } = new OrderModel();

        //Relationship with ContactDetail
        public int FkContactId { get; set; }
        public ContactDetailModel ContactDetail { get; set; } = new ContactDetailModel();

    }
}
