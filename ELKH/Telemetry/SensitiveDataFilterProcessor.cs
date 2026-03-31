using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System.Text.RegularExpressions;

namespace ELKH.Telemetry;

/// <summary>
/// Application Insights telemetry processor to filter out sensitive data
/// from telemetry before it's sent to Azure.
/// </summary>
public class SensitiveDataFilterProcessor : ITelemetryProcessor
{
    private readonly ITelemetryProcessor _next;
    
    // Regex patterns for detecting sensitive data
    private static readonly Regex _emailRegex = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _creditCardRegex = new(@"\b\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b", 
        RegexOptions.Compiled);
    private static readonly Regex _passwordRegex = new(@"(password|pwd|pass)[\s=:]+[^\s&]+", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    // Sensitive property names to exclude
    private static readonly HashSet<string> _sensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "pwd", "pass", "secret", "token", "key", "credential",
        "authorization", "auth", "api-key", "apikey", "sessionid", "ssn",
        "creditcard", "cvv", "pin", "bankaccount"
    };

    public SensitiveDataFilterProcessor(ITelemetryProcessor next)
    {
        _next = next;
    }

    public void Process(ITelemetry item)
    {
        switch (item)
        {
            case RequestTelemetry request:
                ProcessRequestTelemetry(request);
                break;
            
            case DependencyTelemetry dependency:
                ProcessDependencyTelemetry(dependency);
                break;
            
            case TraceTelemetry trace:
                ProcessTraceTelemetry(trace);
                break;
            
            case ExceptionTelemetry exception:
                ProcessExceptionTelemetry(exception);
                break;
        }

        // Filter out health check requests from telemetry to reduce noise
        if (item is RequestTelemetry req && 
            (req.Url?.AbsolutePath?.Contains("/health") == true ||
             req.Url?.AbsolutePath?.Contains("/metrics") == true))
        {
            return; // Don't send to Application Insights
        }

        _next.Process(item);
    }

    private void ProcessRequestTelemetry(RequestTelemetry request)
    {
        // Filter sensitive data from URL
        if (request.Url != null)
        {
            var cleanUrl = FilterSensitiveText(request.Url.ToString());
            if (Uri.TryCreate(cleanUrl, UriKind.Absolute, out var filteredUri))
            {
                request.Url = filteredUri;
            }
        }

        // Clean properties
        CleanProperties(request.Properties);
        CleanProperties(request.Context.GlobalProperties);
    }

    private void ProcessDependencyTelemetry(DependencyTelemetry dependency)
    {
        // Filter sensitive data from dependency data (SQL queries, external API calls)
        dependency.Data = FilterSensitiveText(dependency.Data);
        CleanProperties(dependency.Properties);
    }

    private void ProcessTraceTelemetry(TraceTelemetry trace)
    {
        // Filter sensitive data from trace messages
        trace.Message = FilterSensitiveText(trace.Message);
        CleanProperties(trace.Properties);
    }

    private void ProcessExceptionTelemetry(ExceptionTelemetry exception)
    {
        // Filter sensitive data from exception messages and stack traces
        if (exception.Exception != null)
        {
            // Note: We can't modify the actual exception, but we can clean the message
            exception.Message = FilterSensitiveText(exception.Exception.Message);
        }
        
        CleanProperties(exception.Properties);
    }

    private void CleanProperties(IDictionary<string, string> properties)
    {
        var keysToRemove = new List<string>();
        var keysToMask = new List<string>();

        foreach (var kvp in properties)
        {
            // Check if property name indicates sensitive data
            if (_sensitiveProperties.Contains(kvp.Key))
            {
                keysToMask.Add(kvp.Key);
                continue;
            }

            // Check if property value contains sensitive data
            if (ContainsSensitiveData(kvp.Value))
            {
                keysToMask.Add(kvp.Key);
            }
        }

        // Mask sensitive values
        foreach (var key in keysToMask)
        {
            properties[key] = "[FILTERED]";
        }
    }

    private string FilterSensitiveText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        // Apply regex filters
        text = _emailRegex.Replace(text, "[EMAIL_FILTERED]");
        text = _creditCardRegex.Replace(text, "[CARD_FILTERED]");
        text = _passwordRegex.Replace(text, "$1=[PASSWORD_FILTERED]");

        return text;
    }

    private bool ContainsSensitiveData(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return _emailRegex.IsMatch(value) ||
               _creditCardRegex.IsMatch(value) ||
               _passwordRegex.IsMatch(value);
    }
}