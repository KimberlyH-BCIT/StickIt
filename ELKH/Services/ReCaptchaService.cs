using System.Text.Json;
using ELKH.Configuration;
using Microsoft.Extensions.Options;

namespace ELKH.Services;

public class ReCaptchaService : IReCaptchaService
{
    private readonly HttpClient _http;
    private readonly ReCaptchaOptions _opts;

    public ReCaptchaService(HttpClient http, IOptions<ReCaptchaOptions> opts)
    {
        _http = http;
        _opts = opts.Value;
    }

    public async Task<bool> VerifyAsync(string token, string? remoteIp = null)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        if (string.IsNullOrWhiteSpace(_opts.SecretKey)) return false;

        var form = new Dictionary<string, string>
        {
            ["secret"]   = _opts.SecretKey,
            ["response"] = token
        };

        if (!string.IsNullOrWhiteSpace(remoteIp))
            form["remoteip"] = remoteIp;

        using var res = await _http.PostAsync(_opts.VerifyUrl, new FormUrlEncodedContent(form));
        if (!res.IsSuccessStatusCode) return false;

        var json = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        return doc.RootElement.TryGetProperty("success", out var ok) && ok.GetBoolean();
    }
}
