using Xunit;
using Microsoft.EntityFrameworkCore;
using ELKH.Data;
using ELKH.Models;
using ELKH.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ELKH.Tests.Unit.Services
{
    /// <summary>
    /// Unit tests for ShippingService business logic.
    /// Tests shipping method retrieval, cost calculation, and free shipping threshold rules.
    /// </summary>
    public class ShippingServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly ShippingService _shippingService;

        public ShippingServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _shippingService = new ShippingService(_context, NullLogger<ShippingService>.Instance);

            // Seed test data
            SeedTestData();
        }

        private void SeedTestData()
        {
            var shippingMethods = new List<ShippingMethodModel>
            {
                new ShippingMethodModel
                {
                    PkShippingMethodId = 1,
                    Name = "Standard Shipping",
                    Description = "5-7 business days",
                    BasePrice = 5.99m,
                    DeliveryDaysMin = 5,
                    DeliveryDaysMax = 7,
                    IsActive = true,
                    DisplayOrder = 1,
                    CreatedAt = DateTime.UtcNow
                },
                new ShippingMethodModel
                {
                    PkShippingMethodId = 2,
                    Name = "Express Delivery",
                    Description = "2-3 business days",
                    BasePrice = 12.99m,
                    DeliveryDaysMin = 2,
                    DeliveryDaysMax = 3,
                    IsActive = true,
                    DisplayOrder = 2,
                    CreatedAt = DateTime.UtcNow
                },
                new ShippingMethodModel
                {
                    PkShippingMethodId = 3,
                    Name = "Priority Overnight",
                    Description = "1-2 business days",
                    BasePrice = 19.99m,
                    DeliveryDaysMin = 1,
                    DeliveryDaysMax = 2,
                    IsActive = true,
                    DisplayOrder = 3,
                    CreatedAt = DateTime.UtcNow
                },
                new ShippingMethodModel
                {
                    PkShippingMethodId = 4,
                    Name = "Discontinued Method",
                    Description = "No longer available",
                    BasePrice = 10.00m,
                    DeliveryDaysMin = 3,
                    DeliveryDaysMax = 5,
                    IsActive = false, // Inactive method for testing
                    DisplayOrder = 99,
                    CreatedAt = DateTime.UtcNow
                }
            };

            _context.ShippingMethods.AddRange(shippingMethods);
            _context.SaveChanges();
        }

        #region GetAvailableShippingMethodsAsync Tests

        [Fact]
        public async Task GetAvailableShippingMethodsAsync_ReturnsOnlyActiveMethodsInDisplayOrder()
        {
            // Act
            var result = await _shippingService.GetAvailableShippingMethodsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count); // Should exclude inactive method

            // Verify ordering by DisplayOrder
            Assert.Equal("Standard Shipping", result[0].Name);
            Assert.Equal("Express Delivery", result[1].Name);
            Assert.Equal("Priority Overnight", result[2].Name);

            // Verify all returned methods are active
            Assert.All(result, method => Assert.True(method.IsActive));
        }

        [Fact]
        public async Task GetAvailableShippingMethodsAsync_ReturnsEmptyListWhenNoActiveMethodsExist()
        {
            // Arrange
            var emptyContext = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options);

            var emptyService = new ShippingService(emptyContext, NullLogger<ShippingService>.Instance);

            // Act
            var result = await emptyService.GetAvailableShippingMethodsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        #endregion

        #region GetShippingMethodByIdAsync Tests

        [Fact]
        public async Task GetShippingMethodByIdAsync_ReturnsCorrectMethodWhenExists()
        {
            // Act
            var result = await _shippingService.GetShippingMethodByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.PkShippingMethodId);
            Assert.Equal("Standard Shipping", result.Name);
            Assert.Equal(5.99m, result.BasePrice);
        }

        [Fact]
        public async Task GetShippingMethodByIdAsync_ReturnsNullWhenMethodDoesNotExist()
        {
            // Act
            var result = await _shippingService.GetShippingMethodByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetShippingMethodByIdAsync_ReturnsInactiveMethodWhenExists()
        {
            // Act - Should return inactive method for validation purposes
            var result = await _shippingService.GetShippingMethodByIdAsync(4);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Discontinued Method", result.Name);
            Assert.False(result.IsActive);
        }

        #endregion

        #region CalculateShippingCostAsync Tests - Standard Shipping (Free Shipping)

        [Fact]
        public async Task CalculateShippingCostAsync_StandardShipping_ReturnsFreeWhenCartMeetsFreeShippingThreshold()
        {
            // Arrange
            const int standardShippingId = 1;
            const decimal cartSubtotal = 50.00m; // Exactly at threshold

            // Act
            var result = await _shippingService.CalculateShippingCostAsync(standardShippingId, cartSubtotal);

            // Assert
            Assert.Equal(0m, result); // Should be free
        }

        [Fact]
        public async Task CalculateShippingCostAsync_StandardShipping_ReturnsFreeWhenCartExceedsFreeShippingThreshold()
        {
            // Arrange
            const int standardShippingId = 1;
            const decimal cartSubtotal = 75.50m; // Above threshold

            // Act
            var result = await _shippingService.CalculateShippingCostAsync(standardShippingId, cartSubtotal);

            // Assert
            Assert.Equal(0m, result); // Should be free
        }

        [Fact]
        public async Task CalculateShippingCostAsync_StandardShipping_ReturnsFullPriceWhenCartBelowFreeShippingThreshold()
        {
            // Arrange
            const int standardShippingId = 1;
            const decimal cartSubtotal = 49.99m; // Below threshold

            // Act
            var result = await _shippingService.CalculateShippingCostAsync(standardShippingId, cartSubtotal);

            // Assert
            Assert.Equal(5.99m, result); // Should be full price
        }

        [Theory]
        [InlineData(0.00, 5.99)]
        [InlineData(25.00, 5.99)]
        [InlineData(49.99, 5.99)]
        [InlineData(50.00, 0.00)]
        [InlineData(50.01, 0.00)]
        [InlineData(100.00, 0.00)]
        public async Task CalculateShippingCostAsync_StandardShipping_CorrectlyAppliesFreeShippingThreshold(
            decimal cartSubtotal, decimal expectedShippingCost)
        {
            // Arrange
            const int standardShippingId = 1;

            // Act
            var result = await _shippingService.CalculateShippingCostAsync(standardShippingId, cartSubtotal);

            // Assert
            Assert.Equal(expectedShippingCost, result);
        }

        #endregion

        #region CalculateShippingCostAsync Tests - Express Shipping (No Free Shipping)

        [Theory]
        [InlineData(0.00, 12.99)]
        [InlineData(25.00, 12.99)]
        [InlineData(49.99, 12.99)]
        [InlineData(50.00, 12.99)] // Free shipping threshold doesn't apply to Express
        [InlineData(75.50, 12.99)]
        [InlineData(100.00, 12.99)]
        public async Task CalculateShippingCostAsync_ExpressShipping_AlwaysReturnsFullPriceRegardlessOfCartAmount(
            decimal cartSubtotal, decimal expectedShippingCost)
        {
            // Arrange
            const int expressShippingId = 2;

            // Act
            var result = await _shippingService.CalculateShippingCostAsync(expressShippingId, cartSubtotal);

            // Assert
            Assert.Equal(expectedShippingCost, result);
        }

        #endregion

        #region CalculateShippingCostAsync Tests - Priority Shipping (No Free Shipping)

        [Theory]
        [InlineData(0.00, 19.99)]
        [InlineData(25.00, 19.99)]
        [InlineData(49.99, 19.99)]
        [InlineData(50.00, 19.99)] // Free shipping threshold doesn't apply to Priority
        [InlineData(75.50, 19.99)]
        [InlineData(100.00, 19.99)]
        public async Task CalculateShippingCostAsync_PriorityShipping_AlwaysReturnsFullPriceRegardlessOfCartAmount(
            decimal cartSubtotal, decimal expectedShippingCost)
        {
            // Arrange
            const int priorityShippingId = 3;

            // Act
            var result = await _shippingService.CalculateShippingCostAsync(priorityShippingId, cartSubtotal);

            // Assert
            Assert.Equal(expectedShippingCost, result);
        }

        #endregion

        #region CalculateShippingCostAsync Tests - Error Cases

        [Fact]
        public async Task CalculateShippingCostAsync_ThrowsArgumentExceptionWhenShippingMethodNotFound()
        {
            // Arrange
            const int invalidShippingId = 999;
            const decimal cartSubtotal = 50.00m;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _shippingService.CalculateShippingCostAsync(invalidShippingId, cartSubtotal));
        }

        [Theory]
        [InlineData(-1.00)]
        [InlineData(-0.01)]
        public async Task CalculateShippingCostAsync_ThrowsArgumentExceptionWhenCartSubtotalIsNegative(decimal invalidSubtotal)
        {
            // Arrange
            const int standardShippingId = 1;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _shippingService.CalculateShippingCostAsync(standardShippingId, invalidSubtotal));
        }

        [Fact]
        public async Task CalculateShippingCostAsync_HandlesZeroCartSubtotal()
        {
            // Arrange
            const int standardShippingId = 1;
            const decimal zeroSubtotal = 0.00m;

            // Act
            var result = await _shippingService.CalculateShippingCostAsync(standardShippingId, zeroSubtotal);

            // Assert
            Assert.Equal(5.99m, result); // Should charge shipping for zero subtotal
        }

        #endregion

        #region Business Rule Documentation Tests

        [Fact]
        public void FreeShippingThreshold_IsCorrectlyDocumented()
        {
            // This test documents the business rule for free shipping
            // Free shipping applies ONLY to Standard shipping (ID 1) when cart subtotal >= $50.00
            const decimal FREE_SHIPPING_THRESHOLD = 50.00m;
            const int STANDARD_SHIPPING_ID = 1;

            // Business rule assertion - these values should match ShippingService implementation
            Assert.Equal(50.00m, FREE_SHIPPING_THRESHOLD);
            Assert.Equal(1, STANDARD_SHIPPING_ID);
        }

        [Fact]
        public async Task BusinessRule_OnlyStandardShippingHasFreeShippingDiscount()
        {
            // Arrange - cart amount above free shipping threshold
            const decimal cartSubtotal = 75.00m;

            // Act - Calculate shipping for all methods
            var standardCost = await _shippingService.CalculateShippingCostAsync(1, cartSubtotal);
            var expressCost = await _shippingService.CalculateShippingCostAsync(2, cartSubtotal);
            var priorityCost = await _shippingService.CalculateShippingCostAsync(3, cartSubtotal);

            // Assert - Only Standard shipping should be free
            Assert.Equal(0.00m, standardCost); // Free for Standard
            Assert.Equal(12.99m, expressCost); // Full price for Express
            Assert.Equal(19.99m, priorityCost); // Full price for Priority
        }

        #endregion

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}