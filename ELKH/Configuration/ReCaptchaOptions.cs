namespace ELKH.Configuration;

public class ReCaptchaOptions
{
    public string SiteKey   { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string VerifyUrl { get; set; } = "https://www.google.com/recaptcha/api/siteverify";
}
