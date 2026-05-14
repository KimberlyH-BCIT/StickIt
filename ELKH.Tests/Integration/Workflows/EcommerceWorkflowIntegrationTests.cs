using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ELKH.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELKH.Tests.Integration.Workflows;

/// <summary>
/// End-to-end workflow integration tests that test complete user journeys.
/// These tests simulate real user scenarios from browsing to purchase.
/// </summary>
public class EcommerceWorkflowIntegrationTests : IClassFixture<ELKHWebApplicationFactory>
{
    private readonly ELKHWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public EcommerceWorkflowIntegrationTests(ELKHWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Complete_Shopping_Workflow_Anonymous_User_Should_Be_Redirected_To_Login()
    {
        // Step 1: Browse products
        var productResponse = await _client.GetAsync("/Product");
        productResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 2: View product details
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await db.Products.FirstAsync();
        
        var detailResponse = await _client.GetAsync($"/Product/Details/{product.PkProductId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 3: Try to add to cart (should redirect to login)
        var cartData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("itemId", product.PkProductId.ToString()),
            new KeyValuePair<string, string>("quantity", "1")
        });

        var addToCartResponse = await _client.PostAsync("/Cart/AddToCart", cartData);
        addToCartResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        addToCartResponse.Headers.Location?.ToString().Should().Contain("Identity/Account/Login");
    }

    [Fact]
    public async Task Product_Search_And_Filter_Workflow_Should_Work()
    {
        // Step 1: Search for products
        var searchResponse = await _client.GetAsync("/Product?search=Test");
        searchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var searchContent = await searchResponse.Content.ReadAsStringAsync();
        searchContent.Should().Contain("Search results for");

        // Step 2: Apply category filter
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = await db.Categories.FirstAsync();

        var filterResponse = await _client.GetAsync($"/Product?categoryId={category.PkCategoryId}");
        filterResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 3: Apply sorting
        var sortResponse = await _client.GetAsync("/Product?sort=price_low");
        sortResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 4: Clear filters
        var clearResponse = await _client.GetAsync("/Product");
        clearResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Product_Quick_Actions_Workflow_Should_Handle_Authentication()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await db.Products.FirstAsync();

        // Step 1: Try wishlist action (should redirect to login)
        var wishlistData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("productId", product.PkProductId.ToString())
        });

        var wishlistResponse = await _client.PostAsync("/Wishlist/AddAjax", wishlistData);
        wishlistResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        wishlistResponse.Headers.Location?.ToString().Should().Contain("Identity/Account/Login");

        // Step 2: Try stock notification (should redirect to login)
        var notifyData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("productId", product.PkProductId.ToString())
        });

        var notifyResponse = await _client.PostAsync("/Product/NotifyStock", notifyData);
        notifyResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        notifyResponse.Headers.Location?.ToString().Should().Contain("Identity/Account/Login");
    }

    [Fact]
    public async Task Admin_Area_Access_Should_Be_Properly_Protected()
    {
        // Step 1: Try to access admin dashboard
        var adminResponse = await _client.GetAsync("/Admin");
        adminResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        adminResponse.Headers.Location?.ToString().Should().Contain("Identity/Account/Login");

        // Step 2: Try to access admin user management
        var userMgmtResponse = await _client.GetAsync("/AdminRole");
        userMgmtResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Step 3: Try to access admin system functions
        var systemResponse = await _client.GetAsync("/AdminSystem");
        systemResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Step 4: Try to access manager functions
        var managerResponse = await _client.GetAsync("/Manager");
        managerResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Health_And_Monitoring_Endpoints_Should_Be_Accessible()
    {
        // Step 1: Check main health endpoint
        var healthResponse = await _client.GetAsync("/health");
        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var healthContent = await healthResponse.Content.ReadAsStringAsync();
        var healthData = JsonSerializer.Deserialize<JsonElement>(healthContent);
        healthData.GetProperty("status").GetString().Should().Be("Healthy");

        // Step 2: Check if individual health checks are reported
        healthData.GetProperty("results").ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task Error_Handling_Workflow_Should_Show_Custom_Error_Pages()
    {
        // Step 1: Try to access nonexistent product
        var notFoundResponse = await _client.GetAsync("/Product/Details/99999");
        notFoundResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Step 2: Try to access nonexistent route
        var badRouteResponse = await _client.GetAsync("/NonexistentController/NonexistentAction");
        badRouteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Performance_Critical_Pages_Should_Load_Quickly()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Test critical pages for performance
        var pages = new[] { "/", "/Product", "/Identity/Account/Login", "/Identity/Account/Register" };

        foreach (var page in pages)
        {
            stopwatch.Restart();
            var response = await _client.GetAsync(page);
            stopwatch.Stop();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, $"Page {page} should load in under 5 seconds");
        }
    }

    [Fact]
    public async Task Static_Assets_Should_Be_Accessible()
    {
        // Step 1: Check CSS files
        var cssResponse = await _client.GetAsync("/css/site.css");
        cssResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        cssResponse.Content.Headers.ContentType?.MediaType.Should().Be("text/css");

        // Step 2: Check JavaScript files
        var jsResponse = await _client.GetAsync("/js/site.js");
        jsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        jsResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/javascript");

        // Step 3: Check if Bootstrap is accessible
        var bootstrapResponse = await _client.GetAsync("/lib/bootstrap/dist/css/bootstrap.min.css");
        bootstrapResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("POST", "/Cart/AddToCart")]
    [InlineData("POST", "/Wishlist/Add")]
    [InlineData("POST", "/Order/Create")]
    [InlineData("POST", "/Product/Rate")]
    public async Task CSRF_Protection_Should_Block_Requests_Without_Token(string method, string endpoint)
    {
        var formData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("test", "value")
        });

        HttpResponseMessage response;
        if (method == "POST")
        {
            response = await _client.PostAsync(endpoint, formData);
        }
        else
        {
            throw new ArgumentException($"Method {method} not implemented in test");
        }

        // Should either redirect to login or return 400/401/403
        (response.StatusCode == HttpStatusCode.Redirect ||
         response.StatusCode == HttpStatusCode.BadRequest ||
         response.StatusCode == HttpStatusCode.Unauthorized ||
         response.StatusCode == HttpStatusCode.Forbidden).Should().BeTrue(
            $"Endpoint {endpoint} should be protected against CSRF attacks");
    }
}