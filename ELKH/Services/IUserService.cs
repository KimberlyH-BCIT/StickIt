using System.Threading;
using System.Threading.Tasks;
using ELKH.Models;
using ELKH.ViewModels;

namespace ELKH.Services
{
    /// <summary>
    /// Service for user-related operations with caching support.
    /// Centralizes user lookup logic to avoid repetitive queries and enable
    /// performance optimizations like in-memory caching.
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Retrieves a registered user by email address.
        /// Results are cached for a short period to reduce database queries on hot paths.
        /// </summary>
        Task<RegisteredUserModel?> GetByEmailAsync(string email, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a registered user by primary key without caching.
        /// Use when a fresh database read is required (e.g., after profile updates).
        /// </summary>
        Task<RegisteredUserModel?> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Removes the cached entry for the given email address.
        /// Call this after updating user data to ensure subsequent reads reflect the changes.
        /// </summary>
        void InvalidateCache(string email);

        /// <summary>
        /// Returns the total number of items currently in the user's wishlist.
        /// Used to populate the wishlist badge in the navigation header.
        /// </summary>
        Task<int> GetWishlistCountAsync(int userId, CancellationToken ct = default);

        /// <summary>
        /// Returns a single page of the user's wishlist items with optional sorting.
        /// </summary>
        /// <param name="userId">Primary key of the user whose wishlist to retrieve.</param>
        /// <param name="page">1-based page index.</param>
        /// <param name="sort">Sort key: <c>"date_asc"</c>, <c>"on_sale"</c>, <c>"most_popular"</c>, or default (date descending).</param>
        Task<WishlistSectionVM> GetWishlistSectionAsync(int userId, int page, string sort, CancellationToken ct = default);

        /// <summary>
        /// Returns a single page of the user's orders, filtered by active or historical status.
        /// </summary>
        /// <param name="userId">Primary key of the user whose orders to retrieve.</param>
        /// <param name="page">1-based page index.</param>
        /// <param name="sort">Sort key: <c>"date_asc"</c>, <c>"on_sale"</c>, <c>"most_popular"</c>, or default (date descending).</param>
        /// <param name="activeOnly"><see langword="true"/> to return in-progress orders; <see langword="false"/> for order history.</param>
        Task<OrderSectionVM> GetOrderSectionAsync(int userId, int page, string sort, bool activeOnly, CancellationToken ct = default);

        /// <summary>
        /// Aggregates all dashboard data (wishlist count, wishlist page, active orders, order history)
        /// for the user's account overview page in a single logical call.
        /// </summary>
        Task<DashboardData> GetDashboardDataAsync(int userId, CancellationToken ct = default);
    }
}
