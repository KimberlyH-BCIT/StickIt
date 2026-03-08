using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ELKH.Models;
using Microsoft.Extensions.Options;

namespace ELKH.Services;

public class PayPalService
{
    private readonly HttpClient _http;
    private readonly PayPalOptions _opts;

    public PayPalService(HttpClient http, IOptions<PayPalOptions> opts)
    {
        _http = http;
        _opts = opts.Value;
    }

    private string BaseUrl =>
        _opts.Environment.Equals("live", StringComparison.OrdinalIgnoreCase)
            ? "https://api-m.paypal.com"
            : "https://api-m.sandbox.paypal.com";

    public async Task<string> GetAccessTokenAsync()
    {
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opts.ClientId}:{_opts.Secret}"));
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v1/oauth2/token");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
        req.Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");

        var res = await _http.SendAsync(req);
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("access_token").GetString()!;
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