using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    public class Order
    {
        [Key]
        public int PkOrderId { get; set; }
        [Display(Name = "Order Status")]
        public string OrderStatus { get; set; } = string.Empty;

        [Display(Name = "Total Amount")]
        public decimal TotalAmount { get; set; } = 0;

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        //Relationship with User
        public int FkRegisteredUserId { get; set; }
        public RegisteredUser RegisteredUser { get; set; } = new RegisteredUser();

        //Relationship with OrderItem
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        //Relationship with Transaction
        public int FkTransactionId { get; set; }
        public Transaction Transaction { get; set; } = new Transaction();

        //Delivery Relationship
        public Delivery Delivery { get; set; } = new Delivery();
    }
}
