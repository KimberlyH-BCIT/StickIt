using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Diagnostics;
using Xunit;
using FluentAssertions;
using ELKH.Data;
using ELKH.Services;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Tests.Integration.Performance;

/// <summary>
/// Performance integration tests to ensure the application meets performance requirements.
/// These tests validate response times, memory usage, and throughput under various conditions.
/// </summary>
public class PerformanceIntegrationTests : IClassFixture<ELKHWebApplicationFactory>
{
    private readonly ELKHWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PerformanceIntegrationTests(ELKHWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Home_Page_Should_Load_Under_2_Seconds()
    {
        // Arrange
        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        var response = await _client.GetAsync("/");
        stopwatch.Stop();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000, "Home page should load in under 2 seconds");
    }

    [Fact]
    public async Task Product_Catalog_Should_Load_Under_3_Seconds()
    {
        // Arrange
        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        var response = await _client.GetAsync("/Product");
        stopwatch.Stop();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000, "Product catalog should load in under 3 seconds");
    }

    [Fact]
    public async Task Database_Queries_Should_Be_Optimized()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stopwatch = new Stopwatch();

        // Act & Assert - Test various database operations
        
        // Test 1: Product lookup by ID
        stopwatch.Start();
        var product = await db.Products.FirstOrDefaultAsync();
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500, "Product lookup should be under 500ms");

        // Test 2: Category listing
        stopwatch.Restart();
        var categories = await db.Categories.ToListAsync();
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(300, "Category listing should be under 300ms");

        // Test 3: Product search simulation
        stopwatch.Restart();
        var searchResults = await db.Products
            .Where(p => p.Name.Contains("Test"))
            .Take(10)
            .ToListAsync();
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Product search should be under 1 second");
    }

    [Fact]
    public async Task Concurrent_Requests_Should_Be_Handled_Efficiently()
    {
        // Arrange
        var tasks = new List<Task<TimeSpan>>();
        const int concurrentRequests = 10;

        // Act - Make concurrent requests
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(MeasureRequestTime("/"));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        var averageTime = results.Average(r => r.TotalMilliseconds);
        var maxTime = results.Max(r => r.TotalMilliseconds);

        averageTime.Should().BeLessThan(3000, "Average response time should be under 3 seconds");
        maxTime.Should().BeLessThan(5000, "Maximum response time should be under 5 seconds");
        results.All(r => r.TotalMilliseconds < 10000).Should().BeTrue("No request should take more than 10 seconds");
    }

    [Fact]
    public async Task Search_Service_Should_Perform_Efficiently()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var searchService = scope.ServiceProvider.GetRequiredService<ISearchService>();
        var stopwatch = new Stopwatch();

        // Act & Assert
        stopwatch.Start();
        var searchResults = await searchService.SearchNames("Test");
        stopwatch.Stop();

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Search service should return results in under 1 second");
        searchResults.Should().NotBeNull();
    }

    [Fact]
    public async Task Cache_Should_Improve_Performance()
    {
        // Arrange
        var userService = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IUserService>();
        const string testEmail = "customer@test.com";

        // Act - First call (should hit database)
        var stopwatch = Stopwatch.StartNew();
        var user1 = await userService.GetByEmailAsync(testEmail);
        var firstCallTime = stopwatch.ElapsedMilliseconds;

        // Second call (should hit cache)
        stopwatch.Restart();
        var user2 = await userService.GetByEmailAsync(testEmail);
        var secondCallTime = stopwatch.ElapsedMilliseconds;

        // Assert
        user1.Should().NotBeNull();
        user2.Should().NotBeNull();
        user1?.Email.Should().Be(user2?.Email);

        // Cache should make subsequent calls faster (allow some variance for test environment)
        if (firstCallTime > 50) // Only assert if first call took meaningful time
        {
            secondCallTime.Should().BeLessThan(firstCallTime, "Cached calls should be faster than database calls");
        }
    }

    [Fact]
    public async Task Memory_Usage_Should_Remain_Stable_Under_Load()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(true);
        
        // Act - Simulate load
        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(_client.GetAsync("/Product"));
        }

        await Task.WhenAll(tasks);

        // Force garbage collection and measure memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert
        // Memory increase should be reasonable (under 50MB for 50 requests)
        memoryIncrease.Should().BeLessThan(50 * 1024 * 1024, "Memory usage should not grow excessively under load");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Product")]
    [InlineData("/Identity/Account/Login")]
    [InlineData("/Identity/Account/Register")]
    public async Task Critical_Pages_Should_Meet_Performance_SLA(string url)
    {
        // Act
        var responseTime = await MeasureRequestTime(url);

        // Assert - Different SLAs for different page types
        var slaSeconds = url switch
        {
            "/" => 2.0, // Home page: 2 seconds
            "/Product" => 3.0, // Product catalog: 3 seconds
            "/Identity/Account/Login" => 2.0, // Login: 2 seconds
            "/Identity/Account/Register" => 2.0, // Register: 2 seconds
            _ => 5.0 // Default: 5 seconds
        };

        responseTime.TotalSeconds.Should().BeLessThan(slaSeconds, 
            $"Page {url} should load within {slaSeconds} seconds SLA");
    }

    [Fact]
    public async Task Health_Checks_Should_Respond_Quickly()
    {
        // Act
        var responseTime = await MeasureRequestTime("/health");

        // Assert
        responseTime.TotalMilliseconds.Should().BeLessThan(1000, "Health checks should respond in under 1 second");
    }

    [Fact]
    public async Task Static_Assets_Should_Load_Quickly()
    {
        // Arrange
        var staticAssets = new[]
        {
            "/css/site.css",
            "/js/site.js",
            "/lib/bootstrap/dist/css/bootstrap.min.css",
            "/lib/jquery/dist/jquery.min.js"
        };

        // Act & Assert
        foreach (var asset in staticAssets)
        {
            var responseTime = await MeasureRequestTime(asset);
            responseTime.TotalMilliseconds.Should().BeLessThan(1000, 
                $"Static asset {asset} should load in under 1 second");
        }
    }

    private async Task<TimeSpan> MeasureRequestTime(string url)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await _client.GetAsync(url);
        stopwatch.Stop();

        response.IsSuccessStatusCode.Should().BeTrue($"Request to {url} should succeed");
        return stopwatch.Elapsed;
    }
}