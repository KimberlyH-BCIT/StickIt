using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    public class OrderModel
    {
        [Key]
        public int PkOrderId { get; set; }
        [Display(Name = "Order Status")]
        public string OrderStatus { get; set; } = string.Empty;

        [Display(Name = "Total Amount")]
        public decimal TotalAmount { get; set; } = 0;

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name ="Delivery Status")]
        public string DeliveryStatus { get; set; } = string.Empty;

        //Relationship with User
        public int FkRegisteredUserId { get; set; }
        public RegisteredUserModel RegisteredUser { get; set; } = new RegisteredUserModel();

        //Relationship with OrderItem
        public ICollection<OrderItemModel> OrderItems { get; set; } = new List<OrderItemModel>();

        //Relationship with Transaction
        public TransactionModel Transaction { get; set; } = new TransactionModel();

        //Order Status Relationship
        public OrderStatusModel OrderStatuses { get; set; } = new OrderStatusModel();

        //ContactDetail Relationship
        public int FkContactId { get; set; }
        public ContactDetailModel ContactDetail { get; set; } = new ContactDetailModel();
    }
}
