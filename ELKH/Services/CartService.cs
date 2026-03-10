using System.Linq;
using System.Threading.Tasks;
using ELKH.Data;
using ELKH.Models;
using ELKH.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace ELKH.Services
{
    /// <summary>
    /// Service for managing shopping cart operations and order placement.
    /// Handles cart item management, inventory validation, and order processing with atomic transactions.
    /// </summary>
    /// <remarks>
    /// TABLE OF CONTENTS
    /// ================================================================================
    /// 1. Fields & Constructor
    /// 2. Private Helpers
    ///    - GetUserByEmailAsync()                 // Cached user lookup
    /// 3. Cart Management
    ///    - AddToCartAsync()                      // Add/increment product in cart
    ///    - BuyNowAsync()                         // Quick purchase (add + order)
    ///    - RemoveFromCartAsync()                 // Remove cart item
    ///    - GetCartItemsAsync()                   // Retrieve cart with products
    /// 4. Order Processing
    ///    - PlaceOrderAsync()                     // Process cart → order (transactional)
    /// ================================================================================
    /// </remarks>
    public class CartService : ICartService
    {
        private readonly ApplicationDbContext _db;
        private readonly IUserService _userService;
        private readonly ELKH.Repositories.IContactDetailRepo _contactRepo;

        /// <summary>
        /// Initializes a new instance of <see cref="CartService"/>.
        /// </summary>
        /// <param name="db">EF Core context for cart, order, and product data.</param>
        /// <param name="userService">Cached user lookup service for resolving the acting user.</param>
        /// <param name="contactRepo">Repository for retrieving the user's default delivery address.</param>
        public CartService(ApplicationDbContext db, IUserService userService, ELKH.Repositories.IContactDetailRepo contactRepo)
        {
            _db = db;
            _userService = userService;
            _contactRepo = contactRepo;
        }

        // -------------------------------------------------------------------------
        /// <summary>
        /// Add a product to the user's shopping cart with the specified quantity.
        /// If the product already exists in the cart, increments the quantity.
        /// Uses effective price (after discount) for calculations.
        /// </summary>
        /// <param name="userEmail">Email of the authenticated user</param>
        /// <param name="itemId">Product ID to add</param>
        /// <param name="quantity">Quantity to add (must be positive)</param>
        /// <returns>Task representing the async operation</returns>
        /// <remarks>
        /// Business rules:
        /// - User must exist (retrieved via cached UserService)
        /// - Product must exist in catalog
        /// - If product already in cart, quantity is incremented
        /// - Total price calculated using effective price (base price - discount)
        /// - No inventory validation at this stage (validated at checkout)
        /// </remarks>
        public async Task AddToCartAsync(string userEmail, int itemId, int quantity)
        {
            // Step 1: Validate user exists
            var user = await _userService.GetByEmailAsync(userEmail);
            if (user == null) return;

            // Step 2: Validate product exists
            var product = await _db.Products.FindAsync(itemId);
            if (product == null) return;

            // Step 3: Calculate effective price (with discount applied)
            var effective = product.GetEffectivePrice();

            // Step 4: Check if product already in cart
            var existing = await _db.Carts.FirstOrDefaultAsync(
                c => c.FkRegisteredUserId == user.PkRegisteredUserId && c.FkProductID == itemId);

            if (existing != null)
            {
                // Product exists: increment quantity and recalculate total
                existing.Quantity += quantity;
                existing.TotalPrice = existing.Quantity * effective;
            }
            else
            {
                // New product: create new cart entry
                _db.Carts.Add(new CartModel 
                { 
                    FkRegisteredUserId = user.PkRegisteredUserId, 
                    FkProductID = itemId, 
                    Quantity = quantity, 
                    TotalPrice = quantity * effective 
                });
            }

            // Step 5: Persist changes
            await _db.SaveChangesAsync();
        }

        // ---------------------------------------------------------------------
        // Quick-purchase helpers
        // ---------------------------------------------------------------------
        /// <summary>
        /// Places an immediate order for a single product without modifying the cart.
        /// Validates the address and stock, then creates the order inside a transaction.
        /// Returns the same error codes as PlaceOrderAsync (-1 stock, -2 no address).
        /// </summary>
        public async Task<int> BuyNowAsync(string userEmail, int itemId, int quantity)
        {
            var user = await _userService.GetByEmailAsync(userEmail);
            if (user == null) return 0;

            var product = await _db.Products.FindAsync(itemId);
            if (product == null) return 0;

            if ((product.StockQuantity) < quantity) return -1;

            var defaultContact = await _contactRepo.GetDefaultByUserIdAsync(user.PkRegisteredUserId);
            int contactId = defaultContact?.PkContactId ?? 0;
            if (contactId == 0) return -2;

            var effective = product.GetEffectivePrice();

            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var order = new OrderModel
                {
                    OrderStatus        = "Placed",
                    TotalAmount        = effective * quantity,
                    CreatedAt          = System.DateTime.UtcNow,
                    DeliveryStatus     = "Pending",
                    FkRegisteredUserId = user.PkRegisteredUserId,
                    FkContactId        = contactId
                };
                _db.Orders.Add(order);
                await _db.SaveChangesAsync();

                _db.OrderItems.Add(new OrderItemModel
                {
                    FkOrderId   = order.PkOrderId,
                    FkProductId = itemId,
                    Quantity    = quantity
                });

                product.StockQuantity = (product.StockQuantity) - quantity;

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                return order.PkOrderId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        // ---------------------------------------------------------------------
        // Removal and retrieval
        // ---------------------------------------------------------------------
        /// <summary>
        /// Remove a specific item from the user's shopping cart.
        /// Enforces ownership by validating cart item belongs to the specified user.
        /// </summary>
        /// <param name="userEmail">Email of the authenticated user</param>
        /// <param name="cartId">Primary key of the cart item to remove</param>
        /// <returns>Task representing the async operation</returns>
        /// <remarks>
        /// Security: Cart item ownership is validated before removal.
        /// Users can only remove items from their own cart.
        /// </remarks>
        public async Task RemoveFromCartAsync(string userEmail, int cartId)
        {
            // Step 1: Validate user
            var user = await _userService.GetByEmailAsync(userEmail);
            if (user == null) return;

            // Step 2: Find cart item and validate ownership
            var item = await _db.Carts.FirstOrDefaultAsync(
                c => c.PkCartId == cartId && c.FkRegisteredUserId == user.PkRegisteredUserId);

            if (item != null)
            {
                // Step 3: Remove item and persist
                _db.Carts.Remove(item);
                await _db.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Retrieve all cart items for the specified user with eager-loaded product details.
        /// Uses Include() to prevent N+1 query problems when displaying cart.
        /// </summary>
        /// <param name="userEmail">Email of the authenticated user</param>
        /// <returns>List of cart items with product navigation properties populated</returns>
        /// <remarks>
        /// Performance: Single query with JOIN to Products table.
        /// Product details available without additional queries.
        /// </remarks>
        public async Task<List<CartModel>> GetCartItemsAsync(string userEmail)
        {
            var user = await _userService.GetByEmailAsync(userEmail);
            if (user == null) return new List<CartModel>();

            return await _db.Carts
                .Include(c => c.Product)  // Eager load product details (prevents N+1)
                .Where(c => c.FkRegisteredUserId == user.PkRegisteredUserId)
                .ToListAsync();
        }

        // ---------------------------------------------------------------------
        // Ordering
        // ---------------------------------------------------------------------
        /// <summary>
        /// Process the user's current cart into an order with atomic transaction semantics.
        /// Creates order, order items, clears cart, and returns the new order ID.
        /// </summary>
        /// <param name="userEmail">Email of the authenticated user</param>
        /// <returns>
        /// Positive order ID on success.
        /// 0 if the user was not found or the cart is empty.
        /// -1 if one or more cart items have insufficient stock.
        /// -2 if the user has no delivery address on file.
        /// </returns>
        /// <remarks>
        /// Transaction workflow:
        /// 1. Validate user exists
        /// 2. Validate cart has items
        /// 3. Begin database transaction (ensures atomicity)
        /// 4. Create OrderModel with calculated total
        /// 5. Create OrderItemModel for each cart item
        /// 6. Clear user's cart (order is now placed)
        /// 7. Commit transaction (all-or-nothing)
        /// 
        /// Failure scenarios:
        /// - User not found: returns 0
        /// - Empty cart: returns 0
        /// - Database error: transaction rolled back automatically
        /// 
        /// Important: This method does NOT validate inventory stock levels.
        /// Inventory validation should be done in the controller before calling this method.
        /// 
        /// Performance: Single transaction with minimal round-trips.
        /// </remarks>
        public async Task<int> PlaceOrderAsync(string userEmail)
        {
            // Step 1: Retrieve and validate user
            // NOTE: Direct query here instead of UserService to avoid caching issues during transaction
            var user = await _db.RegisteredUsers.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null) return 0;

            // Step 2: Retrieve all cart items for this user
            var items = await _db.Carts
                .Where(c => c.FkRegisteredUserId == user.PkRegisteredUserId)
                .ToListAsync();

            // Step 3: Validate cart is not empty
            if (!items.Any()) return 0;

                // Step 4: Resolve and validate delivery address before opening a transaction.
                // FkContactId = 0 would write an invalid FK to the Orders table.
                var defaultContact = await _contactRepo.GetDefaultByUserIdAsync(user.PkRegisteredUserId);
                int contactId = defaultContact?.PkContactId ?? 0;
                if (contactId == 0) return -2;

                // Step 5: Load products and validate stock levels before opening a transaction.
                var productIds = items.Select(c => c.FkProductID).ToList();
                var products = await _db.Products
                    .Where(p => productIds.Contains(p.PkProductId))
                    .ToDictionaryAsync(p => p.PkProductId);

                foreach (var c in items)
                {
                    if (!products.TryGetValue(c.FkProductID, out var p) ||
                        (p.StockQuantity) < c.Quantity)
                    {
                        return -1;
                    }
                }

                // Step 6: Begin explicit transaction for atomicity.
                // All subsequent operations must succeed or the entire transaction rolls back.
                await using var transaction = await _db.Database.BeginTransactionAsync();
                try
                {
                    // Step 7: Calculate order total from cart items
                    var total = items.Sum(i => i.TotalPrice);

                    // Step 8: Create order entity with initial status
                    var order = new OrderModel 
                    { 
                        OrderStatus = "Placed",
                        TotalAmount = total,
                        CreatedAt = System.DateTime.UtcNow,
                        DeliveryStatus = "Pending",
                        FkRegisteredUserId = user.PkRegisteredUserId,
                        FkContactId = contactId
                    };
                    _db.Orders.Add(order);

                    // Step 9: Save to generate OrderId (needed for order items)
                    await _db.SaveChangesAsync();

                    // Step 10: Create order line items and decrement stock atomically.
                    // Stock was validated in Step 5; decrementing here inside the transaction
                    // ensures the decrement and the order creation are committed together.
                    foreach (var c in items)
                    {
                        _db.OrderItems.Add(new OrderItemModel 
                        { 
                            FkOrderId = order.PkOrderId,
                            FkProductId = c.FkProductID,
                            Quantity = c.Quantity
                        });
                        products[c.FkProductID].StockQuantity =
                            (products[c.FkProductID].StockQuantity) - c.Quantity;
                    }

                    // Step 11: Clear the cart (order is now placed)
                    _db.Carts.RemoveRange(items);

                    // Step 12: Persist order items, stock updates, and cart removal
                    await _db.SaveChangesAsync();

                    await transaction.CommitAsync();
                    return order.PkOrderId;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
    }
}
