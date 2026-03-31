using ELKH.Data;
using ELKH.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELKH.Repositories;

/// <summary>
/// Repository for shopping cart data operations with user-scoped cart management.
/// 
/// Provides comprehensive CRUD operations for cart items with proper error handling,
/// logging, and transaction management. Supports both individual item operations
/// and bulk cart management for optimal user experience.
/// </summary>
/// <remarks>
/// TABLE OF CONTENTS (120 lines)
/// ================================================================================
/// 1. Constructor & Dependencies ................................... Lines   35-45
///    - ApplicationDbContext, ILogger injection for data access and monitoring
/// 
/// 2. Cart Retrieval Operations .................................... Lines   47-65
///    - GetByUserIdAsync()                    // Complete user cart with product details
///    - GetByUserAndProductAsync()            // Specific cart item lookup for updates
/// 
/// 3. Cart Modification Operations ................................. Lines   67-120
///    - AddAsync()                            // Add new cart item with error handling
///    - UpdateAsync()                         // Modify quantity and pricing
///    - RemoveAsync()                         // Delete individual cart item
///    - ClearByUserIdAsync()                  // Empty entire user cart
/// ================================================================================
/// 
/// PERFORMANCE OPTIMIZATIONS:
/// • Include() statements load related product and image data in single query
/// • Efficient single-item lookups using composite key matching
/// • Batch operations for cart clearing minimize database round trips
/// • Proper async/await patterns prevent thread blocking
/// 
/// ERROR HANDLING STRATEGY:
/// • DbUpdateException logging for database constraint violations
/// • Boolean return values indicate operation success/failure
/// • Structured logging with user and cart identifiers for debugging
/// • Graceful degradation on database errors without throwing exceptions
/// 
/// DATA CONSISTENCY GUARANTEES:
/// • Entity Framework change tracking ensures data integrity
/// • Atomic operations through DbContext transaction management
/// • Proper foreign key relationships prevent orphaned cart items
/// • Cart item validation through repository pattern abstraction
/// 
/// BUSINESS RULES ENFORCED:
/// • User-scoped cart access prevents cross-user data leakage
/// • Cart item quantities and pricing validated at service layer
/// • Product availability checks delegated to business logic layer
/// • Cart persistence supports multiple browser sessions and devices
/// </remarks>
public class CartRepo : ICartRepo
{
    #region Constructor & Dependencies

    private readonly ApplicationDbContext _context;
    private readonly ILogger<CartRepo> _logger;

    /// <summary>
    /// Initializes the cart repository with database context and logging capabilities.
    /// </summary>
    /// <param name="context">Entity Framework database context for cart operations</param>
    /// <param name="logger">Logger for monitoring cart operations and error tracking</param>
    public CartRepo(ApplicationDbContext context, ILogger<CartRepo> logger)
    {
        _context = context;
        _logger  = logger;
    }

    #endregion

    #region Cart Retrieval Operations

    /// <summary>
    /// Retrieves all cart items for a specific user with complete product information.
    /// </summary>
    /// <param name="registeredUserId">The user's unique identifier</param>
    /// <returns>Complete cart with products and images eagerly loaded</returns>
    /// <remarks>
    /// PERFORMANCE OPTIMIZATION:
    /// • Include() loads products and images in single database query
    /// • ThenInclude() prevents N+1 query issues for product images
    /// • AsNoTracking could be added for read-only scenarios
    /// 
    /// BUSINESS VALUE:
    /// • Provides complete cart data for checkout calculations
    /// • Enables rich cart display with product images and details
    /// • Supports cart total calculations and tax computations
    /// </remarks>
    public async Task<IEnumerable<CartModel>> GetByUserIdAsync(int registeredUserId) =>
        await _context.Carts
            .Include(c => c.Product)              // Load product details for pricing
                .ThenInclude(p => p!.ProductImage) // Load product images for display
            .Where(c => c.FkRegisteredUserId == registeredUserId)
            .ToListAsync();

    /// <summary>
    /// Finds a specific cart item for a user-product combination.
    /// </summary>
    /// <param name="registeredUserId">The user's unique identifier</param>
    /// <param name="productId">The product's unique identifier</param>
    /// <returns>Existing cart item if found, null if no match</returns>
    /// <remarks>
    /// BUSINESS LOGIC SUPPORT:
    /// • Enables "add to cart" quantity increments for existing items
    /// • Prevents duplicate cart entries for same product
    /// • Supports cart item updates and modifications
    /// 
    /// PERFORMANCE NOTE:
    /// • Efficient composite key lookup using indexed foreign keys
    /// • No eager loading needed for existence checks and updates
    /// </remarks>
    public async Task<CartModel?> GetByUserAndProductAsync(int registeredUserId, int productId) =>
        await _context.Carts
            .FirstOrDefaultAsync(c => c.FkRegisteredUserId == registeredUserId && c.FkProductID == productId);

