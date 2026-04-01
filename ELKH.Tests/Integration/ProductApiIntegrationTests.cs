using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ELKH.Tests.Integration;

/// <summary>
/// Integration tests for Product API endpoints.
/// Tests the full HTTP request/response cycle including authentication and authorization.
/// </summary>
public class ProductApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ProductApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetProducts_ShouldReturnSuccessAndProducts()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/products");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();

        var products = JsonSerializer.Deserialize<ProductVM[]>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        products.Should().NotBeNull();
    }

    [Fact]
    public async Task GetProductById_WithValidId_ShouldReturnProduct()
    {
        // First get a list of products to get a valid ID
        var listResponse = await _client.GetAsync("/api/v1/products");
        var listContent = await listResponse.Content.ReadAsStringAsync();
        var products = JsonSerializer.Deserialize<ProductVM[]>(listContent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (products != null && products.Length > 0)
        {
            // Act
            var response = await _client.GetAsync($"/api/v1/products/{products[0].ProductId}");

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var product = JsonSerializer.Deserialize<ProductVM>(content, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            product.Should().NotBeNull();
            product!.ProductId.Should().Be(products[0].ProductId);
        }
    }

    [Fact]
    public async Task GetProductById_WithInvalidId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/products/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SearchProducts_WithValidQuery_ShouldReturnResults()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/products/search?q=test");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var searchResults = JsonSerializer.Deserialize<SearchResultDto[]>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        searchResults.Should().NotBeNull();
    }

    [Fact]
    public async Task GetProductCategories_ShouldReturnCategories()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/products/categories");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("/Product")]
    [InlineData("/Product/Details/1")]
    [InlineData("/Cart")]
    [InlineData("/Wishlist")]
    public async Task PublicPages_ShouldReturnSuccess(string url)
    {
        // Act
        var response = await _client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
    }

    [Theory]
    [InlineData("/Manager")]
    [InlineData("/User")]
    [InlineData("/Order")]
    public async Task AuthenticatedPages_WithoutAuth_ShouldReturnUnauthorizedOrRedirect(string url)
    {
        // Act
        var response = await _client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Redirect);
    }
}