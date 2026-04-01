using ELKH.Models;

namespace ELKH.Repositories;

/// <summary>
/// Repository interface for cart operations providing data access methods for
/// managing user shopping carts, cart items, and cart persistence operations.
/// </summary>
public interface ICartRepo
{
    Task<IEnumerable<CartModel>> GetByUserIdAsync(int registeredUserId);
    Task<CartModel?> GetByUserAndProductAsync(int registeredUserId, int productId);
    Task<bool> AddAsync(CartModel cart);
    Task<bool> UpdateAsync(CartModel cart);
    Task<bool> RemoveAsync(int cartId);
    Task<bool> ClearByUserIdAsync(int registeredUserId);
}
