using ELKH.Models;

namespace ELKH.ViewModels;

public class OrderVM
{
    public OrderModel Order { get; set; } = null!;

    // UPDATED: Comparing against the Enum values instead of strings
    public bool CanCancel => Order.OrderStatus == OrderStatus.Pending;
}

public class OrderHistoryVM
{
    public IEnumerable<OrderModel> Orders { get; set; } = new List<OrderModel>();
}