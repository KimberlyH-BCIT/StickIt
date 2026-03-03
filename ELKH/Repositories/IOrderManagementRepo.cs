using ELKH.Models;
using ELKH.ViewModels;

namespace ELKH.Repositories
{
    public interface IOrderManagementRepo
    {
        // Admin projections (light DTOs for listing views)
        Task<IEnumerable<OrderDetailsVM>> GetAllOrdersAsync();
        Task<IEnumerable<OrderDetailsVM>> OrderDetailsAsync(string email);

        // Full-entity queries used by user-facing and detail views
        Task<IEnumerable<OrderModel>> GetAllOrderModelsAsync();
        Task<IEnumerable<OrderModel>> GetUserOrdersAsync(string userEmail);
        Task<OrderModel?> GetOrderWithDetailsAsync(int orderId);
    }
}
