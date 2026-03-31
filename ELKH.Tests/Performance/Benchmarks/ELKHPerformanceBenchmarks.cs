using NBomber.CSharp;
using NBomber.Http.CSharp;
using System.Text.Json;

namespace ELKH.Tests.Performance.Benchmarks;

/// <summary>
/// NBomber performance benchmarks for ELKH eCommerce platform.
/// These tests simulate real-world load scenarios and measure application performance.
/// </summary>
public class ELKHPerformanceBenchmarks
{
    private const string BaseUrl = "http://localhost:5000"; // Update with your actual base URL
    private static readonly HttpClient HttpClient = new HttpClient();

    public static void Main(string[] args)
    {
        // Parse command line arguments for configuration
        var testType = args.Length > 0 ? args[0] : "all";
        var duration = TimeSpan.FromMinutes(args.Length > 1 && int.TryParse(args[1], out var min) ? min : 5);

        Console.WriteLine($"🚀 Starting ELKH Performance Benchmarks");
        Console.WriteLine($"📊 Test Type: {testType}");
        Console.WriteLine($"⏱️ Duration: {duration.TotalMinutes} minutes");
        Console.WriteLine($"🎯 Target: {BaseUrl}");
        Console.WriteLine();

        switch (testType.ToLower())
        {
            case "load":
                RunLoadTest(duration);
                break;
            case "stress":
                RunStressTest(duration);
                break;
            case "spike":
                RunSpikeTest(duration);
                break;
            case "soak":
                RunSoakTest(duration);
                break;
            case "api":
                RunApiPerformanceTest(duration);
                break;
            case "all":
            default:
                RunAllTests();
                break;
        }

        Console.WriteLine("✅ All performance tests completed!");
    }

