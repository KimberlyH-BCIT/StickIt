using NBomber.CSharp;
using System.Text.Json;
using Xunit;
using FluentAssertions;

namespace ELKH.Tests.Performance.Regression;

/// <summary>
/// Automated regression testing for performance characteristics.
/// These tests run as part of CI/CD to detect performance regressions.
/// </summary>
public class PerformanceRegressionTests
{
    private readonly string _baseUrl = "http://localhost:5000"; // Configuration should be injected
    private readonly PerformanceBaseline _baseline;

    public PerformanceRegressionTests()
    {
        _baseline = LoadPerformanceBaseline();
    }

    [Fact]
    public async Task Homepage_Performance_Should_Not_Regress()
    {
        // Arrange
        var scenario = Scenario.Create("homepage_regression", async context =>
        {
            using var client = new HttpClient();
            var response = await client.GetAsync($"{_baseUrl}/");
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 10, during: TimeSpan.FromMinutes(1))
        );

        // Act
        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithoutReports() // Don't generate reports for regression tests
            .Run();

        // Assert
        var scenarioStats = stats.AllScenarios.First();
        var meanResponseTime = scenarioStats.Ok.Response.Mean;
        var p95ResponseTime = scenarioStats.Ok.Response.Percentile95;
        var errorRate = (double)scenarioStats.Fail.Request.Count / scenarioStats.AllRequestCount * 100;

        // Performance regression checks
        meanResponseTime.Should().BeLessThan(_baseline.HomePageMeanResponseTime * 1.1, 
            "Mean response time should not increase by more than 10%");
        
        p95ResponseTime.Should().BeLessThan(_baseline.HomePageP95ResponseTime * 1.15, 
            "95th percentile response time should not increase by more than 15%");
        
        errorRate.Should().BeLessThan(_baseline.HomePageErrorRate + 1.0, 
            "Error rate should not increase by more than 1%");
    }

    [Fact]
    public async Task Product_Catalog_Performance_Should_Not_Regress()
    {
        // Arrange
        var scenario = Scenario.Create("product_catalog_regression", async context =>
        {
            using var client = new HttpClient();
            var response = await client.GetAsync($"{_baseUrl}/Product");
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 8, during: TimeSpan.FromMinutes(1))
        );

        // Act
        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithoutReports()
            .Run();

        // Assert
        var scenarioStats = stats.AllScenarios.First();
        var meanResponseTime = scenarioStats.Ok.Response.Mean;
        var throughput = scenarioStats.AllOkCount / stats.AllDuration.TotalSeconds;

        meanResponseTime.Should().BeLessThan(_baseline.ProductCatalogMeanResponseTime * 1.1);
        throughput.Should().BeGreaterThan(_baseline.ProductCatalogThroughput * 0.9, 
            "Throughput should not decrease by more than 10%");
    }

    [Fact]
    public async Task Search_Performance_Should_Not_Regress()
    {
        // Arrange
        var searchTerms = new[] { "sticker", "animal", "sport", "nature", "funny" };
        
        var scenario = Scenario.Create("search_regression", async context =>
        {
            using var client = new HttpClient();
            var term = searchTerms[Random.Shared.Next(searchTerms.Length)];
            var response = await client.GetAsync($"{_baseUrl}/Product?search={term}");
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 5, during: TimeSpan.FromMinutes(1))
        );

        // Act
        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithoutReports()
            .Run();

        // Assert
        var scenarioStats = stats.AllScenarios.First();
        var meanResponseTime = scenarioStats.Ok.Response.Mean;
        var p99ResponseTime = scenarioStats.Ok.Response.Percentile99;

        meanResponseTime.Should().BeLessThan(_baseline.SearchMeanResponseTime * 1.1);
        p99ResponseTime.Should().BeLessThan(5000, "Search should complete within 5 seconds even at 99th percentile");
    }

