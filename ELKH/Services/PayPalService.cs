using System.Net.Http.Headers;
using System.Text;

namespace ELKH.Services;

/// <summary>
/// PayPal integration service providing payment processing functionality including
/// order creation, payment capture, and transaction management through PayPal's REST API.
/// </summary>
public class PayPalService(HttpClient http, IOptions<PayPalOptions> opts, IMemoryCache cache) : IPayPalService
{
    private readonly PayPalOptions _opts = opts.Value;
    private readonly SemaphoreSlim _tokenSemaphore = new(1, 1);

    // Cache key for PayPal access token
    private const string TokenCacheKey = "PayPal_AccessToken";

    // Token cache duration (PayPal tokens are typically valid for 9 hours, we cache for 8.5 hours)
    private static readonly TimeSpan TokenCacheDuration = TimeSpan.FromHours(8.5);

    private string BaseUrl => _opts.Environment.ToLowerInvariant() switch
    {
        "live" => "https://api-m.paypal.com",
        _ => "https://api-m.sandbox.paypal.com"
    };

    public async Task<string> GetAccessTokenAsync()
    {
        // Check cache first
        if (cache.TryGetValue(TokenCacheKey, out string? cachedToken) && !string.IsNullOrEmpty(cachedToken))
        {
            return cachedToken;
        }

        // Use semaphore to prevent concurrent token requests
        await _tokenSemaphore.WaitAsync();
        try
        {
            // Double-check cache after acquiring lock (another thread might have populated it)
            if (cache.TryGetValue(TokenCacheKey, out cachedToken) && !string.IsNullOrEmpty(cachedToken))
            {
                return cachedToken;
            }

            // Get fresh token from PayPal
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opts.ClientId}:{_opts.Secret}"));
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v1/oauth2/token");
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
            req.Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");

            var res = await http.SendAsync(req);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var token = doc.RootElement.GetProperty("access_token").GetString()!;

            // Cache the token with expiration
            cache.Set(TokenCacheKey, token, TokenCacheDuration);

            return token;
        }
        finally
        {
            _tokenSemaphore.Release();
        }
    }

    public async Task<string> CreateOrderAsync(decimal total, string currency)
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
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var res = await http.SendAsync(req);
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

        var res = await http.SendAsync(req);
        res.EnsureSuccessStatusCode();
    }
}
