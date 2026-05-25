using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace ELKH.Tests.Unit.Services;

// TABLE OF CONTENTS
// - GetAllAsync tests
// - GetByIdAsync tests
// - SearchNames tests
// - CreateAsync tests
// - UpdateAsync tests

/// <summary>
/// Unit tests for ProductService with mocked dependencies.
/// Tests business logic without database dependencies using in-memory database.
/// </summary>
/// <remarks>
/// 1. GetAllAsync tests
/// 2. GetByIdAsync tests
/// 3. SearchNames tests
/// 4. CreateAsync tests
/// 5. UpdateAsync tests
/// </remarks>
public class ProductServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ISearchService> _mockSearchService;
    private readonly Mock<IProductMapper> _mockMapper;
    private readonly IMemoryCache _cache;
    private readonly ProductService _productService;

    public ProductServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        // Setup mocks
        _mockSearchService = new Mock<ISearchService>();
        _mockMapper = new Mock<IProductMapper>();
        _cache = new MemoryCache(new MemoryCacheOptions());

        // Create service under test
        _productService = new ProductService(
            _context,
            _mockSearchService.Object,
            _mockMapper.Object,
            _cache,
            NullLogger<ProductService>.Instance);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnMappedProducts()
    {
        // Arrange
        var products = new List<ProductModel>
        {
            new ProductModel { PkProductId = 1, Name = "Test Product 1", Price = 10.99m },
            new ProductModel { PkProductId = 2, Name = "Test Product 2", Price = 15.99m }
        };

        _context.Products.AddRange(products);
        await _context.SaveChangesAsync();

        var expectedViewModels = products.Select(p => new ProductVM
        {
            ProductId = p.PkProductId,
            ProductName = p.Name,
            Price = p.Price
        }).ToList();

        _mockMapper.Setup(m => m.ToViewModels(It.IsAny<List<ProductModel>>()))
                  .Returns(expectedViewModels);

        // Act
        var result = await _productService.GetAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        _mockMapper.Verify(m => m.ToViewModels(It.IsAny<List<ProductModel>>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldReturnProduct()
    {
        // Arrange
        var product = new ProductModel
        {
            PkProductId = 1,
            Name = "Test Product",
            Price = 10.99m,
            Category = new CategoryModel { PkCategoryId = 1, CategoryName = "Test Category" }
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var expectedViewModel = new ProductVM
        {
            ProductId = product.PkProductId,
            ProductName = product.Name,
            Price = product.Price
        };

        _mockMapper.Setup(m => m.ToViewModel(It.IsAny<ProductModel>()))
                  .Returns(expectedViewModel);

        // Act
        var result = await _productService.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.ProductId.Should().Be(1);
        _mockMapper.Verify(m => m.ToViewModel(It.IsAny<ProductModel>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Act
        var result = await _productService.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
        _mockMapper.Verify(m => m.ToViewModel(It.IsAny<ProductModel>()), Times.Never);
    }

    [Fact]
    public async Task SearchNames_ShouldDelegateToSearchService()
    {
        // Arrange
        var searchQuery = "test";
        var expectedResults = new List<SearchResultDto>
        {
            new SearchResultDto { Id = 1, Name = "Test Product", Price = 19.99m }
        };

        _mockSearchService.Setup(s => s.SearchNames(searchQuery))
                         .ReturnsAsync(expectedResults);

        // Act
        var result = await _productService.SearchNames(searchQuery);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Test Product");
        _mockSearchService.Verify(s => s.SearchNames(searchQuery), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithValidProduct_ShouldCreateProduct()
    {
        // Arrange
        var productVM = new ProductVM
        {
            ProductName = "New Product",
            Description = "Test Description",
            Price = 25.99m,
            CategoryId = 1
        };

        // Act & Assert - since CreateAsync returns Task (void), we just verify no exceptions
        await _productService.CreateAsync(productVM);

        // Verify the product was added to the context
        var savedProduct = await _context.Products.FirstOrDefaultAsync(p => p.Name == "New Product");
        savedProduct.Should().NotBeNull();
        savedProduct!.Name.Should().Be("New Product");
        savedProduct.Price.Should().Be(25.99m);
    }

    [Fact]
    public async Task UpdateAsync_WithValidProduct_ShouldUpdateProduct()
    {
        // Arrange
        var existingProduct = new ProductModel
        {
            PkProductId = 1,
            Name = "Original Name",
            Description = "Original Description",
            Price = 10.99m
        };

        _context.Products.Add(existingProduct);
        await _context.SaveChangesAsync();

        var updatedVM = new ProductVM
        {
            ProductId = 1,
            ProductName = "Updated Name",
            Description = "Updated Description",
            Price = 15.99m
        };

        // Act
        await _productService.UpdateAsync(updatedVM);

        // Assert
        var updatedProduct = await _context.Products.FindAsync(1);
        updatedProduct!.Name.Should().Be("Updated Name");
        updatedProduct.Price.Should().Be(15.99m);
    }

    private void Dispose()
    {
        _context.Dispose();
    }
}