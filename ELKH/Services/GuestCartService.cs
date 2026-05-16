using System.Text.Json;
using ELKH.Data;
using ELKH.Extensions;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Services
{
    /// <summary>
    /// Service for managing session-based shopping carts for guest (anonymous) users.
    /// Provides cart functionality without requiring authentication.
    /// </summary>
    /// <remarks>
/// <para><strong>Table of Contents:</strong></para>
/// <list type="number">
/// <item>Section 1: Service Setup &amp; Dependencies</item>
/// <item>Section 2: Cart Item Management</item>
/// <item>Section 3: Cart Retrieval &amp; Display</item>
/// <item>Section 4: Session Persistence Helpers</item>
/// </list>
/// 
    /// <para><strong>SESSION-BASED CART STRATEGY:</strong></para>
    /// <list type="bullet">
    /// <item>Cart stored in session as JSON (SessionCart key)</item>
    /// <item>Session expires after 20 minutes of inactivity (configurable)</item>
    /// <item>Cart items contain product IDs and quantities only</item>
    /// <item>Product details fetched on-demand from database</item>
    /// </list>
    /// 
    /// <para><strong>MIGRATION PATH:</strong></para>
    /// <list type="bullet">
    /// <item>When guest creates account or logs in, session cart can be migrated to database</item>
    /// <item>Merge strategy: Add session items to user's existing cart (if any)</item>
    /// </list>
    /// 
    /// <para><strong>PERFORMANCE:</strong></para>
    /// <list type="bullet">
    /// <item>Lightweight session storage (IDs only, not full product data)</item>
    /// <item>Batch product lookup for cart display</item>
    /// <item>No database writes until checkout</item>
    /// </list>
    /// </remarks>
    public class GuestCartService : IGuestCartService
    {
        #region Section 1: Service Setup & Dependencies

        // ===================================================================
        // Section 1: Service Setup & Dependencies
        // ===================================================================

        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<GuestCartService> _logger;
        private const string CART_SESSION_KEY = "GuestCart";

        public GuestCartService(
            ApplicationDbContext db,
            IHttpContextAccessor httpContextAccessor,
            ILogger<GuestCartService> logger)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        /// <summary>
        /// Gets the current HTTP session
        /// </summary>
        private ISession Session => _httpContextAccessor.HttpContext?.Session
            ?? throw new InvalidOperationException("Session is not available");

        #endregion

        #region Section 2: Cart Item Management

        // ===================================================================
        // Section 2: Cart Item Management
        // ===================================================================

        /// <summary>
        /// Adds a product to the guest's session cart
        /// </summary>
        public async Task AddToCartAsync(int productId, int quantity)
        {
            if (quantity <= 0)
                throw new ArgumentException("Quantity must be positive", nameof(quantity));

            // Verify product exists and is available
            var product = await _db.Products.FindAsync(productId);
            if (product == null)
                throw new InvalidOperationException("Product not found");

            if (product.StockQuantity <= 0)
                throw new InvalidOperationException("This item is out of stock");

            // Validate requested quantity doesn't exceed available stock
            if (quantity > product.StockQuantity)
                throw new InvalidOperationException($"Only {product.StockQuantity} available in stock");

            var cart = GetCartFromSession();

            // Check if product already in cart
            var existingItem = cart.FirstOrDefault(i => i.ProductId == productId);
            if (existingItem != null)
            {
                // Validate total quantity doesn't exceed stock
                int totalQuantity = existingItem.Quantity + quantity;
                if (totalQuantity > product.StockQuantity)
                    throw new InvalidOperationException($"Only {product.StockQuantity} available in stock");

                existingItem.Quantity += quantity;
            }
            else
            {
                cart.Add(new SessionCartItem
                {
                    ProductId = productId,
                    Quantity = quantity,
                    AddedAt = DateTime.UtcNow
                });
            }

            SaveCartToSession(cart);
            _logger.LogInformation("Guest added product {ProductId} (qty: {Quantity}) to session cart", productId, quantity);
        }

        /// <summary>
        /// Updates the quantity of a cart item. If quantity is 0, removes the item.
        /// </summary>
        public async Task UpdateQuantityAsync(int productId, int newQuantity)
        {
            if (newQuantity < 0)
                throw new ArgumentException("Quantity cannot be negative", nameof(newQuantity));

            var cart = GetCartFromSession();
            var item = cart.FirstOrDefault(i => i.ProductId == productId);

            if (item == null)
            {
                // Item not in cart - this is acceptable, just log and return
                _logger.LogWarning("Attempted to update quantity for product {ProductId} not in cart", productId);
                return;
            }

            // If quantity is 0, remove the item
            if (newQuantity == 0)
            {
                cart.Remove(item);
                _logger.LogInformation("Guest removed product {ProductId} from cart (quantity set to 0)", productId);
            }
            else
            {
                item.Quantity = newQuantity;
                _logger.LogInformation("Guest updated product {ProductId} quantity to {Quantity}", productId, newQuantity);
            }

            SaveCartToSession(cart);
        }

        /// <summary>
        /// Removes a product from the guest's cart
        /// </summary>
        public async Task RemoveFromCartAsync(int productId)
        {
            var cart = GetCartFromSession();
            cart.RemoveAll(i => i.ProductId == productId);
            SaveCartToSession(cart);

            _logger.LogInformation("Guest removed product {ProductId} from session cart", productId);
        }

        /// <summary>
        /// Clears all items from the guest's cart
        /// </summary>
        public async Task ClearCartAsync()
        {
            Session.Remove(CART_SESSION_KEY);
            _logger.LogInformation("Guest cart cleared");
        }

        #endregion

        #region Section 3: Cart Retrieval & Display

        // ===================================================================
        // Section 3: Cart Retrieval & Display
        // ===================================================================

        /// <summary>
        /// Gets the guest's cart items with full product details
        /// </summary>
        public async Task<List<CartItemVM>> GetCartItemsAsync()
        {
            var cart = GetCartFromSession();

            if (cart.Count == 0)
                return new List<CartItemVM>();

            var productIds = cart.Select(i => i.ProductId).ToList();

            // Batch fetch products
            var products = await _db.Products
                .AsNoTracking()
                .Include(p => p.ProductImage)
                .Include(p => p.Category)
                .Where(p => productIds.Contains(p.PkProductId))
                .ToDictionaryAsync(p => p.PkProductId);

            var cartItems = new List<CartItemVM>();

            foreach (var item in cart)
            {
                if (!products.TryGetValue(item.ProductId, out var product))
                {
                    _logger.LogWarning("Product {ProductId} in guest cart not found in database", item.ProductId);
                    continue;
                }

                var effectivePrice = product.GetEffectivePrice();

                cartItems.Add(new CartItemVM
                {
                    ProductId = product.PkProductId,
                    ProductName = product.Name,
                    UnitPrice = effectivePrice,
                    Quantity = item.Quantity,
                    ImageUrl = product.ProductImage?.FirstOrDefault()?.ProductImageURL,
                    LineTotal = effectivePrice * item.Quantity
                });
            }

            return cartItems;
        }

        /// <summary>
        /// Gets the count of items in the guest's cart
        /// </summary>
        public async Task<int> GetCartCountAsync()
        {
            var cart = GetCartFromSession();
            return cart.Sum(i => i.Quantity);
        }

        #endregion

        #region Section 4: User Authentication Migration

        // ===================================================================
        // Section 4: User Authentication Migration
        // ===================================================================

        /// <summary>
        /// Migrates guest cart to authenticated user's cart when they log in
        /// </summary>
        public async Task MigrateToUserCartAsync(string userEmail, ICartService cartService)
        {
            var cart = GetCartFromSession();

            if (cart.Count == 0)
                return;

            _logger.LogInformation("Migrating {Count} items from guest cart to user {Email}", cart.Count, userEmail);

            foreach (var item in cart)
            {
                try
                {
                    await cartService.AddToCartAsync(userEmail, item.ProductId, item.Quantity);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to migrate cart item {ProductId} for user {Email}", item.ProductId, userEmail);
                }
            }

            // Clear session cart after migration
            await ClearCartAsync();
        }

        #endregion

        #region Section 5: Session Storage Management

        // ===================================================================
        // Section 5: Session Storage Management
        // ===================================================================

        /// <summary>
        /// Retrieves cart from session storage
        /// </summary>
        private List<SessionCartItem> GetCartFromSession()
        {
            var cartJson = Session.GetString(CART_SESSION_KEY);

            if (string.IsNullOrEmpty(cartJson))
                return new List<SessionCartItem>();

            try
            {
                return JsonSerializer.Deserialize<List<SessionCartItem>>(cartJson) ?? new List<SessionCartItem>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize guest cart from session");
                return new List<SessionCartItem>();
            }
        }

        /// <summary>
        /// <summary>
        /// Saves cart to session storage
        /// </summary>
        private void SaveCartToSession(List<SessionCartItem> cart)
        {
            var cartJson = JsonSerializer.Serialize(cart);
            Session.SetString(CART_SESSION_KEY, cartJson);
        }

        /// <summary>
        /// Internal class for session cart storage
        /// </summary>
        private class SessionCartItem
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
            public DateTime AddedAt { get; set; }
        }

        #endregion
    }
}
