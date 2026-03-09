using ELKH.Models;
using ELKH.ViewModels;

namespace ELKH.Repositories
{
    /// <summary>
    /// Contract for order data access used by admin and user-facing order views.
    /// Provides both lightweight VM projections (for listing pages) and full-entity
    /// queries (for detail and history views).
    /// </summary>
    public interface IOrderManagementRepo
    {
        // ── Admin projections: return flat DTOs to avoid over-fetching in listing views ──

        /// <summary>Returns all orders projected to summary <see cref="OrderDetailsVM"/> rows.</summary>
        Task<IEnumerable<OrderDetailsVM>> GetAllOrdersAsync();

        /// <summary>Returns all orders for the given user email as summary VMs.</summary>
        Task<IEnumerable<OrderDetailsVM>> OrderDetailsAsync(string email);

        // ── Full-entity queries: eager-load navigation properties for detail views ──

        /// <summary>Returns all order entities ordered by creation date descending.</summary>
        Task<IEnumerable<OrderModel>> GetAllOrderModelsAsync();

        /// <summary>Returns all order entities for a specific user, newest first.</summary>
        Task<IEnumerable<OrderModel>> GetUserOrdersAsync(string userEmail);

        /// <summary>
        /// Returns a single order with all navigation properties needed for the detail view
        /// (line items with products and images, contact detail, transaction),
        /// or <see langword="null"/> when no matching order exists.
        /// </summary>
        Task<OrderModel?> GetOrderWithDetailsAsync(int orderId);
    }
}
