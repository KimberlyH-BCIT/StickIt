using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    public class OrderStatusModel
    {
        [Key]
        public int OrderStatusId { get; set; }

        public string StatusName { get; set; } = string.Empty;

        // Order Relationship
        public int FkOrderId { get; set; }
        public OrderModel? Order { get; set; }   
    }
}
