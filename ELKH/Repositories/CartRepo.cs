using ELKH.Data;
using ELKH.Models;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Repositories;

/*
 CartRepo
 Table of Contents
 1. Fields & Constructor
 2. Read
 3. Write
*/

public class CartRepo : ICartRepo
{
    #region Fields & Constructor
    private readonly ApplicationDbContext _context;

    public CartRepo(ApplicationDbContext context) => _context = context;
    #endregion

    #region Read
    public async Task<IEnumerable<CartModel>> GetByUserIdAsync(int registeredUserId) =>
        await _context.Carts
            .Include(c => c.Product)
                .ThenInclude(p => p.ProductImage)
            .Where(c => c.FkRegisteredUserId == registeredUserId)
            .ToListAsync();

    public async Task<CartModel?> GetByUserAndProductAsync(int registeredUserId, int productId) =>
        await _context.Carts
            .FirstOrDefaultAsync(c => c.FkRegisteredUserId == registeredUserId && c.FkProductID == productId);
    #endregion

    #region Write
    public async Task<bool> AddAsync(CartModel cart)
    {
        try
        {
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpdateAsync(CartModel cart)
    {
        try
        {
            var existing = await _context.Carts.FindAsync(cart.PkCartId);
            if (existing is null) return false;

            existing.Quantity = cart.Quantity;
            existing.TotalPrice = cart.TotalPrice;
            await _context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RemoveAsync(int cartId)
    {
        try
        {
            var cart = await _context.Carts.FindAsync(cartId);
            if (cart is null) return false;

            _context.Carts.Remove(cart);
            await _context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ClearByUserIdAsync(int registeredUserId)
    {
        try
        {
            var items = await _context.Carts
                .Where(c => c.FkRegisteredUserId == registeredUserId)
                .ToListAsync();

            _context.Carts.RemoveRange(items);
            await _context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
    #endregion
}