    /// <summary>
    /// Load test - Normal expected load simulation
    /// </summary>
    private static void RunLoadTest(TimeSpan duration)
    {
        Console.WriteLine("🔄 Running Load Test - Normal User Traffic");

        var scenario = Scenario.Create("load_test", async context =>
        {
            // Simulate user browsing behavior
            using var client = new HttpClient();

            // Step 1: Visit homepage
            var homeResponse = await client.GetAsync($"{BaseUrl}/");
            
            // Step 2: Browse products
            var productsResponse = await client.GetAsync($"{BaseUrl}/Product");
            
            // Step 3: Search for products
            var searchResponse = await client.GetAsync($"{BaseUrl}/Product?search=sticker");
            
            // Step 4: View product details (simulate random product)
            var detailsResponse = await client.GetAsync($"{BaseUrl}/Product/Details/1");

            return Response.Ok();
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 10, during: duration) // 10 users per second
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFolder("reports")
            .WithReportFormats(ReportFormat.Html, ReportFormat.Csv, ReportFormat.Txt)
            .Run();
    }

    /// <summary>
    /// Stress test - Beyond normal capacity
    /// </summary>
    private static void RunStressTest(TimeSpan duration)
    {
        Console.WriteLine("💥 Running Stress Test - High Load Scenarios");

        var scenario = Scenario.Create("stress_test", async context =>
        {
            using var client = new HttpClient();

            // Intensive operations
            var tasks = new[]
            {
                client.GetAsync($"{BaseUrl}/"),
                client.GetAsync($"{BaseUrl}/Product"),
                client.GetAsync($"{BaseUrl}/Product?search=test"),
                client.GetAsync($"{BaseUrl}/health")
            };

            await Task.WhenAll(tasks);
            return Response.Ok();
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 50, during: duration) // 50 users per second
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFolder("reports")
            .WithReportFormats(ReportFormat.Html, ReportFormat.Csv)
            .Run();
    }

    /// <summary>
    /// Spike test - Sudden traffic spikes
    /// </summary>
    private static void RunSpikeTest(TimeSpan duration)
    {
        Console.WriteLine("⚡ Running Spike Test - Traffic Spikes");

        var scenario = Scenario.Create("spike_test", async context =>
        {
            using var client = new HttpClient();
            var response = await client.GetAsync($"{BaseUrl}/");
            return Response.Ok();
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 5, during: TimeSpan.FromMinutes(1)),   // Normal load
            Simulation.InjectPerSec(rate: 100, during: TimeSpan.FromMinutes(2)), // Spike
            Simulation.InjectPerSec(rate: 5, during: TimeSpan.FromMinutes(1))    // Back to normal
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFolder("reports")
            .WithReportFormats(ReportFormat.Html, ReportFormat.Csv)
            .Run();
    }

    /// <summary>
    /// Soak test - Extended duration testing
    /// </summary>
    private static void RunSoakTest(TimeSpan duration)
    {
        Console.WriteLine("🏃‍♂️ Running Soak Test - Extended Load");

        var scenario = Scenario.Create("soak_test", async context =>
        {
            using var client = new HttpClient();

            // Realistic user journey
            await client.GetAsync($"{BaseUrl}/");
            await Task.Delay(TimeSpan.FromSeconds(2)); // Think time
            await client.GetAsync($"{BaseUrl}/Product");
            await Task.Delay(TimeSpan.FromSeconds(3)); // Think time
            await client.GetAsync($"{BaseUrl}/Product?search=animal");

            return Response.Ok();
        })
        .WithLoadSimulations(
            Simulation.KeepConstant(copies: 10, during: duration) // 10 concurrent users
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFolder("reports")
            .WithReportFormats(ReportFormat.Html, ReportFormat.Csv)
            .Run();
    }

    /// <summary>
    /// API-specific performance test
    /// </summary>
    private static void RunApiPerformanceTest(TimeSpan duration)
    {
        Console.WriteLine("🔌 Running API Performance Test");

        var healthCheckScenario = Scenario.Create("health_check_api", async context =>
        {
            using var client = new HttpClient();
            var response = await client.GetAsync($"{BaseUrl}/health");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var healthData = JsonSerializer.Deserialize<JsonElement>(content);
                var status = healthData.GetProperty("status").GetString();
                
                return status == "Healthy" ? Response.Ok() : Response.Fail("Unhealthy");
            }
            
            return Response.Fail($"HTTP {response.StatusCode}");
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 20, during: duration)
        );

        var searchApiScenario = Scenario.Create("search_api", async context =>
        {
            using var client = new HttpClient();
            var searchTerms = new[] { "sticker", "animal", "sport", "nature", "funny" };
            var term = searchTerms[Random.Shared.Next(searchTerms.Length)];
            
            var response = await client.GetAsync($"{BaseUrl}/Product/SearchSuggestions?q={term}");
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail($"HTTP {response.StatusCode}");
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 15, during: duration)
        );

        NBomberRunner
            .RegisterScenarios(healthCheckScenario, searchApiScenario)
            .WithReportFolder("reports")
            .WithReportFormats(ReportFormat.Html, ReportFormat.Csv)
            .Run();
    }

    /// <summary>
    /// Run comprehensive test suite
    /// </summary>
    private static void RunAllTests()
    {
        var testDuration = TimeSpan.FromMinutes(2); // Shorter duration for full suite

        Console.WriteLine("🎯 Running Comprehensive Performance Test Suite");
        Console.WriteLine();

        // Sequential execution of different test types
        RunLoadTest(testDuration);
        System.Threading.Thread.Sleep(30000); // 30 second cooldown

        RunStressTest(testDuration);
        System.Threading.Thread.Sleep(30000);

        RunApiPerformanceTest(testDuration);

        GeneratePerformanceReport();
    }

    /// <summary>
    /// Generate summary performance report
    /// </summary>
    private static void GeneratePerformanceReport()
    {
        Console.WriteLine();
        Console.WriteLine("📊 Performance Test Summary");
        Console.WriteLine("==========================");
        Console.WriteLine("✅ Load Test - Completed");
        Console.WriteLine("✅ Stress Test - Completed");
        Console.WriteLine("✅ API Performance Test - Completed");
        Console.WriteLine();
        Console.WriteLine("📁 Detailed reports saved to: ./reports/");
        Console.WriteLine("🌐 Open the HTML report for detailed analysis");
        Console.WriteLine();
        Console.WriteLine("🎯 Performance Targets:");
        Console.WriteLine("   • Response Time: < 2s (95th percentile)");
        Console.WriteLine("   • Throughput: > 100 req/sec");
        Console.WriteLine("   • Error Rate: < 1%");
        Console.WriteLine("   • Availability: > 99.9%");
    }
}