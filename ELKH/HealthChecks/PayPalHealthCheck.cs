using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ELKH.Configuration;

namespace ELKH.HealthChecks;

/// <summary>
/// Health check for PayPal configuration validation.
/// Verifies that PayPal credentials are configured and valid.
/// </summary>
/// <remarks>
/// This health check validates PayPal configuration without making API calls.
/// It checks:
/// - ClientId is configured and non-empty
/// - Secret is configured and non-empty
/// - Environment is set to valid value (sandbox or live)
/// 
/// HEALTH STATUS MEANINGS:
/// - Healthy: PayPal credentials are configured
/// - Unhealthy: PayPal credentials are missing or invalid
/// 
/// NOTE: This check does NOT verify credentials are correct by calling PayPal API.
/// Actual API connectivity failures will be detected during checkout operations.
/// </remarks>
public class PayPalHealthCheck : IHealthCheck
{
    private readonly PayPalOptions _payPalOptions;

    public PayPalHealthCheck(IOptions<PayPalOptions> payPalOptions)
    {
        _payPalOptions = payPalOptions.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(_payPalOptions.ClientId))
        {
            errors.Add("PayPal ClientId is not configured");
        }

        if (string.IsNullOrWhiteSpace(_payPalOptions.Secret))
        {
            errors.Add("PayPal Secret is not configured");
        }

        if (string.IsNullOrWhiteSpace(_payPalOptions.Environment))
        {
            errors.Add("PayPal Environment is not configured");
        }
        else if (_payPalOptions.Environment != "sandbox" && _payPalOptions.Environment != "live")
        {
            errors.Add($"PayPal Environment '{_payPalOptions.Environment}' is invalid (must be 'sandbox' or 'live')");
        }

        if (errors.Count > 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"PayPal configuration is invalid: {string.Join(", ", errors)}",
                data: new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow,
                    ["service"] = "PayPal",
                    ["errors"] = errors
                }));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "PayPal is configured correctly",
            data: new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow,
                ["service"] = "PayPal",
                ["environment"] = _payPalOptions.Environment
            }));
    }
}