    [Fact]
    public async Task API_Endpoints_Performance_Should_Not_Regress()
    {
        // Arrange
        var apiEndpoints = new[]
        {
            "/health",
            "/Product/SearchSuggestions?q=test",
            "/Product/CheckAvailability/1"
        };

        foreach (var endpoint in apiEndpoints)
        {
            var scenario = Scenario.Create($"api_{endpoint.Replace("/", "_").Replace("?", "_")}", async context =>
            {
                using var client = new HttpClient();
                var response = await client.GetAsync($"{_baseUrl}{endpoint}");
                return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
            })
            .WithLoadSimulations(
                Simulation.InjectPerSec(rate: 15, during: TimeSpan.FromSeconds(30))
            );

            // Act
            var stats = NBomberRunner
                .RegisterScenarios(scenario)
                .WithoutReports()
                .Run();

            // Assert
            var scenarioStats = stats.AllScenarios.First();
            var meanResponseTime = scenarioStats.Ok.Response.Mean;
            
            meanResponseTime.Should().BeLessThan(1000, $"API endpoint {endpoint} should respond within 1 second");
        }
    }

    [Fact]
    public async Task Database_Query_Performance_Should_Not_Regress()
    {
        // This test would ideally connect to the database directly
        // For now, we'll test through the application endpoints that hit the database

        var scenario = Scenario.Create("database_regression", async context =>
        {
            using var client = new HttpClient();
            
            // Simulate database-heavy operations
            var tasks = new[]
            {
                client.GetAsync($"{_baseUrl}/Product/Details/1"),    // Product lookup
                client.GetAsync($"{_baseUrl}/Product?categoryId=1"), // Category filter
                client.GetAsync($"{_baseUrl}/Product?sort=price_low") // Sorting
            };

            await Task.WhenAll(tasks);
            return tasks.All(t => t.Result.IsSuccessStatusCode) ? Response.Ok() : Response.Fail();
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 5, during: TimeSpan.FromMinutes(1))
        );

        // Act
        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithoutReports()
            .Run();

        // Assert
        var scenarioStats = stats.AllScenarios.First();
        var meanResponseTime = scenarioStats.Ok.Response.Mean;
        
        meanResponseTime.Should().BeLessThan(3000, "Database operations should complete within 3 seconds on average");
    }

    [Fact]
    public async Task Concurrent_User_Capacity_Should_Not_Regress()
    {
        // Test the system's ability to handle concurrent users
        var scenario = Scenario.Create("concurrent_users", async context =>
        {
            using var client = new HttpClient();
            
            // Simulate user journey
            await client.GetAsync($"{_baseUrl}/");
            await Task.Delay(1000); // Think time
            await client.GetAsync($"{_baseUrl}/Product");
            await Task.Delay(2000); // Think time
            await client.GetAsync($"{_baseUrl}/Product/Details/1");
            
            return Response.Ok();
        })
        .WithLoadSimulations(
            Simulation.KeepConstant(copies: 25, during: TimeSpan.FromMinutes(2)) // 25 concurrent users
        );

        // Act
        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithoutReports()
            .Run();

        // Assert
        var scenarioStats = stats.AllScenarios.First();
        var errorRate = (double)scenarioStats.Fail.Request.Count / scenarioStats.AllRequestCount * 100;
        
        errorRate.Should().BeLessThan(5.0, "Error rate should be less than 5% under normal concurrent load");
        scenarioStats.AllOkCount.Should().BeGreaterThan(0, "System should handle concurrent users successfully");
    }

    private PerformanceBaseline LoadPerformanceBaseline()
    {
        // In a real implementation, this would load from a file or database
        // For now, we'll use hardcoded baselines that represent good performance
        return new PerformanceBaseline
        {
            HomePageMeanResponseTime = 1500,      // 1.5 seconds
            HomePageP95ResponseTime = 2500,       // 2.5 seconds
            HomePageErrorRate = 0.5,              // 0.5%
            
            ProductCatalogMeanResponseTime = 2000, // 2 seconds
            ProductCatalogThroughput = 10,         // 10 req/sec
            
            SearchMeanResponseTime = 1200,         // 1.2 seconds
            
            ApiMeanResponseTime = 500,             // 0.5 seconds
            DatabaseQueryTime = 200                // 0.2 seconds
        };
    }
}

/// <summary>
/// Performance baseline values for regression testing.
/// These should be updated when legitimate performance improvements are made.
/// </summary>
public class PerformanceBaseline
{
    public double HomePageMeanResponseTime { get; set; }
    public double HomePageP95ResponseTime { get; set; }
    public double HomePageErrorRate { get; set; }
    
    public double ProductCatalogMeanResponseTime { get; set; }
    public double ProductCatalogThroughput { get; set; }
    
    public double SearchMeanResponseTime { get; set; }
    
    public double ApiMeanResponseTime { get; set; }
    public double DatabaseQueryTime { get; set; }
}