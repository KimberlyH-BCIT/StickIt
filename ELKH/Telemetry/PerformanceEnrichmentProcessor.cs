using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace ELKH.Telemetry;

/// <summary>
/// Application Insights telemetry processor to enrich telemetry data
/// with additional performance context and business metrics.
/// </summary>
public class PerformanceEnrichmentProcessor : ITelemetryProcessor
{
    private readonly ITelemetryProcessor _next;
    
    public PerformanceEnrichmentProcessor(ITelemetryProcessor next)
    {
        _next = next;
    }

    public void Process(ITelemetry item)
    {
        switch (item)
        {
            case RequestTelemetry request:
                EnrichRequestTelemetry(request);
                break;
            
            case DependencyTelemetry dependency:
                EnrichDependencyTelemetry(dependency);
                break;
            
            case EventTelemetry eventTelemetry:
                EnrichEventTelemetry(eventTelemetry);
                break;
        }

        _next.Process(item);
    }

    private void EnrichRequestTelemetry(RequestTelemetry request)
    {
        // Add business context
        request.Properties["Environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown";
        request.Properties["MachineName"] = Environment.MachineName;
        
        // Categorize requests by type
        if (request.Url != null)
        {
            var path = request.Url.AbsolutePath.ToLowerInvariant();

            request.Properties["RequestCategory"] = path switch
            {
                string p when p.Contains("/api/") => "API",
                string p when p.Contains("/admin") => "Admin",
                string p when p.Contains("/identity") => "Authentication",
                string p when p.Contains("/product") => "Catalog",
                string p when p.Contains("/cart") => "Commerce",
                string p when p.Contains("/order") => "Orders",
                string p when p.StartsWith("/health") => "Health",
                string p when p.Contains(".css") || p.Contains(".js") || p.Contains(".png") => "Static",
                _ => "General"
            };

            // Track business-critical operations
            if (path.Contains("/checkout") || path.Contains("/payment"))
            {
                request.Properties["BusinessCritical"] = "true";
            }

            // Performance tier classification
            request.Properties["PerformanceTier"] = request.Duration.TotalMilliseconds switch
            {
                < 100 => "Excellent",
                < 300 => "Good",
                < 1000 => "Fair",
                < 3000 => "Poor",
                _ => "Critical"
            };
        }

        // Add user context if available
        if (!string.IsNullOrEmpty(request.Context.User.Id))
        {
            request.Properties["HasUserContext"] = "true";
        }

        // Response size category
        if (request.Properties.ContainsKey("ResponseSize"))
        {
            if (int.TryParse(request.Properties["ResponseSize"], out var size))
            {
                request.Properties["ResponseSizeCategory"] = size switch
                {
                    < 1024 => "Small",
                    < 10240 => "Medium", 
                    < 102400 => "Large",
                    _ => "XLarge"
                };
            }
        }
    }

    private void EnrichDependencyTelemetry(DependencyTelemetry dependency)
    {
        // Categorize dependencies
        dependency.Properties["DependencyCategory"] = dependency.Type?.ToLowerInvariant() switch
        {
            "sql" => "Database",
            "http" => "WebService",
            "redis" => "Cache",
            "azure blob" => "Storage",
            _ => "Other"
        };

        // Performance classification for dependencies
        dependency.Properties["PerformanceTier"] = dependency.Duration.TotalMilliseconds switch
        {
            < 50 => "Excellent",
            < 150 => "Good", 
            < 500 => "Fair",
            < 2000 => "Poor",
            _ => "Critical"
        };

        // Mark slow database queries
        if (dependency.Type?.Equals("sql", StringComparison.OrdinalIgnoreCase) == true &&
            dependency.Duration.TotalMilliseconds > 1000)
        {
            dependency.Properties["SlowQuery"] = "true";
        }

        // Mark external service calls
        if (dependency.Type?.Equals("http", StringComparison.OrdinalIgnoreCase) == true)
        {
            dependency.Properties["ExternalCall"] = "true";
            
            // Identify critical external services
            if (dependency.Target?.Contains("paypal") == true ||
                dependency.Target?.Contains("stripe") == true ||
                dependency.Target?.Contains("sendgrid") == true)
            {
                dependency.Properties["CriticalExternal"] = "true";
            }
        }
    }

    private void EnrichEventTelemetry(EventTelemetry eventTelemetry)
    {
        // Add environment context to custom events
        eventTelemetry.Properties["Environment"] = 
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown";
        
        // Categorize events by business domain
        var eventName = eventTelemetry.Name?.ToLowerInvariant() ?? "";
        
        eventTelemetry.Properties["EventCategory"] = eventName switch
        {
            var e when e.Contains("user") || e.Contains("login") || e.Contains("register") => "User",
            var e when e.Contains("product") || e.Contains("catalog") => "Catalog",
            var e when e.Contains("order") || e.Contains("checkout") || e.Contains("payment") => "Commerce",
            var e when e.Contains("search") => "Search",
            var e when e.Contains("cart") => "ShoppingCart",
            var e when e.Contains("error") || e.Contains("exception") => "Error",
            var e when e.Contains("performance") || e.Contains("slow") => "Performance",
            _ => "General"
        };

        // Mark business-critical events
        if (eventName.Contains("payment") || eventName.Contains("checkout") || 
            eventName.Contains("order") || eventName.Contains("critical"))
        {
            eventTelemetry.Properties["BusinessCritical"] = "true";
        }

        // Add timestamp for event correlation
        eventTelemetry.Properties["ProcessedAt"] = DateTimeOffset.UtcNow.ToString("o");
    }
}