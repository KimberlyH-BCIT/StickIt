using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ELKH.Configuration;

namespace ELKH.HealthChecks;

/// <summary>
/// Health check for SMTP email server connectivity.
/// Verifies that the application can connect to the configured SMTP server.
/// </summary>
/// TIMEOUT:
/// - Default: 10 seconds
/// - Configurable via HealthCheckOptions
/// </remarks>
public class EmailHealthCheck : IHealthCheck
{
    private readonly EmailOptions _emailOptions;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<EmailHealthCheck> _logger;

    public EmailHealthCheck(
        IOptions<EmailOptions> emailOptions,
        IWebHostEnvironment env,
        ILogger<EmailHealthCheck> logger)
    {
        _emailOptions = emailOptions.Value;
        _env = env;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // In Development, FileEmailSender is used (no SMTP), so skip the check
        if (_env.IsDevelopment())
        {
            return HealthCheckResult.Healthy(
                "Email service running in development mode (FileEmailSender)",
                data: new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow,
                    ["environment"] = "Development",
                    ["service"] = "FileEmailSender"
                });
        }

        // Validate configuration
        if (string.IsNullOrWhiteSpace(_emailOptions.Host))
        {
            return HealthCheckResult.Unhealthy(
                "SMTP host is not configured",
                data: new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow,
                    ["service"] = "SMTP",
                    ["errorType"] = "ConfigurationMissing"
                });
        }

        if (_emailOptions.Port <= 0 || _emailOptions.Port > 65535)
        {
            return HealthCheckResult.Unhealthy(
                $"SMTP port {_emailOptions.Port} is invalid",
                data: new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow,
                    ["service"] = "SMTP",
                    ["port"] = _emailOptions.Port,
                    ["errorType"] = "InvalidPort"
                });
        }

        try
        {
            // Perform TCP connection test to SMTP server
            // This validates network connectivity without sending emails
            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync(_emailOptions.Host, _emailOptions.Port, cancellationToken);

            if (!client.Connected)
            {
                return HealthCheckResult.Unhealthy(
                    $"Failed to connect to SMTP server {_emailOptions.Host}:{_emailOptions.Port}",
                    data: new Dictionary<string, object>
                    {
                        ["timestamp"] = DateTime.UtcNow,
                        ["service"] = "SMTP",
                        ["host"] = _emailOptions.Host,
                        ["port"] = _emailOptions.Port
                    });
            }

            return HealthCheckResult.Healthy(
                $"SMTP server {_emailOptions.Host}:{_emailOptions.Port} is reachable",
                data: new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow,
                    ["service"] = "SMTP",
                    ["host"] = _emailOptions.Host,
                    ["port"] = _emailOptions.Port,
                    ["ssl"] = _emailOptions.EnableSsl
                });
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            _logger.LogWarning(ex, "SMTP health check failed: Cannot connect to {Host}:{Port}",
                _emailOptions.Host, _emailOptions.Port);

            return HealthCheckResult.Unhealthy(
                $"SMTP server {_emailOptions.Host}:{_emailOptions.Port} is unreachable",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow,
                    ["service"] = "SMTP",
                    ["host"] = _emailOptions.Host,
                    ["port"] = _emailOptions.Port,
                    ["errorType"] = "NetworkError",
                    ["socketErrorCode"] = ex.SocketErrorCode.ToString()
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP health check failed: Unexpected error");

            return HealthCheckResult.Unhealthy(
                "SMTP health check failed with unexpected error",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow,
                    ["service"] = "SMTP",
                    ["errorType"] = ex.GetType().Name
                });
        }
    }
}
