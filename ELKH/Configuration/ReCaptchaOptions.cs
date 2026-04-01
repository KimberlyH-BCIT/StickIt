namespace ELKH.Configuration;

/// <summary>
/// Configuration options for Google reCAPTCHA integration.
/// Provides bot protection and spam prevention for user-facing forms.
/// </summary>
/// <remarks>
/// reCAPTCHA helps protect the application from automated bots and spam by
/// requiring users to complete challenges that are easy for humans but difficult
/// for bots. This configuration supports reCAPTCHA v2 ("I'm not a robot" checkbox).
/// 
/// <para><strong>Security Requirements:</strong></para>
/// <list type="bullet">
/// <item>SiteKey can be public (used in frontend JavaScript)</item>
/// <item>SecretKey must be kept confidential (server-side verification only)</item>
/// <item>Register at https://www.google.com/recaptcha/admin to get keys</item>
/// <item>Configure allowed domains to prevent unauthorized usage</item>
/// </list>
/// 
/// <para><strong>Usage Scenarios:</strong></para>
/// <list type="bullet">
/// <item>Contact forms - prevent spam submissions</item>
/// <item>Registration forms - prevent bot account creation</item>
/// <item>Login forms - additional protection against brute force attacks</item>
/// <item>Review submissions - prevent fake review spam</item>
/// </list>
/// 
/// <para><strong>Configuration Example:</strong></para>
/// <code>
/// {
///   "ReCaptcha": {
///     "SiteKey": "6LeIxAcTAAAAAJcZVRqyHh71UMIEGNQ_MXjiZKhI",
///     "SecretKey": "6LeIxAcTAAAAAGG-vFI1TnRWxMZNFuojJ4WifJWe",
///     "VerifyUrl": "https://www.google.com/recaptcha/api/siteverify"
///   }
/// }
/// </code>
/// </remarks>
public class ReCaptchaOptions
{
    /// <summary>
    /// reCAPTCHA Site Key for frontend integration.
    /// </summary>
    /// <remarks>
    /// This key is used in client-side JavaScript to render the reCAPTCHA widget.
    /// It's safe to expose publicly as it's domain-restricted in Google's console.
    /// </remarks>
    public string SiteKey   { get; set; } = "";

    /// <summary>
    /// reCAPTCHA Secret Key for server-side verification.
    /// </summary>
    /// <remarks>
    /// This key is used to verify reCAPTCHA responses on the server.
    /// Must be kept confidential and configured via secure methods.
    /// </remarks>
    public string SecretKey { get; set; } = "";

    /// <summary>
    /// Google reCAPTCHA verification API endpoint.
    /// </summary>
    /// <remarks>
    /// Standard Google endpoint for verifying reCAPTCHA responses.
    /// Should rarely need to change from the default value.
    /// </remarks>
    public string VerifyUrl { get; set; } = "https://www.google.com/recaptcha/api/siteverify";
}
