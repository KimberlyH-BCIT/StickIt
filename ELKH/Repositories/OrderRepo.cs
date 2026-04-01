using ELKH.Data;
using ELKH.Models;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Repositories;

public class OrderRepo : IOrderRepo
{
    #region Fields & Constructor
    private readonly ApplicationDbContext _context;

    public OrderRepo(ApplicationDbContext context) => _context = context;
    #endregion

    #region Read
    public async Task<OrderModel?> GetByIdAsync(int id) =>
        await _context.Orders.FindAsync(id);

    public async Task<OrderModel?> GetByIdWithItemsAsync(int id) =>
        await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(i => i.Product!)
            .ThenInclude(p => p.ProductImage)
            .Include(o => o.ContactDetail)
            .Include(o => o.Transaction)
            .FirstOrDefaultAsync(o => o.PkOrderId == id);

    public async Task<IEnumerable<OrderModel>> GetByUserIdAsync(int registeredUserId) =>
        await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(i => i.Product)
            .Where(o => o.FkRegisteredUserId == registeredUserId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    #endregion

    #region Write
    public async Task<OrderModel> CreateAsync(OrderModel order)
    {
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return order;
    }

    /// <summary>
    /// UPDATED: Parameter 'status' changed from string to OrderStatus Enum.
    /// </summary>
    public async Task<bool> UpdateStatusAsync(int orderId, OrderStatus status)
    {
        try
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order is null) return false;

            // FIXED: Assigning Enum directly instead of string
            order.OrderStatus = status;
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