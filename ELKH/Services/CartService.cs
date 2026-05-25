using ELKH.Extensions;

namespace ELKH.Services;

// ╔════════════════════════════════════════════════════════════════════════════════════╗
// ║ CartService - TABLE OF CONTENTS                                                  ║
// ╚════════════════════════════════════════════════════════════════════════════════════╝
//
// OVERVIEW: Shopping cart and order-placement service with inventory validation.
// TABLE OF CONTENTS:
// - ClearCartAsync
// - AddToCartAsync
// - BuyNowAsync
// - PlaceOrderAsync
// - Helper validation and totals logic

/// <summary>
/// Service for managing shopping cart operations and order placement.
/// Handles cart item management, inventory validation, and order processing with atomic transactions.
/// </summary>
public class CartService : ICartService
{
    private readonly ApplicationDbContext _db;
    private readonly IUserService _userService;
    private readonly ELKH.Repositories.IContactDetailRepo _contactRepo;
    private readonly IShippingService _shippingService;

    /// <summary>
    /// Initializes a new instance of <see cref="CartService"/>.
    /// </summary>
    /// <param name="db">EF Core context for cart, order, and product data.</param>
    /// <param name="userService">Cached user lookup service for resolving the acting user.</param>
    /// <param name="contactRepo">Repository for retrieving the user's default delivery address.</param>
    /// <param name="shippingService">Service for shipping method validation and cost calculation.</param>
    public CartService(ApplicationDbContext db, IUserService userService, ELKH.Repositories.IContactDetailRepo contactRepo, IShippingService shippingService)
    {
        _db = db;
        _userService = userService;
        _contactRepo = contactRepo;
        _shippingService = shippingService;
    }

    public async Task ClearCartAsync(string userEmail)
    {
        var user = await _userService.GetByEmailAsync(userEmail);
        if (user == null) return;

        var items = await _db.Carts
            .Where(c => c.FkRegisteredUserId == user.PkRegisteredUserId)
            .ToListAsync();

        if (items.Count == 0) return;

        _db.Carts.RemoveRange(items);
        await _db.SaveChangesAsync();
    }

    public async Task AddToCartAsync(string userEmail, int itemId, int quantity)
    {
        var user = await _userService.GetByEmailAsync(userEmail);
        if (user == null)
            throw new InvalidOperationException("User not found.");

        // Step 2: Validate product exists
        var product = await _db.Products.FindAsync(itemId);
        if (product == null)
            throw new InvalidOperationException("Product not found.");

        // Step 3: Validate stock availability
        if (product.StockQuantity <= 0)
            throw new InvalidOperationException("This item is out of stock and cannot be added to your cart.");

        var existing = await _db.Carts.FirstOrDefaultAsync(
            c => c.FkRegisteredUserId == user.PkRegisteredUserId && c.FkProductID == itemId);

        // Step 5: Validate total quantity doesn't exceed stock
        var totalQuantity = (existing?.Quantity ?? 0) + quantity;
        if (totalQuantity > product.StockQuantity)
            throw new InvalidOperationException($"Cannot add {quantity} items. Only {product.StockQuantity - (existing?.Quantity ?? 0)} available (you already have {existing?.Quantity ?? 0} in cart).");

        // Step 6: Calculate effective price (with discount applied)
        var effective = product.GetEffectivePrice();

        if (existing != null)
        {
            existing.Quantity += quantity;
            existing.TotalPrice = existing.Quantity * effective;
        }
        else
        {
            _db.Carts.Add(new CartModel
            {
                FkRegisteredUserId = user.PkRegisteredUserId,
                FkProductID = itemId,
                Quantity = quantity,
                TotalPrice = quantity * effective
            });
        }

        // Step 7: Persist changes
        await _db.SaveChangesAsync();
    }

