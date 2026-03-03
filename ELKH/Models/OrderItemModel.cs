using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    public class OrderItemModel
    {
        [Key]
        public int PkOrderItemId { get; set; }

        public int Quantity { get; set; } = 1;

        //Relationship with Order
        public int FkOrderId { get; set; }
        public OrderModel Orders { get; set; } = new OrderModel();

        //Relationship with Product
        public int FkProductId { get; set; }
        public ProductModel Products { get; set; } = new ProductModel();
    }
}
