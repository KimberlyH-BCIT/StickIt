using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace ELKH.Telemetry;

// ╔═══════════════════════════════════════════════════════════════════════════════════════════════╗
// ║                   PERFORMANCE ENRICHMENT PROCESSOR - TABLE OF CONTENTS                       ║
// ╚═══════════════════════════════════════════════════════════════════════════════════════════════╝
// 
// OVERVIEW:
// Application Insights telemetry processor to enrich telemetry data with additional performance
// context and business metrics for comprehensive monitoring and alerting capabilities.
// 
// TABLE OF CONTENTS:
// ┌─ Section 1: Processor Setup & Pipeline Integration ................................... Line 49
// │  ├─ Constructor with pipeline integration
// │  ├─ ITelemetryProcessor interface implementation
// │  ├─ Next processor chain management
// │  └─ Telemetry type dispatching logic
// ├─ Section 2: Request Telemetry Enrichment ........................................... Line 59
// │  ├─ EnrichRequestTelemetry() - HTTP request context enhancement
// │  ├─ Environment and machine context addition
// │  ├─ Request categorization (API, Admin, Authentication, etc.)
// │  ├─ Business-critical operation identification
// │  ├─ Performance tier classification based on duration
// │  ├─ User context detection and tracking
// │  └─ Response size categorization for payload analysis
// ├─ Section 3: Dependency Telemetry Enrichment ....................................... Line 103
// │  ├─ EnrichDependencyTelemetry() - External dependency context
// │  ├─ Dependency categorization (Database, WebService, Cache, Storage)
// │  ├─ Performance tier classification for dependencies
// │  ├─ Slow query detection and flagging
// │  ├─ External service call identification
// │  └─ Critical external service monitoring (PayPal, Stripe, SendGrid)
// └─ Section 4: Event Telemetry Enrichment ............................................ Line 147
//    ├─ EnrichEventTelemetry() - Custom event context enhancement
//    ├─ Environment context addition for events
//    ├─ Business domain categorization (User, Catalog, Commerce, etc.)
//    ├─ Business-critical event identification
//    ├─ Event correlation timestamp addition
//    └─ Custom event classification for alerting
//
// ARCHITECTURE NOTES:
// • Pipeline processor pattern for telemetry enrichment
// • Strategy pattern for different telemetry types (Request, Dependency, Event)
// • Comprehensive business context addition for actionable insights
// • Performance classification for SLA monitoring and alerting
// • Chain of responsibility pattern with next processor invocation
//
// BUSINESS INTELLIGENCE FEATURES:
// • Request categorization for business area monitoring
// • Performance tier classification for SLA tracking
// • Business-critical operation flagging for priority alerting
// • External service dependency monitoring
// • Custom event categorization for business metrics
//
// PERFORMANCE MONITORING:
// • Duration-based performance classification (Excellent < 100ms, Good < 300ms, etc.)
// • Slow query detection for database optimization
// • Response size monitoring for bandwidth optimization
// • External service performance tracking
// • Critical path identification for business operations
//
// ALERTING & MONITORING:
// • Business-critical operation flagging for immediate alerts
// • Performance tier degradation monitoring
// • External service failure detection
// • Slow query identification for optimization
// • Error categorization for incident response
//
// SECURITY & COMPLIANCE:
// • User context tracking without PII exposure
// • Environment-specific monitoring for compliance
// • Audit trail enhancement for business operations
// • External service monitoring for security compliance
// • Error tracking for security incident detection

/// <summary>
/// Application Insights telemetry processor to enrich telemetry data
/// with additional performance context and business metrics.
/// </summary>
/// <remarks>
/// <para><strong>Telemetry Enhancement Strategy:</strong></para>
/// This processor enriches three types of telemetry:
/// <list type="bullet">
/// <item><strong>RequestTelemetry</strong>: HTTP requests with business context and performance classification</item>
/// <item><strong>DependencyTelemetry</strong>: External dependencies with performance and criticality analysis</item>
/// <item><strong>EventTelemetry</strong>: Custom events with business domain categorization</item>
/// </list>
/// 
/// <para><strong>Performance Classification:</strong></para>
/// Operations are classified into performance tiers (Excellent, Good, Fair, Poor, Critical)
/// based on duration thresholds to enable SLA monitoring and performance alerting.
/// 
/// <para><strong>Business Intelligence:</strong></para>
/// Telemetry is enriched with business context including request categories, critical operations,
/// and external service dependencies to provide actionable insights for business operations.
/// </remarks>
public class PerformanceEnrichmentProcessor : ITelemetryProcessor
{
    #region Section 1: Processor Setup & Pipeline Integration

    // ═══════════════════════════════════════════════════════════════════
    // Section 1: Processor Setup & Pipeline Integration
    // ═══════════════════════════════════════════════════════════════════

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

    #endregion

    #region Section 2: Request Telemetry Enrichment

    // ═══════════════════════════════════════════════════════════════════
    // Section 2: Request Telemetry Enrichment
    // ═══════════════════════════════════════════════════════════════════

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
        if (request.Properties.TryGetValue("ResponseSize", out var responseSizeValue))
        {
            if (int.TryParse(responseSizeValue, out var size))
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

    #endregion

    #region Section 3: Dependency Telemetry Enrichment

    // ═══════════════════════════════════════════════════════════════════
    // Section 3: Dependency Telemetry Enrichment
    // ═══════════════════════════════════════════════════════════════════

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

    #endregion

    #region Section 4: Event Telemetry Enrichment

    // ═══════════════════════════════════════════════════════════════════
    // Section 4: Event Telemetry Enrichment
    // ═══════════════════════════════════════════════════════════════════

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

    #endregion
}
