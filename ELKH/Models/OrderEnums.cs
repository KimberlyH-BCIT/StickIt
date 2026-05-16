namespace ELKH.Models
{
    public enum OrderStatus
    {
        Pending = 0,   // Order placed, waiting for action
        Paid = 1,      // Payment confirmed (PayPal captured)
        Shipped = 2,   // Order is on the way / finished
        Cancelled = 3  // Order was aborted
    }

    public enum DeliveryStatus
    {
        Pending = 0,
        InTransit = 1,
        Shipped = 2,
        Delivered = 3,
        Cancelled = 4
    }
}
