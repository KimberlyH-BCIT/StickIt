using ELKH.Data;
using ELKH.Models;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Repositories;

/*
 OrderRepo
 Table of Contents
 1. Fields & Constructor
 2. Read
 3. Write
*/

public class OrderRepo : IOrderRepo
{
    #region Fields & Constructor
    private readonly ApplicationDbContext _context;

    public OrderRepo(ApplicationDbContext context) => _context = context;
    #endregion

    #region Read
    /// <summary>Returns a single order by primary key without any navigation properties loaded.</summary>
    public async Task<OrderModel?> GetByIdAsync(int id) =>
        await _context.Orders.FindAsync(id);

    /// <summary>
    /// Returns a single order with all detail needed for the order confirmation/detail view:
    /// line items with their products and images, the shipping contact, and the transaction record.
    /// </summary>
    public async Task<OrderModel?> GetByIdWithItemsAsync(int id) =>
        await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(i => i.Product!)
            .ThenInclude(p => p.ProductImage)
            .Include(o => o.ContactDetail)
            .Include(o => o.Transaction)
            .FirstOrDefaultAsync(o => o.PkOrderId == id);

    /// <summary>
    /// Returns all orders for a given registered user, newest first.
    /// Includes line items (with products) so callers can show order summaries without extra queries.
    /// </summary>
    public async Task<IEnumerable<OrderModel>> GetByUserIdAsync(int registeredUserId) =>
        await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(i => i.Product)
            .Where(o => o.FkRegisteredUserId == registeredUserId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    #endregion

    #region Write
    /// <summary>Persists a new order and returns the saved entity (with its generated primary key).</summary>
    public async Task<OrderModel> CreateAsync(OrderModel order)
    {
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return order;
    }

    /// <summary>
    /// Updates <see cref="OrderModel.OrderStatus"/> for the given order.
    /// Returns <c>false</c> when no order with <paramref name="orderId"/> exists or a database error occurs.
    /// </summary>
    public async Task<bool> UpdateStatusAsync(int orderId, string status)
    {
        try
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order is null) return false;

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