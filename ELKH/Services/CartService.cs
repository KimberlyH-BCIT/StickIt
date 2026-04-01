using System.Linq;
using System.Threading.Tasks;
using ELKH.Data;
using ELKH.Models;
using ELKH.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace ELKH.Services
{
    public class CartService : ICartService
    {
        private readonly ApplicationDbContext _db;
        private readonly IUserService _userService;
        private readonly ELKH.Repositories.IContactDetailRepo _contactRepo;

        public CartService(ApplicationDbContext db, IUserService userService, ELKH.Repositories.IContactDetailRepo contactRepo)
        {
            _db = db;
            _userService = userService;
            _contactRepo = contactRepo;
        }

        public async Task AddToCartAsync(string userEmail, int itemId, int quantity)
        {
            var user = await _userService.GetByEmailAsync(userEmail);
            if (user == null) return;

            var product = await _db.Product.FindAsync(itemId);
            if (product == null) return;

            var effective = product.GetEffectivePrice();

            var existing = await _db.Carts.FirstOrDefaultAsync(
                c => c.FkRegisteredUserId == user.PkRegisteredUserId && c.FkProductID == itemId);

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

            await _db.SaveChangesAsync();
        }

        public async Task<int> BuyNowAsync(string userEmail, int itemId, int quantity)
        {
            var user = await _userService.GetByEmailAsync(userEmail);
            if (user == null) return 0;

            var product = await _db.Product.FindAsync(itemId);
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
                    // FIXED: Replaced "Placed" string with Enum
                    OrderStatus = OrderStatus.Pending,
                    TotalAmount = effective * quantity,
                    CreatedAt = System.DateTime.UtcNow,
                    // FIXED: Replaced "Pending" string with Enum
                    DeliveryStatus = DeliveryStatus.Pending,
                    FkRegisteredUserId = user.PkRegisteredUserId,
                    FkContactId = contactId
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

        public async Task RemoveFromCartAsync(string userEmail, int cartId)
        {
            var user = await _userService.GetByEmailAsync(userEmail);
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

        public async Task<int> PlaceOrderAsync(string userEmail)
        {
            var user = await _db.RegisteredUsers.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null) return 0;

            var items = await _db.Carts
                .Where(c => c.FkRegisteredUserId == user.PkRegisteredUserId)
                .ToListAsync();

            if (!items.Any()) return 0;

            var defaultContact = await _contactRepo.GetDefaultByUserIdAsync(user.PkRegisteredUserId);
            int contactId = defaultContact?.PkContactId ?? 0;
            if (contactId == 0) return -2;

            var productIds = items.Select(c => c.FkProductID).ToList();
            var products = await _db.Product
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

            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var total = items.Sum(i => i.TotalPrice);

                var order = new OrderModel
                {
                    // FIXED: Replaced "Placed" string with Enum
                    OrderStatus = OrderStatus.Pending,
                    TotalAmount = total,
                    CreatedAt = System.DateTime.UtcNow,
                    // FIXED: Replaced "Pending" string with Enum
                    DeliveryStatus = DeliveryStatus.Pending,
                    FkRegisteredUserId = user.PkRegisteredUserId,
                    FkContactId = contactId
                };
                _db.Orders.Add(order);

                await _db.SaveChangesAsync();

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

                _db.Carts.RemoveRange(items);
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
    }
}