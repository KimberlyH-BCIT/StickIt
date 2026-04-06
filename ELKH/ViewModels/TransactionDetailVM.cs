namespace ELKH.ViewModels
{
    /// <summary>
    /// View model representing the full detail of a single transaction,
    /// including payment info, customer identity, and purchased line items.
    /// </summary>
    public class TransactionDetailVM
    {
        public int TransactionId { get; set; }
        public int OrderId { get; set; }
        public string? TransactionStatus { get; set; }
        public decimal Amount { get; set; }
        public decimal DeliveryFee { get; set; }
        public DateTime TransactionDate { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }

        public List<TransactionItemVM> Items { get; set; } = new();
    }

    /// <summary>
    /// View model representing a single purchased item within a <see cref="TransactionDetailVM"/>.
    /// </summary>
    public class TransactionItemVM
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public int Quantity { get; set; }
        public string Thumbnail { get; set; } = "";
    }
}
