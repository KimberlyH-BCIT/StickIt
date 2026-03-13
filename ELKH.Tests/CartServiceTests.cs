using System.Linq;
using System.Threading.Tasks;
using ELKH.Data;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;
using Moq;
using Xunit;

namespace ELKH.Tests;

/// <summary>
/// Unit tests for <see cref="CartService"/> using an EF Core in-memory database.
/// IUserService and IContactDetailRepo are mocked so tests run without a real DB or network.
/// </summary>
public class CartServiceTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static ApplicationDbContext CreateDb(string name)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static (CartService svc, ApplicationDbContext db) Build(
        string dbName,
        RegisteredUserModel? user,
        ContactDetailModel? defaultContact = null)
    {
        var db = CreateDb(dbName);

        var userSvc = new Mock<IUserService>();
        userSvc.Setup(u => u.GetByEmailAsync(It.IsAny<string>()))
               .ReturnsAsync(user);

        var contactRepo = new Mock<IContactDetailRepo>();
        contactRepo.Setup(r => r.GetDefaultByUserIdAsync(It.IsAny<int>()))
                   .ReturnsAsync(defaultContact);

        return (new CartService(db, userSvc.Object, contactRepo.Object), db);
    }

    private static RegisteredUserModel TestUser(int id = 1) =>
        new() { PkRegisteredUserId = id, Email = "test@test.com" };

    private static ProductModel TestProduct(int id = 1, int stock = 10, decimal price = 9.99m) =>
        new() { PkProductId = id, Name = "Sticker", Price = price, StockQuantity = stock, IsActive = true };

    private static ContactDetailModel TestContact(int id = 1, int userId = 1) =>
        new() { PkContactId = id, FkRegisteredUserId = userId, FirstName = "Jane", IsDefault = true };

    // ── AddToCartAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task AddToCartAsync_NewItem_AddsCartRow()
    {
        var (svc, db) = Build("Cart_AddNew", TestUser());
        db.Products.Add(TestProduct());
        db.RegisteredUsers.Add(TestUser());
        await db.SaveChangesAsync();

        await svc.AddToCartAsync("test@test.com", 1, 2);

        var row = await db.Carts.SingleAsync();
        Assert.Equal(2, row.Quantity);
    }

    [Fact]
    public async Task AddToCartAsync_ExistingItem_IncrementsQuantity()
    {
        var (svc, db) = Build("Cart_AddExisting", TestUser());
        db.Products.Add(TestProduct());
        db.RegisteredUsers.Add(TestUser());
        db.Carts.Add(new CartModel { FkRegisteredUserId = 1, FkProductID = 1, Quantity = 1, TotalPrice = 9.99m });
        await db.SaveChangesAsync();

        await svc.AddToCartAsync("test@test.com", 1, 3);

        var row = await db.Carts.SingleAsync();
        Assert.Equal(4, row.Quantity);
    }

    [Fact]
    public async Task AddToCartAsync_UnknownUser_DoesNothing()
    {
        var (svc, db) = Build("Cart_AddUnknown", user: null);
        db.Products.Add(TestProduct());
        await db.SaveChangesAsync();

        await svc.AddToCartAsync("nobody@test.com", 1, 1);

        Assert.Empty(db.Carts);
    }

    // ── PlaceOrderAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task PlaceOrderAsync_HappyPath_CreatesOrderClearsCart()
    {
        var (svc, db) = Build("Cart_PlaceHappy", TestUser(), TestContact());
        db.RegisteredUsers.Add(TestUser());
        db.Products.Add(TestProduct(stock: 5));
        db.Carts.Add(new CartModel { FkRegisteredUserId = 1, FkProductID = 1, Quantity = 2, TotalPrice = 19.98m });
        await db.SaveChangesAsync();

        var orderId = await svc.PlaceOrderAsync("test@test.com");

        Assert.True(orderId > 0);
        Assert.Empty(db.Carts);

        var order = await db.Orders.Include(o => o.OrderItems).SingleAsync();
        Assert.Equal("Placed", order.OrderStatus);
        Assert.Single(order.OrderItems);

        var product = await db.Products.FindAsync(1);
        Assert.Equal(3, product!.StockQuantity);      // 5 - 2 = 3
    }

    [Fact]
    public async Task PlaceOrderAsync_UnknownUser_ReturnsZero()
    {
        var (svc, _) = Build("Cart_PlaceNoUser", user: null);
        var result = await svc.PlaceOrderAsync("ghost@test.com");
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task PlaceOrderAsync_EmptyCart_ReturnsZero()
    {
        var (svc, db) = Build("Cart_PlaceEmpty", TestUser(), TestContact());
        db.RegisteredUsers.Add(TestUser());
        await db.SaveChangesAsync();

        var result = await svc.PlaceOrderAsync("test@test.com");
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task PlaceOrderAsync_InsufficientStock_ReturnsMinusOne()
    {
        var (svc, db) = Build("Cart_PlaceNoStock", TestUser(), TestContact());
        db.RegisteredUsers.Add(TestUser());
        db.Products.Add(TestProduct(stock: 1));
        db.Carts.Add(new CartModel { FkRegisteredUserId = 1, FkProductID = 1, Quantity = 5, TotalPrice = 49.95m });
        await db.SaveChangesAsync();

        var result = await svc.PlaceOrderAsync("test@test.com");
        Assert.Equal(-1, result);
    }

    [Fact]
    public async Task PlaceOrderAsync_NoAddress_ReturnsMinusTwo()
    {
        var (svc, db) = Build("Cart_PlaceNoAddress", TestUser(), defaultContact: null);
        db.RegisteredUsers.Add(TestUser());
        db.Products.Add(TestProduct(stock: 5));
        db.Carts.Add(new CartModel { FkRegisteredUserId = 1, FkProductID = 1, Quantity = 1, TotalPrice = 9.99m });
        await db.SaveChangesAsync();

        var result = await svc.PlaceOrderAsync("test@test.com");
        Assert.Equal(-2, result);
    }

    // ── BuyNowAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task BuyNowAsync_HappyPath_CreatesOrderWithoutTouchingCart()
    {
        var (svc, db) = Build("Cart_BuyNow", TestUser(), TestContact());
        db.RegisteredUsers.Add(TestUser());
        db.Products.Add(TestProduct(stock: 3));
        await db.SaveChangesAsync();

        var orderId = await svc.BuyNowAsync("test@test.com", 1, 2);

        Assert.True(orderId > 0);
        Assert.Empty(db.Carts);   // cart untouched
        var product = await db.Products.FindAsync(1);
        Assert.Equal(1, product!.StockQuantity);   // 3 - 2
    }

    [Fact]
    public async Task BuyNowAsync_InsufficientStock_ReturnsMinusOne()
    {
        var (svc, db) = Build("Cart_BuyNowNoStock", TestUser(), TestContact());
        db.RegisteredUsers.Add(TestUser());
        db.Products.Add(TestProduct(stock: 1));
        await db.SaveChangesAsync();

        var result = await svc.BuyNowAsync("test@test.com", 1, 5);
        Assert.Equal(-1, result);
    }

    // ── AddToCartAsync boundary cases ─────────────────────────────────────────

    [Fact]
    public async Task AddToCartAsync_ZeroQuantity_AddsRowWithZeroQuantity()
    {
        // CartService does not validate quantity > 0 at the add stage
        // (validation happens at checkout). This test documents current behaviour.
        var (svc, db) = Build("Cart_ZeroQty", TestUser());
        db.Products.Add(TestProduct());
        db.RegisteredUsers.Add(TestUser());
        await db.SaveChangesAsync();

        await svc.AddToCartAsync("test@test.com", 1, 0);

        var row = await db.Carts.SingleAsync();
        Assert.Equal(0, row.Quantity);
    }

    [Fact]
    public async Task AddToCartAsync_ProductNotFound_DoesNothing()
    {
        var (svc, db) = Build("Cart_NoProduct", TestUser());
        db.RegisteredUsers.Add(TestUser());
        await db.SaveChangesAsync();

        // Product id 99 does not exist — service should silently no-op.
        await svc.AddToCartAsync("test@test.com", 99, 1);

        Assert.Empty(db.Carts);
    }

    // ── PlaceOrderAsync boundary cases ───────────────────────────────────────

    [Fact]
    public async Task PlaceOrderAsync_ExactlyEnoughStock_DecrementToZero()
    {
        // Placing an order for the exact remaining stock should succeed and
        // leave StockQuantity == 0 rather than going negative.
        var (svc, db) = Build("Cart_ExactStock", TestUser(), TestContact());
        db.RegisteredUsers.Add(TestUser());
        db.Products.Add(TestProduct(stock: 2));
        db.Carts.Add(new CartModel { FkRegisteredUserId = 1, FkProductID = 1, Quantity = 2, TotalPrice = 19.98m });
        await db.SaveChangesAsync();

        var orderId = await svc.PlaceOrderAsync("test@test.com");

        Assert.True(orderId > 0);
        var product = await db.Products.FindAsync(1);
        Assert.Equal(0, product!.StockQuantity);
    }
}
