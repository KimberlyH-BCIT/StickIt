namespace ELKH.Services;

/// <summary>
/// Service interface for Google reCAPTCHA verification operations providing
/// security validation for forms and user interactions to prevent automated abuse.
/// </summary>
public interface IReCaptchaService
{
    Task<bool> VerifyAsync(string token, string? remoteIp = null);
}
