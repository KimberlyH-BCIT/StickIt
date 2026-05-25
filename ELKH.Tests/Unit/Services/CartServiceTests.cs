using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ELKH.Tests.Unit.Services;

// TABLE OF CONTENTS
// - AddToCartAsync tests
// - RemoveFromCartAsync tests
// - GetCartItemsAsync tests
// - ClearCartAsync tests

/// <summary>
/// Unit tests for CartService with mocked dependencies.
/// Tests cart operations including add, remove, and order placement.
/// </summary>
/// <remarks>
/// 1. AddToCartAsync tests
/// 2. RemoveFromCartAsync tests
/// 3. GetCartItemsAsync tests
/// 4. ClearCartAsync tests
/// </remarks>
public class CartServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<IContactDetailRepo> _mockContactDetailRepo;
    private readonly Mock<IShippingService> _mockShippingService;
    private readonly CartService _cartService;

    public CartServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        // Setup mocks
        _mockUserService = new Mock<IUserService>();
        _mockContactDetailRepo = new Mock<IContactDetailRepo>();
        _mockShippingService = new Mock<IShippingService>();

        // Create service under test
        _cartService = new CartService(
            _context,
            _mockUserService.Object,
            _mockContactDetailRepo.Object,
            _mockShippingService.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var user = new RegisteredUserModel
        {
            PkRegisteredUserId = 1,
            Email = "test@example.com"
        };

        var product = new ProductModel
        {
            PkProductId = 1,
            Name = "Test Product",
            Price = 19.99m,
            StockQuantity = 10
        };

        _context.RegisteredUsers.Add(user);
        _context.Products.Add(product);
        _context.SaveChanges();
    }

    [Fact]
    public async Task AddToCartAsync_WithValidData_ShouldAddItem()
    {
        // Arrange
        _mockUserService.Setup(u => u.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new RegisteredUserModel { PkRegisteredUserId = 1, Email = "test@example.com" });

        // Act
        await _cartService.AddToCartAsync("test@example.com", 1, 2);

        // Assert
        var cartItem = await _context.Carts.FirstOrDefaultAsync(c => c.FkRegisteredUserId == 1 && c.FkProductID == 1);
        cartItem.Should().NotBeNull();
        cartItem!.Quantity.Should().Be(2);
    }

    [Fact]
    public async Task AddToCartAsync_WithExistingItem_ShouldUpdateQuantity()
    {
        // Arrange
        var existingCartItem = new CartModel
        {
            FkRegisteredUserId = 1,
            FkProductID = 1,
            Quantity = 1
        };

        _context.Carts.Add(existingCartItem);
        await _context.SaveChangesAsync();

        _mockUserService.Setup(u => u.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new RegisteredUserModel { PkRegisteredUserId = 1, Email = "test@example.com" });

        // Act
        await _cartService.AddToCartAsync("test@example.com", 1, 2);

        // Assert
        var cartItem = await _context.Carts.FirstOrDefaultAsync(c => c.FkRegisteredUserId == 1 && c.FkProductID == 1);
        cartItem!.Quantity.Should().Be(3); // 1 + 2
    }

    [Fact]
    public async Task AddToCartAsync_WithInsufficientStock_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _mockUserService.Setup(u => u.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new RegisteredUserModel { PkRegisteredUserId = 1, Email = "test@example.com" });

        // Act
        var action = () => _cartService.AddToCartAsync("test@example.com", 1, 20); // More than available stock

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();

        var cartItem = await _context.Carts.FirstOrDefaultAsync(c => c.FkRegisteredUserId == 1 && c.FkProductID == 1);
        cartItem.Should().BeNull();
    }

    [Fact]
    public async Task RemoveFromCartAsync_WithValidData_ShouldRemoveItem()
    {
        // Arrange
        var cartItem = new CartModel
        {
            PkCartId = 1,
            FkRegisteredUserId = 1,
            FkProductID = 1,
            Quantity = 2
        };

        _context.Carts.Add(cartItem);
        await _context.SaveChangesAsync();

        // Act
        await _cartService.RemoveFromCartAsync("test@example.com", 1);

        // Assert
        var deletedItem = await _context.Carts.FindAsync(1);
        deletedItem.Should().BeNull();
    }

    [Fact]
    public async Task GetCartItemsAsync_ShouldReturnUserCartItems()
    {
        // Arrange
        var cartItems = new List<CartModel>
        {
            new CartModel { PkCartId = 1, FkRegisteredUserId = 1, FkProductID = 1, Quantity = 2 },
            new CartModel { PkCartId = 2, FkRegisteredUserId = 1, FkProductID = 2, Quantity = 1 }
        };

        _context.Carts.AddRange(cartItems);
        await _context.SaveChangesAsync();

        _mockUserService.Setup(u => u.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new RegisteredUserModel { PkRegisteredUserId = 1, Email = "test@example.com" });

        // Act
        var result = await _cartService.GetCartItemsAsync("test@example.com");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ClearCartAsync_ShouldRemoveAllUserCartItems()
    {
        // Arrange
        var cartItems = new List<CartModel>
        {
            new CartModel { FkRegisteredUserId = 1, FkProductID = 1, Quantity = 2 },
            new CartModel { FkRegisteredUserId = 1, FkProductID = 2, Quantity = 1 },
            new CartModel { FkRegisteredUserId = 2, FkProductID = 1, Quantity = 1 } // Different user
        };

        _context.Carts.AddRange(cartItems);
        await _context.SaveChangesAsync();

        _mockUserService.Setup(u => u.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new RegisteredUserModel { PkRegisteredUserId = 1, Email = "test@example.com" });

        // Act
        await _cartService.ClearCartAsync("test@example.com");

        // Assert
        var remainingItems = await _context.Carts.Where(c => c.FkRegisteredUserId == 1).ToListAsync();
        remainingItems.Should().BeEmpty();

        // Verify other users' items are not affected
        var otherUserItems = await _context.Carts.Where(c => c.FkRegisteredUserId == 2).ToListAsync();
        otherUserItems.Should().HaveCount(1);
    }

    private void Dispose()
    {
        _context.Dispose();
    }
}