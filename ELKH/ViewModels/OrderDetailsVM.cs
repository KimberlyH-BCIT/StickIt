
namespace ELKH.ViewModels
{
    public class OrderDetailsVM
    {
        public int OrderId { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string DeliveryStatus { get; set; } = string.Empty;
        public int TransactionId { get; set; }
        public List<OrderItemVM> OrderItems { get; set; } = new List<OrderItemVM>();
    }
}