    // ---------------------------------------------------------------------
    // Quick-purchase helpers
    // ---------------------------------------------------------------------
    /// <summary>
    /// Places an immediate order for a single product without modifying the cart.
    /// Validates the address and stock, then creates the order inside a transaction.
    /// Returns the same error codes as PlaceOrderAsync (-1 stock, -2 no address, -3 invalid shipping).
    /// </summary>
    public async Task<int> BuyNowAsync(string userEmail, int itemId, int quantity, int shippingMethodId)
    {
        var user = await _userService.GetByEmailAsync(userEmail);
        if (user == null) return 0;

        var product = await _db.Products.FindAsync(itemId);
        if (product == null) return 0;

        if ((product.StockQuantity) < quantity) return -1;

        // Validate shipping method
        var shippingMethod = await _shippingService.GetShippingMethodByIdAsync(shippingMethodId);
        if (shippingMethod == null || !shippingMethod.IsActive)
            return -3; // Invalid or inactive shipping method

        var defaultContact = await _contactRepo.GetDefaultByUserIdAsync(user.PkRegisteredUserId);
        int contactId = defaultContact?.PkContactId ?? 0;
        if (contactId == 0) return -2;

        var effective = product.GetEffectivePrice();
        var subtotal = effective * quantity;

        // Calculate shipping cost
        var shippingCost = await _shippingService.CalculateShippingCostAsync(shippingMethodId, subtotal);
        var totalAmount = subtotal + shippingCost;

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var order = new OrderModel
            {
                OrderStatus = OrderStatus.Pending,
                TotalAmount = totalAmount,
                CreatedAt = System.DateTime.UtcNow,
                DeliveryStatus = DeliveryStatus.Pending,
                FkRegisteredUserId = user.PkRegisteredUserId,
                FkContactId = contactId,
                FkShippingMethodId = shippingMethodId,
                ShippingMethodName = shippingMethod.Name,
                ShippingCost = shippingCost
            };
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            _db.OrderItems.Add(new OrderItemModel
            {
                FkOrderId = order.PkOrderId,
                FkProductId = itemId,
                Quantity = quantity,
                UnitPrice = effective
            });

            product.StockQuantity = (product.StockQuantity) - quantity;

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return order.PkOrderId;
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return -1;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateQuantityAsync(string userEmail, int cartId, int quantity)
    {
        if (quantity < 1) quantity = 1;

        var user = await _userService.GetByEmailAsync(userEmail);
        if (user == null) return;

        var item = await _db.Carts.FirstOrDefaultAsync(c =>
            c.PkCartId == cartId && c.FkRegisteredUserId == user.PkRegisteredUserId);

        if (item == null) return;

        // update qty
        item.Quantity = quantity;

        // keep TotalPrice in sync with displayed price
        var product = await _db.Products.FindAsync(item.FkProductID);
        var unit = product?.GetEffectivePrice() ?? 0m;
        item.TotalPrice = unit * quantity;

        await _db.SaveChangesAsync();
    }

    public async Task RemoveFromCartAsync(string userEmail, int cartId)
    {
        var user = await _userService.GetByEmailAsync(userEmail);
        if (user == null)
        {
            user = await _db.RegisteredUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == userEmail);
        }

        if (user == null) return;

        var item = await _db.Carts.FirstOrDefaultAsync(
            c => c.PkCartId == cartId && c.FkRegisteredUserId == user.PkRegisteredUserId);

        if (item != null)
        {
            _db.Carts.Remove(item);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<List<CartModel>> GetCartItemsAsync(string userEmail)
    {
        var user = await _userService.GetByEmailAsync(userEmail);
        if (user == null) return new List<CartModel>();

        return await _db.Carts
            .Include(c => c.Product)
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
    /// 1. Validate user exists
    /// 2. Validate cart has items
    /// 3. Validate shipping method and calculate shipping cost
    /// 4. Begin database transaction (ensures atomicity)
    /// 5. Create OrderModel with calculated total including shipping
    /// 6. Create OrderItemModel for each cart item
    /// 7. Clear user's cart (order is now placed)
    /// 8. Commit transaction (all-or-nothing)
    /// 
    /// Failure scenarios:
    /// - User not found: returns 0
    /// - Empty cart: returns 0
    /// - Invalid shipping method: returns -3
    /// - Database error: transaction rolled back automatically
    /// 
    /// Important: This method does NOT validate inventory stock levels.
    /// Inventory validation should be done in the controller before calling this method.
    /// 
    /// Performance: Single transaction with minimal round-trips.
    /// </remarks>
    public async Task<int> PlaceOrderAsync(string userEmail, int shippingMethodId)
    {
        var user = await _db.RegisteredUsers.FirstOrDefaultAsync(u => u.Email == userEmail);
        if (user == null) return 0;

        var items = await _db.Carts
            .Where(c => c.FkRegisteredUserId == user.PkRegisteredUserId)
            .ToListAsync();

        // Step 3: Validate cart is not empty
        if (items.Count == 0) return 0;

        // Step 4: Validate shipping method and calculate shipping cost
        var shippingMethod = await _shippingService.GetShippingMethodByIdAsync(shippingMethodId);
        if (shippingMethod == null || !shippingMethod.IsActive)
            return -3; // Invalid or inactive shipping method

        // Step 5: Resolve and validate delivery address before opening a transaction.
        // FkContactId = 0 would write an invalid FK to the Orders table.
        var defaultContact = await _contactRepo.GetDefaultByUserIdAsync(user.PkRegisteredUserId);
        int contactId = defaultContact?.PkContactId ?? 0;
        if (contactId == 0) return -2;

        // Step 6: Load products and validate stock levels before opening a transaction.
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

        // Step 7: Calculate shipping cost using cart subtotal
        var cartSubtotal = items.Sum(i => i.TotalPrice);
        var shippingCost = await _shippingService.CalculateShippingCostAsync(shippingMethodId, cartSubtotal);

        // Step 8: Begin explicit transaction for atomicity.
        // All subsequent operations must succeed or the entire transaction rolls back.
        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // Step 9: Calculate order totals (subtotal + shipping)
            var subtotal = items.Sum(i => i.TotalPrice);
            var totalAmount = subtotal + shippingCost;

            // Step 10: Create order entity with shipping information
            var order = new OrderModel
            {
                OrderStatus = OrderStatus.Pending,
                TotalAmount = totalAmount,
                CreatedAt = System.DateTime.UtcNow,
                DeliveryStatus = DeliveryStatus.Pending,
                FkRegisteredUserId = user.PkRegisteredUserId,
                FkContactId = contactId,
                FkShippingMethodId = shippingMethodId,
                ShippingMethodName = shippingMethod.Name,
                ShippingCost = shippingCost
            };
            _db.Orders.Add(order);

            // Step 11: Save to generate OrderId (needed for order items)
            await _db.SaveChangesAsync();

            // Step 12: Create order line items and decrement stock atomically.
            // Stock was validated in Step 6; decrementing here inside the transaction
            // ensures the decrement and the order creation are committed together.
            foreach (var c in items)
            {
                _db.OrderItems.Add(new OrderItemModel
                {
                    FkOrderId = order.PkOrderId,
                    FkProductId = c.FkProductID,
                    Quantity = c.Quantity,
                    UnitPrice = products[c.FkProductID].GetEffectivePrice()
                });
                products[c.FkProductID].StockQuantity =
                    (products[c.FkProductID].StockQuantity) - c.Quantity;
            }

            // Step 13: Clear the cart (order is now placed)
            _db.Carts.RemoveRange(items);

            // Step 14: Persist order items, stock updates, and cart removal
            await _db.SaveChangesAsync();

            await transaction.CommitAsync();
            return order.PkOrderId;
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return -1; // concurrent order modified stock first
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
