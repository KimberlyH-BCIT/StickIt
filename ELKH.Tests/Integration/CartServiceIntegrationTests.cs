using System;
using System.Threading.Tasks;
using ELKH.Data;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ELKH.Tests.Integration;

public class CartServiceIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    public CartServiceIntegrationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
    }
    public void Dispose() => _connection.Dispose();
    private ApplicationDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        return new ApplicationDbContext(opts);
    }
    private static CartService BuildService(ApplicationDbContext db, RegisteredUserModel? user, ContactDetailModel? contact = null)
    {
        var userSvc = new Mock<IUserService>();
        userSvc.Setup(u => u.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        var contactRepo = new Mock<IContactDetailRepo>();
        contactRepo.Setup(r => r.GetDefaultByUserIdAsync(It.IsAny<int>())).ReturnsAsync(contact);
        return new CartService(db, userSvc.Object, contactRepo.Object);
    }
    private static CategoryModel TestCategory() => new() { PkCategoryId = 1, CategoryName = "Test" };
    private static RegisteredUserModel TestUser() => new() { PkRegisteredUserId = 1, Email = "test@test.com" };
    private static ProductModel TestProduct(int stock = 10) => new() { PkProductId = 1, Name = "Sticker", NameNormalized = "sticker", Description = "desc", Price = 9.99m, StockQuantity = stock, IsActive = true, FkCategoryId = 1 };
    private static ContactDetailModel TestContact() => new() { PkContactId = 1, FkRegisteredUserId = 1, FirstName = "Jane", IsDefault = true };
    private async Task SeedBaseAsync(ApplicationDbContext db, int stock = 10, bool withContact = false)
    {
        db.Categories.Add(TestCategory());
        db.RegisteredUsers.Add(TestUser());
        db.Products.Add(TestProduct(stock));
        if (withContact) db.ContactDetails.Add(TestContact());
        await db.SaveChangesAsync();
    }
    [Fact]
    public async Task DirectInsert_CartWithOrphanProductFk_ThrowsDbUpdateException()
    {
        using var db = CreateContext();
        await SeedBaseAsync(db);
        db.Carts.Add(new CartModel { FkRegisteredUserId = 1, FkProductID = 999, Quantity = 1, TotalPrice = 9.99m });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task AddToCartAsync_ValidProduct_PersistsCorrectly()
    {
        using var db = CreateContext();
        await SeedBaseAsync(db);
        await BuildService(db, TestUser()).AddToCartAsync("test@test.com", 1, 3);
        var row = await db.Carts.SingleAsync();
        Assert.Equal(3, row.Quantity);
        Assert.Equal(1, row.FkProductID);
    }
    [Fact]
    public async Task PlaceOrderAsync_HappyPath_CreatesOrderAndDecrementsStock()
    {
        using var db = CreateContext();
        await SeedBaseAsync(db, stock: 5, withContact: true);
        db.Carts.Add(new CartModel { FkRegisteredUserId = 1, FkProductID = 1, Quantity = 2, TotalPrice = 19.98m });
        await db.SaveChangesAsync();
        var orderId = await BuildService(db, TestUser(), TestContact()).PlaceOrderAsync("test@test.com");
        Assert.True(orderId > 0);
        Assert.Empty(db.Carts);
        Assert.Equal(3, (await db.Products.FindAsync(1))!.StockQuantity);
    }
    [Fact]
    public async Task DeleteProduct_CascadesCartRows()
    {
        using var db = CreateContext();
        await SeedBaseAsync(db);
        db.Carts.Add(new CartModel { FkRegisteredUserId = 1, FkProductID = 1, Quantity = 1, TotalPrice = 9.99m });
        await db.SaveChangesAsync();
        db.Products.Remove((await db.Products.FindAsync(1))!);
        await db.SaveChangesAsync();
        Assert.Empty(db.Carts);
    }
}