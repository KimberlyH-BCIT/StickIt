using ELKH.ViewModels;

public class OrderDetailsVM
{
    public int OrderId { get; set; }
    public string UserEmail { get; set; } = string.Empty;

        /// <summary>Current delivery status of the order.</summary>
    public string DeliveryStatus { get; set; } = string.Empty;
    
    // Add these fields to fix the CS1061 errors
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal TotalOrderAmount { get; set; }

        /// <summary>Name of the product in the order.</summary>
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int? TransactionId { get; set; }

    public List<OrderItemVM> OrderItems { get; set; } = new List<OrderItemVM>();
}