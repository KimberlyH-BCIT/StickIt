using ELKH.Models;

namespace ELKH.Repositories
{
    /// <summary>
    /// Abstraction over order-history data access used by admin/staff controllers.
    /// </summary>
    public interface IOrderHistoryManagementRepo
    {
        Task<IEnumerable<OrderModel>> GetAllOrders();
        Task<OrderModel?> OrderDetails(string email, int orderId);
        Task<OrderModel?> GetByIdAsync(int orderId);

        // FIXED: Changed 'string deliveryStatus' to 'DeliveryStatus deliveryStatus'
        Task<OrderModel?> UpdateDeliveryStatusAsync(int orderId, DeliveryStatus deliveryStatus);
    }
}
