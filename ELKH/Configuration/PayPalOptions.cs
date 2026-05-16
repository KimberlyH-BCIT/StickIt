namespace ELKH.Configuration;

/// <summary>
/// Configuration options for PayPal payment processing integration.
/// Contains credentials and settings required for PayPal API communication.
/// </summary>
/// <remarks>
/// This configuration class supports both sandbox (development) and live (production)
/// PayPal environments. All sensitive values (ClientId, Secret) must be configured
/// via user secrets in development or environment variables/Azure Key Vault in production.
/// 
/// <para><strong>Security Requirements:</strong></para>
/// <list type="bullet">
/// <item>ClientId and Secret must never be committed to source control</item>
/// <item>Use dotnet user-secrets in development</item>
/// <item>Use secure configuration providers in production</item>
/// <item>Validate environment setting to prevent sandbox/live mix-ups</item>
/// </list>
/// 
/// <para><strong>Supported Environments:</strong></para>
/// <list type="bullet">
/// <item>"sandbox" - PayPal sandbox environment for testing</item>
/// <item>"live" - PayPal production environment for real transactions</item>
/// </list>
/// 
/// <para><strong>Configuration Example:</strong></para>
/// <code>
/// {
///   "PayPal": {
///     "ClientId": "your-client-id",
///     "Secret": "your-secret",
///     "Environment": "sandbox",
///     "Currency": "CAD"
///   }
/// }
/// </code>
/// </remarks>
public class PayPalOptions
{
    /// <summary>
    /// PayPal Client ID for API authentication.
    /// </summary>
    /// <remarks>
    /// This is a public identifier for your PayPal application.
    /// Must be configured via secure configuration providers.
    /// </remarks>
    public string ClientId { get; set; } = "";

    /// <summary>
    /// PayPal Client Secret for API authentication.
    /// </summary>
    /// <remarks>
    /// This is a confidential secret that must never be exposed publicly.
    /// Must be configured via user secrets or secure environment variables.
    /// </remarks>
    public string Secret { get; set; } = "";

    /// <summary>
    /// PayPal environment setting: "sandbox" for testing, "live" for production.
    /// </summary>
    /// <remarks>
    /// Defaults to "sandbox" for safe development practices.
    /// Production deployments must explicitly set this to "live".
    /// </remarks>
    public string Environment { get; set; } = "sandbox";

    /// <summary>
    /// Currency code for PayPal transactions.
    /// </summary>
    /// <remarks>
    /// Defaults to "CAD" (Canadian Dollar) for ELKH's Canadian market focus.
    /// Must be a valid ISO 4217 currency code supported by PayPal.
    /// </remarks>
    public string Currency { get; set; } = "CAD";

    /// <summary>
    /// Optional expected merchant identifier returned by PayPal for verified captures.
    /// </summary>
    public string? MerchantId { get; set; }

    /// <summary>
    /// Optional expected merchant email returned by PayPal for verified captures.
    /// </summary>
    public string? MerchantEmail { get; set; }
}
