namespace ELKH.ViewModels
{
    public class OrderItemVM
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal ProductPrice { get; set; }
    }
}
