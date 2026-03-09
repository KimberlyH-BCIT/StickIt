using ELKH.Models;

namespace ELKH.Repositories;

public interface IOrderRepo
{
    Task<OrderModel?> GetByIdAsync(int id);
    Task<OrderModel?> GetByIdWithItemsAsync(int id);
    Task<IEnumerable<OrderModel>> GetByUserIdAsync(int registeredUserId);
    Task<OrderModel> CreateAsync(OrderModel order);
    Task<bool> UpdateStatusAsync(int orderId, string status);
}