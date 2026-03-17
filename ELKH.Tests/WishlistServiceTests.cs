using System.Linq;
using System.Threading.Tasks;
using ELKH.Data;
using ELKH.Models;
using ELKH.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ELKH.Tests;

/// <summary>
/// Unit tests for <see cref="WishlistService"/> using an EF Core in-memory database.
/// </summary>
public class WishlistServiceTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static ApplicationDbContext CreateDb(string name)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static (WishlistService svc, ApplicationDbContext db) Build(
        string dbName, RegisteredUserModel? user)
    {
        var db = CreateDb(dbName);
        var userSvc = new Mock<IUserService>();
        userSvc.Setup(u => u.GetByEmailAsync(It.IsAny<string>()))
               .ReturnsAsync(user);
        return (new WishlistService(db, userSvc.Object), db);
    }

    private static RegisteredUserModel TestUser(int id = 1) =>
        new() { PkRegisteredUserId = id, Email = "user@test.com" };

    private static ProductModel TestProduct(int id = 1) =>
        new() { PkProductId = id, Name = "Sticker", Price = 4.99m, IsActive = true };

    // ── AddAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_NewProduct_ReturnsSuccessAndCountOne()
    {
        var (svc, db) = Build("Wish_AddNew", TestUser());
        db.RegisteredUsers.Add(TestUser());
        db.Products.Add(TestProduct());
        await db.SaveChangesAsync();

        var result = await svc.AddAsync("user@test.com", 1);

        Assert.True(result.Success);
        Assert.Equal(1, result.Count);
        Assert.Single(db.WishListItems.ToList());
    }

    [Fact]
    public async Task AddAsync_DuplicateProduct_ReturnsAlreadyExists()
    {
        var (svc, db) = Build("Wish_AddDuplicate", TestUser());
        db.RegisteredUsers.Add(TestUser());
        db.Products.Add(TestProduct());
        var wishlist = new WishListModel { FkUserId = 1 };
        db.WishLists.Add(wishlist);
        await db.SaveChangesAsync();
        db.WishListItems.Add(new WishListItemModel { FkWishListId = wishlist.PkWishListId, FkProductId = 1 });
        await db.SaveChangesAsync();

        var result = await svc.AddAsync("user@test.com", 1);

        Assert.False(result.Success);
        Assert.True(result.AlreadyExists);
    }

    [Fact]
    public async Task AddAsync_ProductNotFound_ReturnsFailure()
    {
        var (svc, db) = Build("Wish_AddNoProduct", TestUser());
        db.RegisteredUsers.Add(TestUser());
        await db.SaveChangesAsync();

        var result = await svc.AddAsync("user@test.com", 999);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task AddAsync_UnknownUser_ReturnsFailure()
    {
        var (svc, _) = Build("Wish_AddNoUser", user: null);
        var result = await svc.AddAsync("nobody@test.com", 1);
        Assert.False(result.Success);
    }

    // ── RemoveAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveAsync_ExistingItem_RemovesAndReturnsSuccess()
    {
        var (svc, db) = Build("Wish_Remove", TestUser());
        db.RegisteredUsers.Add(TestUser());
        db.Products.Add(TestProduct());
        var wishlist = new WishListModel { FkUserId = 1 };
        db.WishLists.Add(wishlist);
        await db.SaveChangesAsync();
        db.WishListItems.Add(new WishListItemModel { FkWishListId = wishlist.PkWishListId, FkProductId = 1 });
        await db.SaveChangesAsync();

        var result = await svc.RemoveAsync("user@test.com", 1);

        Assert.True(result.Success);
        Assert.Equal(0, result.Count);
        Assert.Empty(db.WishListItems.ToList());
    }

    [Fact]
    public async Task RemoveAsync_ItemNotInWishlist_ReturnsFailure()
    {
        var (svc, db) = Build("Wish_RemoveMissing", TestUser());
        db.RegisteredUsers.Add(TestUser());
        var wishlist = new WishListModel { FkUserId = 1 };
        db.WishLists.Add(wishlist);
        await db.SaveChangesAsync();

        var result = await svc.RemoveAsync("user@test.com", 99);

        Assert.False(result.Success);
    }

    // ── GetItemsAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetItemsAsync_NoWishlist_ReturnsEmpty()
    {
        var (svc, db) = Build("Wish_GetEmpty", TestUser());
        db.RegisteredUsers.Add(TestUser());
        await db.SaveChangesAsync();

        var items = await svc.GetItemsAsync("user@test.com", "date_desc");

        Assert.Empty(items);
    }
}
