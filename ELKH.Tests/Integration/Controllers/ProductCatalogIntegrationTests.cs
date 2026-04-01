using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ELKH.Data;
using ELKH.Models;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Tests.Integration.Controllers;

/// <summary>
/// Integration tests for Product catalog functionality.
/// Tests complete product browsing, searching, and viewing workflows.
/// </summary>
public class ProductCatalogIntegrationTests : IClassFixture<ELKHWebApplicationFactory>
{
    private readonly ELKHWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProductCatalogIntegrationTests(ELKHWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Product_Index_Should_Display_Products()
    {
        // Act
        var response = await _client.GetAsync("/Product");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        
        content.Should().Contain("Products");
        content.Should().Contain("Test Product 1"); // From seeded data
        content.Should().Contain("Test Product 2");
    }

    [Fact]
    public async Task Product_Search_Should_Return_Filtered_Results()
    {
        // Act
        var response = await _client.GetAsync("/Product?search=Test Product 1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        
        content.Should().Contain("Test Product 1");
        content.Should().Contain("Search results for"); // Search indicator
    }

    [Fact]
    public async Task Product_Details_Should_Show_Product_Information()
    {
        // Arrange - Get a product ID from the database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await db.Products.FirstAsync();

        // Act
        var response = await _client.GetAsync($"/Product/Details/{product.PkProductId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        
        content.Should().Contain(product.Name);
        content.Should().Contain(product.Description);
        content.Should().Contain(product.Price.ToString("C")); // Price formatting
    }

    [Fact]
    public async Task Product_Details_Nonexistent_Should_Return_NotFound()
    {
        // Act
        var response = await _client.GetAsync("/Product/Details/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("name_asc")]
    [InlineData("name_desc")]
    [InlineData("price_low")]
    [InlineData("price_high")]
    [InlineData("newest")]
    [InlineData("oldest")]
    public async Task Product_Sorting_Should_Work_For_All_Options(string sortOption)
    {
        // Act
        var response = await _client.GetAsync($"/Product?sort={sortOption}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Products");
    }

    [Fact]
    public async Task Product_Category_Filter_Should_Work()
    {
        // Arrange - Get a category ID from the database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = await db.Categories.FirstAsync();

        // Act
        var response = await _client.GetAsync($"/Product?categoryId={category.PkCategoryId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Products");
    }

    [Fact]
    public async Task Product_Pagination_Should_Work()
    {
        // Act
        var response = await _client.GetAsync("/Product?page=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Products");
    }

    [Fact]
    public async Task Product_Search_With_No_Results_Should_Show_Empty_Message()
    {
        // Act
        var response = await _client.GetAsync("/Product?search=NonexistentProduct12345");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("No products found");
    }

    [Fact]
    public async Task Product_JSON_Search_API_Should_Return_Valid_JSON()
    {
        // Act
        var response = await _client.GetAsync("/Product/SearchSuggestions?q=Test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        
        var json = await response.Content.ReadAsStringAsync();
        var suggestions = JsonSerializer.Deserialize<string[]>(json);
        suggestions.Should().NotBeNull();
    }

    [Fact]
    public async Task Product_Quick_View_Should_Return_Product_Data()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await db.Products.FirstAsync();

        // Act
        var response = await _client.GetAsync($"/Product/QuickView/{product.ProductId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(product.ProductName);
    }

    [Fact]
    public async Task Product_Related_Items_Should_Return_Similar_Products()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await db.Products.FirstAsync();

        // Act
        var response = await _client.GetAsync($"/Product/Related/{product.ProductId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Product_Availability_Check_Should_Return_Stock_Status()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await db.Products.FirstAsync();

        // Act
        var response = await _client.GetAsync($"/Product/CheckAvailability/{product.ProductId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Theory]
    [InlineData(1, 10)] // First page, 10 items
    [InlineData(1, 20)] // First page, 20 items
    [InlineData(2, 5)]  // Second page, 5 items
    public async Task Product_Pagination_With_Different_Page_Sizes_Should_Work(int page, int pageSize)
    {
        // Act
        var response = await _client.GetAsync($"/Product?page={page}&pageSize={pageSize}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Products");
    }
}