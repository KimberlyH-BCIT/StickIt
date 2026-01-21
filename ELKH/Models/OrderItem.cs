using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    public class OrderItem
    {
        [Key]
        public int PkOrderItemId { get; set; }

        public int Quantity { get; set; } = 1;

        //Relationship with Order
        public int FkOrderId { get; set; }
        public Order Orders { get; set; } = new Order();

        //Relationship with Product
        public int FkProductId { get; set; }
        public Product Products { get; set; } = new Product();
    }
}
