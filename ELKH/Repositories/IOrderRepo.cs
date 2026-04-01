using ELKH.Models;

namespace ELKH.Repositories;

/// <summary>
/// Repository interface for order operations providing data access methods for
/// managing customer orders, order items, order history, and order lifecycle operations.
/// </summary>
public interface IOrderRepo
{
    Task<OrderModel?> GetByIdAsync(int id);
    Task<OrderModel?> GetByIdWithItemsAsync(int id);
    Task<IEnumerable<OrderModel>> GetByUserIdAsync(int registeredUserId);
    Task<OrderModel> CreateAsync(OrderModel order);
    Task<bool> UpdateStatusAsync(int orderId, OrderStatus status);
}