    #endregion

    #region Cart Modification Operations
    /// <summary>
    /// Adds a new cart item to the database with comprehensive error handling.
    /// </summary>
    /// <param name="cart">The cart item to add with user, product, and quantity information</param>
    /// <returns>True if successfully added, false if database error occurred</returns>
    /// <remarks>
    /// ERROR HANDLING:
    /// • DbUpdateException caught and logged for debugging
    /// • Foreign key constraint violations logged with context
    /// • Graceful failure prevents application crashes
    /// 
    /// BUSINESS RULES:
    /// • Service layer should validate product availability before calling
    /// • Service layer should check for existing cart items to increment instead
    /// • Cart item pricing should be calculated by service layer
    /// </remarks>
    public async Task<bool> AddAsync(CartModel cart)
    {
        try
        {
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error adding cart item for user {UserId}, product {ProductId}", 
                cart.FkRegisteredUserId, cart.FkProductID);
            return false;
        }
    }

    /// <summary>
    /// Updates an existing cart item's quantity and total price.
    /// </summary>
    /// <param name="cart">Cart item with updated quantity and pricing information</param>
    /// <returns>True if successfully updated, false if item not found or database error</returns>
    /// <remarks>
    /// UPDATE STRATEGY:
    /// • Find existing entity first to ensure it exists and track changes
    /// • Selective property updates preserve audit fields and timestamps
    /// • Change tracking automatically detects modifications for efficient updates
    /// 
    /// BUSINESS VALIDATION:
    /// • Quantity validation should occur at service layer
    /// • Price recalculation should be handled by business logic
    /// • Inventory checks delegated to service layer
    /// </remarks>
    public async Task<bool> UpdateAsync(CartModel cart)
    {
        try
        {
            var existing = await _context.Carts.FindAsync(cart.PkCartId);
            if (existing is null) 
            {
                _logger.LogWarning("Attempted to update non-existent cart item {CartId}", cart.PkCartId);
                return false;
            }

            // Update only the modifiable properties
            existing.Quantity   = cart.Quantity;
            existing.TotalPrice = cart.TotalPrice;

            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error updating cart item {CartId} for user {UserId}", 
                cart.PkCartId, cart.FkRegisteredUserId);
            return false;
        }
    }

    /// <summary>
    /// Removes a specific cart item from the database.
    /// </summary>
    /// <param name="cartId">The unique identifier of the cart item to remove</param>
    /// <returns>True if successfully removed, false if item not found or database error</returns>
    /// <remarks>
    /// DELETION STRATEGY:
    /// • Hard delete removes cart items completely (no audit trail needed for temporary cart data)
    /// • Find first to ensure item exists before attempting removal
    /// • Graceful handling of concurrent deletion scenarios
    /// </remarks>
    public async Task<bool> RemoveAsync(int cartId)
    {
        try
        {
            var cart = await _context.Carts.FindAsync(cartId);
            if (cart is null) 
            {
                _logger.LogWarning("Attempted to remove non-existent cart item {CartId}", cartId);
                return false;
            }

            _context.Carts.Remove(cart);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error removing cart item {CartId}", cartId);
            return false;
        }
    }

    /// <summary>
    /// Clears all cart items for a specific user (typically used after checkout completion).
    /// </summary>
    /// <param name="registeredUserId">The user's unique identifier</param>
    /// <returns>True if successfully cleared, false if database error occurred</returns>
    /// <remarks>
    /// BULK OPERATION OPTIMIZATION:
    /// • Batch retrieval and removal minimizes database round trips
    /// • RemoveRange() efficiently handles multiple deletions
    /// • Single SaveChangesAsync() call for atomic transaction
    /// 
    /// BUSINESS SCENARIOS:
    /// • Called after successful order placement
    /// • Used for "clear cart" user functionality
    /// • Supports cart abandonment cleanup processes
    /// </remarks>
    public async Task<bool> ClearByUserIdAsync(int registeredUserId)
    {
        try
        {
            var items = await _context.Carts
                .Where(c => c.FkRegisteredUserId == registeredUserId)
                .ToListAsync();

            if (items.Count > 0)
            {
                _context.Carts.RemoveRange(items);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Cleared {ItemCount} cart items for user {UserId}", 
                    items.Count, registeredUserId);
            }

            return true;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error clearing cart for user {UserId}", registeredUserId);
            return false;
        }
    }

    #endregion
}
