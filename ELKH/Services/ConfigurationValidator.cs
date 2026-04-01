using ELKH.Configuration;
using Microsoft.Extensions.Options;

namespace ELKH.Services;

/// <summary>
/// Validates critical application configuration at startup to fail fast if required
/// secrets are missing or invalid. Prevents application from starting with incomplete
/// configuration that would cause runtime failures.
/// </summary>
/// <remarks>
/// TABLE OF CONTENTS
/// ================================================================================
/// 1. Dependencies & Constructor ................................. Lines [30-51]
///    - ILogger, IWebHostEnvironment    // Core dependencies
///    - PayPal, ReCaptcha, Email       // Options injection
///    - IConfiguration                 // Admin seed access
/// 
/// 2. Main Validation Method ..................................... Lines [53-189]
///    - ValidateConfiguration()        // Orchestrates all validation checks
///    - Error collection strategy      // Accumulate all errors before failing
/// 
/// 3. PayPal Configuration Validation ............................ Lines [64-84]
///    - ClientId validation            // Required for payment processing
///    - Secret validation             // Required for API authentication
///    - Environment validation        // Must be 'sandbox' or 'live'
/// 
/// 4. ReCaptcha Configuration Validation ......................... Lines [86-97]
///    - SiteKey validation            // Frontend captcha integration
///    - SecretKey validation          // Backend verification token
/// 
/// 5. Email Configuration Validation ............................. Lines [99-130]
///    - Host, Port validation         // SMTP server configuration
///    - User, Pass validation         // SMTP authentication
///    - Environment-aware rules       // Development uses file email
/// 
/// 6. Admin Seed Configuration Validation ........................ Lines [132-151]
///    - AdminEmail validation         // First admin account creation
///    - AdminPass validation          // Secure password requirements
/// 
/// 7. Error Handling & Environment Strategy ...................... Lines [153-189]
///    - Development mode              // Warnings only, graceful degradation
///    - Production mode               // Fail fast with detailed instructions
/// ================================================================================
/// 
/// ARCHITECTURAL CONTEXT:
/// • Critical startup service implementing fail-fast configuration validation
/// • Prevents runtime failures by catching configuration issues early
/// • Environment-aware behavior: warnings in development, errors in production
/// • Part of ELKH's robust deployment and configuration management strategy
/// • Integrated with ASP.NET Core's Options pattern and dependency injection
/// 
/// VALIDATION STRATEGY:
/// This service implements a comprehensive configuration validation approach:
/// 1. Accumulate all errors before reporting (don't fail on first error)
/// 2. Environment-specific behavior (development vs production)
/// 3. Clear error messages with actionable remediation steps
/// 4. Structured logging for operational monitoring
/// 5. Exit code strategy for automated deployment scenarios
/// 
/// CONFIGURATION SOURCES VALIDATED:
/// • PayPal Integration - ClientId, Secret, Environment (sandbox/live)
/// • ReCaptcha Protection - SiteKey for frontend, SecretKey for backend
/// • Email Services - SMTP configuration for transactional emails
/// • Admin Seeding - Initial admin account for application bootstrap
/// 
/// ENVIRONMENT BEHAVIOR:
/// • Development: Log warnings, allow startup with degraded functionality
/// • Staging/Production: Log critical errors, throw exceptions, abort startup
/// • Provides clear instructions for each configuration source type
/// • Supports multiple secret management patterns (user-secrets, env vars, key vault)
/// 
/// INTEGRATION POINTS:
/// • Used by: Program.cs startup validation pipeline
/// • Depends on: IOptions&lt;T&gt; for strongly-typed configuration access
/// • Integrates with: ASP.NET Core configuration providers
/// • Supports: Azure Key Vault, AWS Secrets Manager, environment variables
/// 
/// OPERATIONAL CONSIDERATIONS:
/// • Structured logging enables monitoring of configuration issues
/// • Clear error messages reduce deployment troubleshooting time
/// • Environment-aware behavior supports different deployment scenarios
/// • Exit code strategy enables automated deployment pipeline integration
/// 
/// SECURITY IMPLICATIONS:
/// • Validates presence of security-critical configurations
/// • Does not log sensitive values (secrets, passwords)
/// • Enforces minimum password complexity for admin accounts
/// • Validates PayPal environment to prevent sandbox/live mix-ups
/// </remarks>
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
        var errors = new List<string>();

        // ═══════════════════════════════════════════════════════════════════
        // PayPal Configuration Validation
        // ═══════════════════════════════════════════════════════════════════
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

        // ═══════════════════════════════════════════════════════════════════
        // ReCaptcha Configuration Validation
        // ═══════════════════════════════════════════════════════════════════
        if (string.IsNullOrWhiteSpace(_reCaptchaOptions.SiteKey))
        {
            errors.Add("ReCaptcha:SiteKey is not configured. Register at https://www.google.com/recaptcha/admin");
        }

        if (string.IsNullOrWhiteSpace(_reCaptchaOptions.SecretKey))
        {
            errors.Add("ReCaptcha:SecretKey is not configured. Register at https://www.google.com/recaptcha/admin");
        }

        // ═══════════════════════════════════════════════════════════════════
        // Email Configuration Validation
        // ═══════════════════════════════════════════════════════════════════
        // Email is only required in non-Development environments
        // (Development uses FileEmailSender which writes to disk)
        if (!_env.IsDevelopment())
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

        // ═══════════════════════════════════════════════════════════════════
        // Admin Seed Configuration Validation
        // ═══════════════════════════════════════════════════════════════════
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

        // ═══════════════════════════════════════════════════════════════════
        // Error Handling: Fail Fast or Warn
        // ═══════════════════════════════════════════════════════════════════
        if (errors.Count > 0)
        {
            var errorMessage = string.Join(Environment.NewLine, errors);

            if (_env.IsDevelopment())
            {
                // Development: Log warnings but allow startup
                _logger.LogWarning(
                    "Configuration validation warnings detected:{NewLine}{Errors}{NewLine}" +
                    "The application will start with limited functionality. Configure missing values via:{NewLine}" +
                    "  • User Secrets: dotnet user-secrets set <Key> <Value>{NewLine}" +
                    "  • appsettings.Development.json (for non-secrets){NewLine}" +
                    "  • Environment variables",
                    Environment.NewLine, Environment.NewLine, errorMessage, Environment.NewLine, Environment.NewLine, Environment.NewLine);
            }
            else
            {
                // Production: Fail fast with detailed instructions
                _logger.LogCritical(
                    "Critical configuration validation failed:{NewLine}{Errors}{NewLine}" +
                    "Application startup aborted. Fix configuration errors and restart:{NewLine}" +
                    "  • Environment Variables: Set ASPNETCORE_PayPal__ClientId, etc.{NewLine}" +
                    "  • Azure Key Vault: Configure in Azure App Service configuration{NewLine}" +
                    "  • AWS Secrets Manager: Configure via AWS Systems Manager Parameter Store",
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
