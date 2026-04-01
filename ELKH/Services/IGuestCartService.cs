using ELKH.ViewModels;

namespace ELKH.Services
{
    /// <summary>
    /// Interface for guest cart service operations.
    /// Provides session-based shopping cart functionality for anonymous users.
    /// </summary>
    public interface IGuestCartService
    {
        /// <summary>
        /// Adds a product to the guest's session cart with specified quantity.
        /// </summary>
        /// <param name="productId">ID of the product to add</param>
        /// <param name="quantity">Quantity to add (must be positive)</param>
        /// <returns>Task representing the async operation</returns>
        Task AddToCartAsync(int productId, int quantity);

        /// <summary>
        /// Updates the quantity of a cart item. If quantity is 0, removes the item.
        /// </summary>
        /// <param name="productId">ID of the product to update</param>
        /// <param name="newQuantity">New quantity (0 removes the item)</param>
        /// <returns>Task representing the async operation</returns>
        Task UpdateQuantityAsync(int productId, int newQuantity);

        /// <summary>
        /// Removes a product from the guest's cart.
        /// </summary>
        /// <param name="productId">ID of the product to remove</param>
        /// <returns>Task representing the async operation</returns>
        Task RemoveFromCartAsync(int productId);

        /// <summary>
        /// Clears all items from the guest's cart.
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        Task ClearCartAsync();

        /// <summary>
        /// Gets the guest's cart items with full product details.
        /// </summary>
        /// <returns>List of cart items with product details and pricing</returns>
        Task<List<CartItemVM>> GetCartItemsAsync();

        /// <summary>
        /// Gets the count of items in the guest's cart for UI display.
        /// </summary>
        /// <returns>Total quantity of items in cart</returns>
        Task<int> GetCartCountAsync();

        /// <summary>
        /// Migrates guest cart to authenticated user's cart when they log in.
        /// </summary>
        /// <param name="userEmail">Email of the authenticated user</param>
        /// <param name="cartService">User cart service to merge items into</param>
        /// <returns>Task representing the async migration operation</returns>
        Task MigrateToUserCartAsync(string userEmail, ICartService cartService);
    }
}
