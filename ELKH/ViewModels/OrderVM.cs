namespace ELKH.ViewModels;

/// <summary>
/// View model for order information containing complete order details,
/// customer information, order items, and transaction data for order management.
/// </summary>
public class OrderVM
{
    public OrderModel Order { get; set; } = null!;

    // UPDATED: Comparing against the Enum values instead of strings
    public bool CanCancel => Order.OrderStatus == OrderStatus.Pending;
}

public class OrderHistoryVM
{
    public IEnumerable<OrderModel> Orders { get; set; } = [];
    public string CurrentSort { get; set; } = "date_desc";
}
