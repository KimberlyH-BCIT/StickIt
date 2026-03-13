using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ELKH.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ELKH.Services;

public class PayPalService : IPayPalService
{
    private readonly HttpClient _http;
    private readonly PayPalOptions _opts;
    private readonly IMemoryCache _cache;

    // Cache key is environment-scoped so sandbox and live tokens never collide.
    private string TokenCacheKey => $"paypal_oauth_token_{_opts.Environment}";

    public PayPalService(HttpClient http, IOptions<PayPalOptions> opts, IMemoryCache cache)
    {
        _http  = http;
        _opts  = opts.Value;
        _cache = cache;
    }

    private string BaseUrl =>
        _opts.Environment.Equals("live", StringComparison.OrdinalIgnoreCase)
            ? "https://api-m.paypal.com"
            : "https://api-m.sandbox.paypal.com";

    public async Task<string> GetAccessTokenAsync()
    {
        // PayPal OAuth tokens are valid for ~9 hours. Cache for 8 hours to give
        // a comfortable safety margin before the token is rejected mid-request.
        if (_cache.TryGetValue(TokenCacheKey, out string? cached) && cached != null)
            return cached;

        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opts.ClientId}:{_opts.Secret}"));
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v1/oauth2/token");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
        req.Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");

        var res = await _http.SendAsync(req);
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var token = doc.RootElement.GetProperty("access_token").GetString()!;

        _cache.Set(TokenCacheKey, token, TimeSpan.FromHours(8));
        return token;
    }

    public async Task<string> CreateOrderAsync(decimal total, string currency, string? idempotencyKey = null)
    {
        var token = await GetAccessTokenAsync();

        var body = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    amount = new
                    {
                        currency_code = currency,
                        value = total.ToString("F2")
                    }
                }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v2/checkout/orders");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // PayPal deduplicates requests with the same PayPal-Request-Id within a short window.
        // This prevents double-charges when a network error causes the client to retry.
        req.Headers.Add("PayPal-Request-Id", idempotencyKey ?? Guid.NewGuid().ToString());
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var res = await _http.SendAsync(req);
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    public async Task CaptureOrderAsync(string paypalOrderId)
    {
        var token = await GetAccessTokenAsync();

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v2/checkout/orders/{paypalOrderId}/capture");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var res = await _http.SendAsync(req);
        res.EnsureSuccessStatusCode();
    }
}
