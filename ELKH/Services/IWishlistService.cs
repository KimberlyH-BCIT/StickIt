using ELKH.Models;

namespace ELKH.Services
{
    /// <summary>
    /// Result of a wishlist add or remove operation.
    /// Carries enough data for both JSON (AJAX) and redirect (non-AJAX) responses.
    /// </summary>
    public class WishlistResult
    {
        public bool Success { get; init; }
        public bool AlreadyExists { get; init; }
        public string Message { get; init; } = string.Empty;
        /// <summary>Authoritative item count after the operation.</summary>
        public int Count { get; init; }
    }

    /// <summary>
    /// Service for wishlist mutations and retrieval.
    /// Keeps all EF access out of <c>WishlistController</c>.
    /// </summary>
    public interface IWishlistService
    {
        /// <summary>
        /// Adds a product to the user's wishlist, creating the wishlist record if one does not exist.
        /// Returns <see cref="WishlistResult.AlreadyExists"/> = <see langword="true"/> if the product
        /// is already present, along with the current authoritative item count.
        /// </summary>
        Task<WishlistResult> AddAsync(string userEmail, int productId);

        /// <summary>
        /// Removes a product from the user's wishlist.
        /// Returns <c>Success = false</c> with a message if the user, wishlist, or item is not found.
        /// </summary>
        Task<WishlistResult> RemoveAsync(string userEmail, int productId);

        /// <summary>
        /// Returns all wishlist items for the user with their product details eager-loaded.
        /// </summary>
        /// <param name="userEmail">Email of the authenticated user.</param>
        /// <param name="sort">Sort key: <c>"date_asc"</c> or default (date descending).</param>
        Task<IEnumerable<WishListItemModel>> GetItemsAsync(string userEmail, string sort);
    }
}
