using ELKH.Models;

namespace ELKH.ViewModels;

public class OrderVM
{
    public OrderModel Order { get; set; } = null!;
    public bool CanCancel => Order.OrderStatus == "Pending" || Order.OrderStatus == "Paid";
}

public class OrderHistoryVM
{
    public IEnumerable<OrderModel> Orders { get; set; } = new List<OrderModel>();
}