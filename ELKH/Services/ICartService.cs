using System.Collections.Generic;
using System.Threading.Tasks;
using ELKH.Models;

namespace ELKH.Services
{
    /// <summary>
    /// Contract for shopping cart and order operations.
    /// Manages cart items, quick purchases, and order placement with atomic transactions.
    /// </summary>
    public interface ICartService
    {
        Task UpdateQuantityAsync(string userEmail, int cartId, int quantity);
        Task ClearCartAsync(string email);
        /// <summary>
        /// Adds a product to the user's shopping cart with the specified quantity.
        /// </summary>
        /// <param name="userEmail">Email of the authenticated user</param>
        /// <param name="itemId">Product ID to add</param>
        /// <param name="quantity">Quantity to add (must be positive)</param>
        /// <returns>Task representing the async operation</returns>
        /// <remarks>
        /// If product already exists in cart, quantity is incremented.
        /// Total price is calculated using effective price (after discounts).
        /// </remarks>
        Task AddToCartAsync(string userEmail, int itemId, int quantity);

        /// <summary>
        /// Places an immediate order for a single product without modifying the cart.
        /// </summary>
        /// <param name="userEmail">Email of the authenticated user</param>
        /// <param name="itemId">Product ID to purchase</param>
        /// <param name="quantity">Quantity to purchase</param>
        /// <param name="shippingMethodId">ID of the selected shipping method</param>
        /// <returns>
        /// Positive order ID on success.
        /// 0 if the user or product was not found.
        /// -1 if the item has insufficient stock.
        /// -2 if the user has no delivery address on file.
        /// -3 if the shipping method is invalid or inactive.
        /// </returns>
        /// <remarks>
        /// This is an isolated single-item purchase that does NOT increment or modify
        /// the user's existing cart. Stock is validated and decremented atomically
        /// inside a transaction. All validation rules from PlaceOrderAsync apply.
        /// </remarks>
        Task<int> BuyNowAsync(string userEmail, int itemId, int quantity, int shippingMethodId);

        /// <summary>
        /// Removes a specific item from the user's shopping cart.
        /// </summary>
        /// <param name="userEmail">Email of the authenticated user</param>
        /// <param name="cartId">Primary key of the cart item to remove</param>
        /// <returns>Task representing the async operation</returns>
        /// <remarks>
        /// Security: Cart item ownership is validated before removal.
        /// Users can only remove items from their own cart.
        /// </remarks>
        Task RemoveFromCartAsync(string userEmail, int cartId);

        /// <summary>
        /// Processes the user's current cart into an order with atomic transaction semantics.
        /// </summary>
        /// <param name="userEmail">Email of the authenticated user</param>
        /// <param name="shippingMethodId">ID of the selected shipping method</param>
        /// <returns>
        /// Positive order ID on success.
        /// 0 if the user was not found or the cart is empty.
        /// -1 if one or more cart items have insufficient stock.
        /// -2 if the user has no delivery address on file.
        /// -3 if the shipping method is invalid or inactive.
        /// </returns>
        /// <remarks>
        /// Transaction workflow:
        /// 1. Validates user exists and cart has items
        /// 2. Validates shipping method and calculates shipping cost
        /// 3. Creates OrderModel with calculated total including shipping
        /// 4. Creates OrderItemModel for each cart item
        /// 5. Clears user's cart
        /// 6. Commits transaction (all-or-nothing)
        /// 
        /// Returns 0 if user not found or cart is empty.
        /// Returns -3 if shipping method is invalid.
        /// Database errors trigger automatic transaction rollback.
        /// </remarks>
        Task<int> PlaceOrderAsync(string userEmail, int shippingMethodId);

        /// <summary>
        /// Retrieves all cart items for the user with eager-loaded product details.
        /// </summary>
        /// <param name="userEmail">Email of the authenticated user</param>
        /// <returns>List of cart items with product navigation properties populated</returns>
        /// <remarks>
        /// Performance: Uses Include() to prevent N+1 query problems.
        /// Product details are available without additional queries.
        /// </remarks>
        Task<List<CartModel>> GetCartItemsAsync(string userEmail);
    }

}
