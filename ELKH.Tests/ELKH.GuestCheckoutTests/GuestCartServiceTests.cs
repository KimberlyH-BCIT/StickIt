using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;
using ELKH.Data;
using ELKH.Models;
using ELKH.Services;
using ELKH.ViewModels;

namespace ELKH.GuestCheckoutTests;

/// <summary>
/// Unit tests for GuestCartService - session-based cart for anonymous users.
/// Tests CRUD operations, cart retrieval, and cart migration functionality.
/// </summary>
public class GuestCartServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly Mock<ILogger<GuestCartService>> _mockLogger;
    private readonly Mock<ISession> _mockSession;
    private readonly GuestCartService _service;
    private readonly Dictionary<string, byte[]> _sessionStorage;

    public GuestCartServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        // Setup session storage dictionary
        _sessionStorage = new Dictionary<string, byte[]>();

        // Setup mock session
        _mockSession = new Mock<ISession>();
        _mockSession.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<byte[]>()))
            .Callback<string, byte[]>((key, value) => _sessionStorage[key] = value);
        _mockSession.Setup(s => s.TryGetValue(It.IsAny<string>(), out It.Ref<byte[]>.IsAny))
            .Returns((string key, out byte[] value) =>
            {
                var exists = _sessionStorage.TryGetValue(key, out var result);
                value = result!;
                return exists;
            });
        _mockSession.Setup(s => s.Remove(It.IsAny<string>()))
            .Callback<string>(key => _sessionStorage.Remove(key));

        // Setup HTTP context accessor
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(c => c.Session).Returns(_mockSession.Object);
        _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(mockHttpContext.Object);

        // Setup logger
        _mockLogger = new Mock<ILogger<GuestCartService>>();

        // Create service under test
        _service = new GuestCartService(
            _context,
            _mockHttpContextAccessor.Object,
            _mockLogger.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var products = new List<ProductModel>
        {
            new ProductModel
            {
                PkProductId = 1,
                Name = "Test Sticker 1",
                Price = 9.99m,
                DiscountPercent = 0,
                StockQuantity = 50,
                IsActive = true,
                FkCategoryId = 1
            },
            new ProductModel
            {
                PkProductId = 2,
                Name = "Test Sticker 2",
                Price = 14.99m,
                DiscountPercent = 10,
                StockQuantity = 30,
                IsActive = true,
                FkCategoryId = 1
            },
            new ProductModel
            {
                PkProductId = 3,
                Name = "Out of Stock Sticker",
                Price = 19.99m,
                DiscountPercent = 0,
                StockQuantity = 0,
                IsActive = true,
                FkCategoryId = 1
            }
        };

        _context.Products.AddRange(products);
        _context.SaveChanges();
    }

    [Fact]
    public async Task AddToCartAsync_WithValidProduct_ShouldAddItemToSession()
    {
        // Arrange
        int productId = 1;
        int quantity = 2;

        // Act
        await _service.AddToCartAsync(productId, quantity);

        // Assert
        var items = await _service.GetCartItemsAsync();
        items.Should().HaveCount(1);
        items.First().ProductId.Should().Be(productId);
        items.First().Quantity.Should().Be(quantity);
        items.First().ProductName.Should().Be("Test Sticker 1");
        items.First().UnitPrice.Should().Be(9.99m);
    }

    [Fact]
    public async Task AddToCartAsync_WithExistingProduct_ShouldUpdateQuantity()
    {
        // Arrange
        int productId = 1;
        await _service.AddToCartAsync(productId, 2);

        // Act
        await _service.AddToCartAsync(productId, 3);

        // Assert
        var items = await _service.GetCartItemsAsync();
        items.Should().HaveCount(1);
        items.First().Quantity.Should().Be(5); // 2 + 3
    }

    [Fact]
    public async Task AddToCartAsync_WithInvalidProduct_ShouldThrowException()
    {
        // Arrange
        int invalidProductId = 999;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _service.AddToCartAsync(invalidProductId, 1));
    }

    [Fact]
    public async Task AddToCartAsync_WithOutOfStockProduct_ShouldThrowException()
    {
        // Arrange
        int outOfStockProductId = 3;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _service.AddToCartAsync(outOfStockProductId, 1));
        
        exception.Message.Should().Contain("out of stock");
    }

    [Fact]
    public async Task AddToCartAsync_WithQuantityExceedingStock_ShouldThrowException()
    {
        // Arrange
        int productId = 1;
        int excessiveQuantity = 100; // Stock is only 50

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _service.AddToCartAsync(productId, excessiveQuantity));
        
        exception.Message.Should().Contain("Only 50 available");
    }

    [Fact]
    public async Task UpdateQuantityAsync_WithValidData_ShouldUpdateQuantity()
    {
        // Arrange
        int productId = 1;
        await _service.AddToCartAsync(productId, 2);

        // Act
        await _service.UpdateQuantityAsync(productId, 5);

        // Assert
        var items = await _service.GetCartItemsAsync();
        items.First().Quantity.Should().Be(5);
    }

    [Fact]
    public async Task UpdateQuantityAsync_WithZeroQuantity_ShouldRemoveItem()
    {
        // Arrange
        int productId = 1;
        await _service.AddToCartAsync(productId, 2);

        // Act
        await _service.UpdateQuantityAsync(productId, 0);

        // Assert
        var items = await _service.GetCartItemsAsync();
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateQuantityAsync_WithNonExistentProduct_ShouldNotThrowException()
    {
        // Act
        var act = async () => await _service.UpdateQuantityAsync(999, 5);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RemoveFromCartAsync_WithExistingProduct_ShouldRemoveItem()
    {
        // Arrange
        await _service.AddToCartAsync(1, 2);
        await _service.AddToCartAsync(2, 3);

        // Act
        await _service.RemoveFromCartAsync(1);

        // Assert
        var items = await _service.GetCartItemsAsync();
        items.Should().HaveCount(1);
        items.First().ProductId.Should().Be(2);
    }

    [Fact]
    public async Task RemoveFromCartAsync_WithNonExistentProduct_ShouldNotThrowException()
    {
        // Act
        var act = async () => await _service.RemoveFromCartAsync(999);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ClearCartAsync_ShouldRemoveAllItems()
    {
        // Arrange
        await _service.AddToCartAsync(1, 2);
        await _service.AddToCartAsync(2, 3);

        // Act
        await _service.ClearCartAsync();

        // Assert
        var items = await _service.GetCartItemsAsync();
        items.Should().BeEmpty();
        _sessionStorage.Should().NotContainKey("GuestCart");
    }

    [Fact]
    public async Task GetCartItemsAsync_WithEmptyCart_ShouldReturnEmptyList()
    {
        // Act
        var items = await _service.GetCartItemsAsync();

        // Assert
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCartItemsAsync_WithMultipleItems_ShouldReturnAllItems()
    {
        // Arrange
        await _service.AddToCartAsync(1, 2);
        await _service.AddToCartAsync(2, 1);

        // Act
        var items = await _service.GetCartItemsAsync();

        // Assert
        items.Should().HaveCount(2);

        var item1 = items.First(i => i.ProductId == 1);
        item1.Quantity.Should().Be(2);
        item1.UnitPrice.Should().Be(9.99m);
        item1.LineTotal.Should().Be(19.98m);

        var item2 = items.First(i => i.ProductId == 2);
        item2.Quantity.Should().Be(1);
        // 14.99 * 0.90 = 13.491, use approximate comparison
        item2.UnitPrice.Should().BeApproximately(13.49m, 0.01m);
        item2.LineTotal.Should().BeApproximately(13.49m, 0.01m);
    }

    [Fact]
    public async Task GetCartItemsAsync_WithDiscountedProduct_ShouldCalculateEffectivePrice()
    {
        // Arrange
        await _service.AddToCartAsync(2, 1); // Product with 10% discount

        // Act
        var items = await _service.GetCartItemsAsync();

        // Assert
        var item = items.First();
        // 14.99 * 0.90 = 13.491, use approximate comparison to handle decimal precision
        item.UnitPrice.Should().BeApproximately(13.49m, 0.01m);
    }

    [Fact]
    public async Task GetCartCountAsync_WithEmptyCart_ShouldReturnZero()
    {
        // Act
        var count = await _service.GetCartCountAsync();

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetCartCountAsync_WithMultipleItems_ShouldReturnTotalQuantity()
    {
        // Arrange
        await _service.AddToCartAsync(1, 2);
        await _service.AddToCartAsync(2, 3);

        // Act
        var count = await _service.GetCartCountAsync();

        // Assert
        count.Should().Be(5); // 2 + 3
    }

    [Fact]
    public async Task MigrateToUserCartAsync_WithValidData_ShouldTransferItemsToUserCart()
    {
        // Arrange
        await _service.AddToCartAsync(1, 2);
        await _service.AddToCartAsync(2, 1);

        var mockCartService = new Mock<ICartService>();
        var userEmail = "test@example.com";

        // Act
        await _service.MigrateToUserCartAsync(userEmail, mockCartService.Object);

        // Assert
        mockCartService.Verify(
            c => c.AddToCartAsync(userEmail, 1, 2),
            Times.Once);
        mockCartService.Verify(
            c => c.AddToCartAsync(userEmail, 2, 1),
            Times.Once);

        // Session cart should be cleared after migration
        var items = await _service.GetCartItemsAsync();
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task MigrateToUserCartAsync_WithEmptyCart_ShouldNotCallCartService()
    {
        // Arrange
        var mockCartService = new Mock<ICartService>();
        var userEmail = "test@example.com";

        // Act
        await _service.MigrateToUserCartAsync(userEmail, mockCartService.Object);

        // Assert
        mockCartService.Verify(
            c => c.AddToCartAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task SessionCart_ShouldPersistAcrossMultipleCalls()
    {
        // Arrange & Act
        await _service.AddToCartAsync(1, 1);
        var items1 = await _service.GetCartItemsAsync();

        await _service.AddToCartAsync(2, 2);
        var items2 = await _service.GetCartItemsAsync();

        await _service.UpdateQuantityAsync(1, 3);
        var items3 = await _service.GetCartItemsAsync();

        // Assert
        items1.Should().HaveCount(1);
        items2.Should().HaveCount(2);
        items3.Should().HaveCount(2);
        items3.First(i => i.ProductId == 1).Quantity.Should().Be(3);
    }

    [Fact]
    public async Task GetCartItemsAsync_WithDeletedProduct_ShouldSkipInvalidItems()
    {
        // Arrange
        await _service.AddToCartAsync(1, 2);
        
        // Delete product from database
        var product = await _context.Products.FindAsync(1);
        _context.Products.Remove(product!);
        await _context.SaveChangesAsync();

        // Act
        var items = await _service.GetCartItemsAsync();

        // Assert
        items.Should().BeEmpty(); // Invalid product should be skipped
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task AddToCartAsync_WithVariousQuantities_ShouldStoreCorrectly(int quantity)
    {
        // Arrange & Act
        await _service.AddToCartAsync(1, quantity);

        // Assert
        var items = await _service.GetCartItemsAsync();
        items.First().Quantity.Should().Be(quantity);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
