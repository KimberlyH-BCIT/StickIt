using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using ELKH.Data;
using ELKH.Mapping;
using ELKH.Models;
using ELKH.Services;
using ELKH.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ELKH.Tests;

/// <summary>
/// Unit tests for <see cref="ProductService"/> using an EF Core in-memory database
/// and a real AutoMapper instance wired with <see cref="AutoMapperProfile"/>.
/// </summary>
public class ProductServiceTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static ApplicationDbContext CreateDb(string name)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfile>());
        return config.CreateMapper();
    }

    private static ProductService Build(ApplicationDbContext db)
    {
        var searchSvc = new Mock<ISearchService>();
        return new ProductService(db, searchSvc.Object, CreateMapper(), NullLogger<ProductService>.Instance);
    }

    private static CategoryModel TestCategory(int id = 1) =>
        new() { PkCategoryId = id, CategoryName = "Stickers" };

    private static ProductModel TestProduct(int id = 1, int catId = 1) =>
        new()
        {
            PkProductId  = id,
            Name         = "Galaxy Sticker",
            Description  = "Space-themed sticker",
            Price        = 3.99m,
            IsActive     = true,
            FkCategoryId = catId
        };

    // ── GetAllAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsMappedProducts()
    {
        var db = CreateDb("Prod_GetAll");
        db.Categories.Add(TestCategory());
        db.Products.Add(TestProduct());
        await db.SaveChangesAsync();

        var result = (await Build(db).GetAllAsync()).ToList();

        Assert.Single(result);
        Assert.Equal("Galaxy Sticker", result[0].ProductName);
    }

    [Fact]
    public async Task GetAllAsync_EmptyCatalog_ReturnsEmptyList()
    {
        var db = CreateDb("Prod_GetAllEmpty");
        var result = await Build(db).GetAllAsync();
        Assert.Empty(result);
    }

    // ── GetByIdAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsViewModel()
    {
        var db = CreateDb("Prod_GetById");
        db.Categories.Add(TestCategory());
        db.Products.Add(TestProduct());
        await db.SaveChangesAsync();

        var vm = await Build(db).GetByIdAsync(1);

        Assert.NotNull(vm);
        Assert.Equal(1, vm!.ProductId);
        Assert.Equal("Galaxy Sticker", vm.ProductName);
    }

    [Fact]
    public async Task GetByIdAsync_MissingId_ReturnsNull()
    {
        var db = CreateDb("Prod_GetByIdMissing");
        var vm = await Build(db).GetByIdAsync(999);
        Assert.Null(vm);
    }

    // ── CreateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_PersistsProductWithNormalizedName()
    {
        var db = CreateDb("Prod_Create");
        db.Categories.Add(TestCategory());
        await db.SaveChangesAsync();

        var vm = new ProductVM
        {
            ProductName = "Crème Brûlée",
            Description = "Dessert sticker",
            Price       = 2.50m,
            CategoryId  = 1,
            IsActive    = true
        };

        await Build(db).CreateAsync(vm);

        var product = await db.Products.SingleAsync();
        Assert.Equal("Crème Brûlée", product.Name);
        Assert.Equal("creme brulee", product.NameNormalized);
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ExistingProduct_UpdatesFieldsAndNormalizesName()
    {
        var db = CreateDb("Prod_Update");
        db.Categories.Add(TestCategory());
        db.Products.Add(TestProduct());
        await db.SaveChangesAsync();

        var vm = new ProductVM
        {
            ProductId   = 1,
            ProductName = "Étoile Sticker",
            Description = "Star sticker",
            Price       = 5.99m,
            CategoryId  = 1,
            IsActive    = true
        };

        await Build(db).UpdateAsync(vm);

        var product = await db.Products.FindAsync(1);
        Assert.Equal("Étoile Sticker", product!.Name);
        Assert.Equal("etoile sticker", product.NameNormalized);
        Assert.Equal(5.99m, product.Price);
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingProduct_RemovesIt()
    {
        var db = CreateDb("Prod_Delete");
        db.Categories.Add(TestCategory());
        db.Products.Add(TestProduct());
        await db.SaveChangesAsync();

        await Build(db).DeleteAsync(1);

        Assert.Empty(db.Products.ToList());
    }

    [Fact]
    public async Task DeleteAsync_MissingProduct_DoesNotThrow()
    {
        var db = CreateDb("Prod_DeleteMissing");
        var ex = await Record.ExceptionAsync(() => Build(db).DeleteAsync(999));
        Assert.Null(ex);
    }
}
