using ELKH.Configuration;
using Microsoft.Extensions.Options;

namespace ELKH.Services;

// TABLE OF CONTENTS
// - Configuration validation
// - Required setting checks
// - Error reporting

/// <summary>
/// Validates critical application configuration at startup to fail fast if required
/// secrets are missing or invalid. Prevents application from starting with incomplete
/// configuration that would cause runtime failures.
/// </summary>
public class ConfigurationValidator
{
    private readonly ILogger<ConfigurationValidator> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly PayPalOptions _payPalOptions;
    private readonly ReCaptchaOptions _reCaptchaOptions;
    private readonly EmailOptions _emailOptions;
    private readonly IConfiguration _configuration;

    public ConfigurationValidator(
        ILogger<ConfigurationValidator> logger,
        IWebHostEnvironment env,
        IOptions<PayPalOptions> payPalOptions,
        IOptions<ReCaptchaOptions> reCaptchaOptions,
        IOptions<EmailOptions> emailOptions,
        IConfiguration configuration)
    {
        _logger = logger;
        _env = env;
        _payPalOptions = payPalOptions.Value;
        _reCaptchaOptions = reCaptchaOptions.Value;
        _emailOptions = emailOptions.Value;
        _configuration = configuration;
    }

    /// <summary>
    /// Validates all critical configuration settings.
    /// Throws exceptions in production, logs warnings in development.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when required configuration is missing in non-Development environments.
    /// </exception>
    public void ValidateConfiguration()
    {
        var isNonProductionValidationEnvironment = _env.IsDevelopment() || _env.IsEnvironment("Testing");
        var errors = new List<string>();

        // ===================================================================
        // PayPal Configuration Validation
        // ===================================================================
        if (string.IsNullOrWhiteSpace(_payPalOptions.ClientId))
        {
            errors.Add("PayPal:ClientId is not configured. Set via user-secrets or environment variable.");
        }

        if (string.IsNullOrWhiteSpace(_payPalOptions.Secret))
        {
            errors.Add("PayPal:Secret is not configured. Set via user-secrets or environment variable.");
        }

        if (string.IsNullOrWhiteSpace(_payPalOptions.Environment))
        {
            errors.Add("PayPal:Environment is not configured. Must be 'sandbox' or 'live'.");
        }
        else if (_payPalOptions.Environment != "sandbox" && _payPalOptions.Environment != "live")
        {
            errors.Add($"PayPal:Environment is '{_payPalOptions.Environment}' but must be 'sandbox' or 'live'.");
        }

        // ===================================================================
        // ReCaptcha Configuration Validation
        // ===================================================================
        if (string.IsNullOrWhiteSpace(_reCaptchaOptions.SiteKey))
        {
            errors.Add("ReCaptcha:SiteKey is not configured. Register at https://www.google.com/recaptcha/admin");
        }

        if (string.IsNullOrWhiteSpace(_reCaptchaOptions.SecretKey))
        {
            errors.Add("ReCaptcha:SecretKey is not configured. Register at https://www.google.com/recaptcha/admin");
        }

        // ===================================================================
        // Email Configuration Validation
        // ===================================================================
        // Email is only required in non-Development environments
        // (Development uses FileEmailSender which writes to disk)
        if (!isNonProductionValidationEnvironment)
        {
            if (string.IsNullOrWhiteSpace(_emailOptions.Host))
            {
                errors.Add("Email:Host is not configured. Required for SMTP email sending.");
            }

            if (_emailOptions.Port <= 0 || _emailOptions.Port > 65535)
            {
                errors.Add($"Email:Port is {_emailOptions.Port} but must be between 1 and 65535.");
            }

            if (string.IsNullOrWhiteSpace(_emailOptions.User))
            {
                errors.Add("Email:User is not configured. Required for SMTP authentication.");
            }

            if (string.IsNullOrWhiteSpace(_emailOptions.Pass))
            {
                errors.Add("Email:Pass is not configured. Required for SMTP authentication.");
            }

            if (string.IsNullOrWhiteSpace(_emailOptions.From))
            {
                errors.Add("Email:From is not configured. Required for email sender address.");
            }
        }

        // ===================================================================
        // Admin Seed Configuration Validation
        // ===================================================================
        var adminEmail = _configuration["Seed:AdminEmail"];
        var adminPass = _configuration["Seed:AdminPass"];

        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            errors.Add("Seed:AdminEmail is not configured. Set via user-secrets: dotnet user-secrets set Seed:AdminEmail your@email.com");
        }

        if (string.IsNullOrWhiteSpace(adminPass))
        {
            errors.Add("Seed:AdminPass is not configured. Set via user-secrets: dotnet user-secrets set Seed:AdminPass YourSecurePassword123!");
        }
        else if (adminPass.Length < 8)
        {
            errors.Add("Seed:AdminPass must be at least 8 characters long.");
        }

        // ===================================================================
        // Error Handling: Fail Fast or Warn
        // ===================================================================
        if (errors.Count > 0)
        {
            var errorMessage = string.Join(Environment.NewLine, errors);

            if (isNonProductionValidationEnvironment)
            {
                // Development: Log warnings but allow startup
                _logger.LogWarning(
                    "Configuration validation warnings detected:{NewLine}{Errors}{NewLine}" +
                    "The application will start with limited functionality. Configure missing values via:{NewLine}" +
                    "  â€¢ User Secrets: dotnet user-secrets set <Key> <Value>{NewLine}" +
                    "  â€¢ appsettings.Development.json (for non-secrets){NewLine}" +
                    "  â€¢ Environment variables",
                    Environment.NewLine, Environment.NewLine, errorMessage, Environment.NewLine, Environment.NewLine, Environment.NewLine);
            }
            else
            {
                // Production: Fail fast with detailed instructions
                _logger.LogCritical(
                    "Critical configuration validation failed:{NewLine}{Errors}{NewLine}" +
                    "Application startup aborted. Fix configuration errors and restart:{NewLine}" +
                    "  â€¢ Environment Variables: Set ASPNETCORE_PayPal__ClientId, etc.{NewLine}" +
                    "  â€¢ Azure Key Vault: Configure in Azure App Service configuration{NewLine}" +
                    "  â€¢ AWS Secrets Manager: Configure via AWS Systems Manager Parameter Store",
                    Environment.NewLine, Environment.NewLine, errorMessage, Environment.NewLine, Environment.NewLine, Environment.NewLine);

                throw new InvalidOperationException(
                    $"Application configuration is invalid. Missing or invalid required settings:{Environment.NewLine}{errorMessage}");
            }
        }
        else
        {
            _logger.LogInformation("Configuration validation passed. All required secrets are configured.");
        }
    }
}
