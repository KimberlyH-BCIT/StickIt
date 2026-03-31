using NBomber.CSharp;
using NBomber.Http.CSharp;
using System.Net.Http;
using Xunit;
using FluentAssertions;

namespace ELKH.Tests.Performance;

/// <summary>
/// Performance tests for key ELKH application endpoints.
/// Tests response times and throughput under load.
/// </summary>
public class PerformanceTests
{
    [Fact]
    public void ProductListingPerformance_ShouldHandleNormalLoad()
    {
        var httpClient = new HttpClient();
        
        var scenario = Scenario.Create("product_listing_load", async context =>
        {
            var response = await httpClient.GetAsync("https://localhost:5001/Product");
            
            return response.IsSuccessStatusCode 
                ? Response.Ok() 
                : Response.Fail($"Status code: {response.StatusCode}");
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 10, during: TimeSpan.FromMinutes(1))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        // Assert performance metrics
        var scenarioStats = stats.AllScenarios.First(s => s.ScenarioName == "product_listing_load");
        scenarioStats.Ok.Request.Count.Should().BeGreaterThan(0);
        scenarioStats.Ok.Request.Mean.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(2)); // Response should be under 2 seconds
    }

    [Fact]
    public void ApiEndpointPerformance_ShouldMeetResponseTimeTargets()
    {
        var httpClient = new HttpClient();
        
        var scenario = Scenario.Create("api_performance", async context =>
        {
            var response = await httpClient.GetAsync("https://localhost:5001/api/v1/products");
            
            return response.IsSuccessStatusCode 
                ? Response.Ok() 
                : Response.Fail($"Status code: {response.StatusCode}");
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 20, during: TimeSpan.FromSeconds(30))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        // Assert API response times
        var scenarioStats = stats.AllScenarios.First(s => s.ScenarioName == "api_performance");
        scenarioStats.Ok.Request.Count.Should().BeGreaterThan(0);
        scenarioStats.Ok.Request.Mean.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(1)); // API should respond under 1 second
    }

    [Fact]
    public void DatabaseQueryPerformance_ShouldBeEfficient()
    {
        // This would typically test direct database operations
        // For this example, we'll test through the web layer
        
        var httpClient = new HttpClient();
        
        var scenario = Scenario.Create("db_query_performance", async context =>
        {
            // Test a page that requires database queries
            var response = await httpClient.GetAsync("https://localhost:5001/Product/Details/1");
            
            return response.IsSuccessStatusCode 
                ? Response.Ok() 
                : Response.Fail($"Status code: {response.StatusCode}");
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 15, during: TimeSpan.FromSeconds(20))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        // Assert database query performance
        var scenarioStats = stats.AllScenarios.First(s => s.ScenarioName == "db_query_performance");
        scenarioStats.Ok.Request.Count.Should().BeGreaterThan(0);
        
        // Database-backed pages should still be reasonably fast
        scenarioStats.Ok.Request.Mean.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(3));
    }

    [Theory]
    [InlineData("https://localhost:5001/")]
    [InlineData("https://localhost:5001/Product")]
    [InlineData("https://localhost:5001/api/v1/products")]
    public void EndpointAvailability_ShouldBeAccessible(string endpoint)
    {
        var httpClient = new HttpClient();
        
        var scenario = Scenario.Create($"availability_test_{endpoint.Replace(":", "").Replace("/", "_")}", async context =>
        {
            try
            {
                var response = await httpClient.GetAsync(endpoint);
                return response.IsSuccessStatusCode 
                    ? Response.Ok() 
                    : Response.Fail($"Status code: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                return Response.Fail($"Exception: {ex.Message}");
            }
        })
        .WithLoadSimulations(
            Simulation.KeepConstant(copies: 1, during: TimeSpan.FromSeconds(5))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        // Assert endpoint is available
        var scenarioStats = stats.AllScenarios.First();
        scenarioStats.Ok.Request.Count.Should().BeGreaterThan(0);
    }
}