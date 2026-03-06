using ELKH.Models;

namespace ELKH.Repositories;

public interface ICartRepo
{
    Task<IEnumerable<CartModel>> GetByUserIdAsync(int registeredUserId);
    Task<CartModel?> GetByUserAndProductAsync(int registeredUserId, int productId);
    Task<bool> AddAsync(CartModel cart);
    Task<bool> UpdateAsync(CartModel cart);
    Task<bool> RemoveAsync(int cartId);
    Task<bool> ClearByUserIdAsync(int registeredUserId);
}