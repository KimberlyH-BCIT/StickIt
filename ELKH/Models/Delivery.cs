using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    public class Delivery
    {
        [Key]
        public int DeliveryId { get; set; }
        public string DeliveryStatus { get; set; } = string.Empty;

        //Order Relationshiop
        public int FkOrderId { get; set; }
        public Order Order { get; set; } = new Order();

        //Address Relationship
        public int FkAddressId { get; set; }
        public Address Address { get; set; } = new Address();

    }
}
