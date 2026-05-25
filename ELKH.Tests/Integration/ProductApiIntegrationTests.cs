using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Text.Json;
using Xunit;
using ELKH.Models.Api;
using ELKH.ViewModels;

namespace ELKH.Tests.Integration;

// TABLE OF CONTENTS
// - Product listing and pagination tests
// - Product lookup and availability tests
// - Search suggestions tests
// - Public and authenticated page access tests

/// <summary>
/// Integration tests for the product API endpoints and public page access.
/// </summary>
/// <remarks>
/// 1. Product listing and pagination tests
/// 2. Product lookup and availability tests
/// 3. Search suggestions tests
/// 4. Public and authenticated page access tests
/// </remarks>
[Collection("Integration")]
public class ProductApiIntegrationTests : IClassFixture<ELKHWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ELKHWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProductApiIntegrationTests(ELKHWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetProducts_ShouldReturnWrappedPagedProducts()
    {
        var response = await _client.GetAsync("/api/v1/ProductApi");

        response.EnsureSuccessStatusCode();
        var payload = await DeserializeAsync<ApiResponse<PagedResult<ProductApiModel>>>(response);

        payload.Should().NotBeNull();
        payload!.Success.Should().BeTrue();
        payload.Data.Should().NotBeNull();
        payload.Data!.Items.Should().NotBeNull();
        payload.Data.Page.Should().Be(1);
        payload.Data.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetProductById_WithInvalidId_ShouldReturnStructuredNotFound()
    {
        var response = await _client.GetAsync("/api/v1/ProductApi/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var payload = await DeserializeAsync<ApiErrorResponse>(response);
        payload.Should().NotBeNull();
        payload!.Success.Should().BeFalse();
        payload.ErrorCode.Should().Be("PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task GetProductById_WithValidId_ShouldReturnWrappedProduct()
    {
        var listResponse = await _client.GetAsync("/api/v1/ProductApi");
        listResponse.EnsureSuccessStatusCode();
        var listPayload = await DeserializeAsync<ApiResponse<PagedResult<ProductApiModel>>>(listResponse);

        var firstProduct = listPayload!.Data!.Items.FirstOrDefault();
        if (firstProduct is null)
        {
            return;
        }

        var response = await _client.GetAsync($"/api/v1/ProductApi/{firstProduct.Id}");

        response.EnsureSuccessStatusCode();
        var payload = await DeserializeAsync<ApiResponse<ProductApiModel>>(response);
        payload.Should().NotBeNull();
        payload!.Success.Should().BeTrue();
        payload.Data.Should().NotBeNull();
        payload.Data!.Id.Should().Be(firstProduct.Id);
    }

    [Fact]
    public async Task ProductAvailability_WithValidId_ShouldReturnWrappedAvailability()
    {
        var listResponse = await _client.GetAsync("/api/v1/ProductApi");
        listResponse.EnsureSuccessStatusCode();
        var listPayload = await DeserializeAsync<ApiResponse<PagedResult<ProductApiModel>>>(listResponse);

        var firstProduct = listPayload!.Data!.Items.FirstOrDefault();
        if (firstProduct is null)
        {
            return;
        }

        var response = await _client.GetAsync($"/api/v1/ProductApi/{firstProduct.Id}/availability");

        response.EnsureSuccessStatusCode();
        var payload = await DeserializeAsync<ApiResponse<ProductAvailabilityModel>>(response);
        payload.Should().NotBeNull();
        payload!.Success.Should().BeTrue();
        payload.Data.Should().NotBeNull();
        payload.Data!.ProductId.Should().Be(firstProduct.Id);
    }

    [Fact]
    public async Task SearchSuggestions_WithQuery_ShouldReturnWrappedSuggestions()
    {
        var response = await _client.GetAsync("/api/v1/ProductApi/search-suggestions?query=sticker");

        response.EnsureSuccessStatusCode();
        var payload = await DeserializeAsync<ApiResponse<List<string>>>(response);
        payload.Should().NotBeNull();
        payload!.Success.Should().BeTrue();
        payload.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchSuggestions_WithMissingQuery_ShouldReturnEmptySuggestionList()
    {
        var response = await _client.GetAsync("/api/v1/ProductApi/search-suggestions");

        response.EnsureSuccessStatusCode();
        var payload = await DeserializeAsync<ApiResponse<List<string>>>(response);
        payload.Should().NotBeNull();
        payload!.Success.Should().BeTrue();
        payload.Data.Should().NotBeNull();
        payload.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task ProductAvailability_WithInvalidId_ShouldReturnStructuredNotFound()
    {
        var response = await _client.GetAsync("/api/v1/ProductApi/999999/availability");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var payload = await DeserializeAsync<ApiErrorResponse>(response);
        payload.Should().NotBeNull();
        payload!.ErrorCode.Should().Be("PRODUCT_NOT_FOUND");
    }

    [Theory]
    [InlineData("/Product")]
    [InlineData("/Product/Details/1")]
    [InlineData("/Cart")]
    [InlineData("/Wishlist")]
    public async Task PublicPages_ShouldReturnSuccess(string url)
    {
        var response = await _client.GetAsync(url);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
    }

    [Theory]
    [InlineData("/Manager")]
    [InlineData("/User")]
    [InlineData("/Order")]
    public async Task AuthenticatedPages_WithoutAuth_ShouldReturnUnauthorizedOrRedirect_WithoutFollowingRedirects(string url)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await client.GetAsync(url);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Redirect);
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrWhiteSpace();
        return JsonSerializer.Deserialize<T>(content, JsonOptions);
    }
}
