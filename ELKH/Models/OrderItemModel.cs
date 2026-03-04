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
        public OrderModel? Order { get; set; }

        //Relationship with Product
        public int FkProductId { get; set; }
        public ProductModel? Product { get; set; }
    }
}
