using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

namespace ELKH.Services;

/// <summary>
/// PayPal integration service providing token management and server-side payment verification through PayPal's REST API.
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

    public async Task<PayPalVerificationResult> VerifyCapturedOrderAsync(string paypalOrderId, decimal expectedAmount, string expectedCurrency)
    {
        if (string.IsNullOrWhiteSpace(paypalOrderId))
        {
            throw new InvalidOperationException("A PayPal order ID is required for payment verification.");
        }

        var token = await GetAccessTokenAsync();

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/v2/checkout/orders/{paypalOrderId}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var res = await http.SendAsync(req);
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var status = root.GetProperty("status").GetString() ?? string.Empty;
        if (!string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"PayPal order {paypalOrderId} is not completed. Current status: {status}.");
        }

        if (!root.TryGetProperty("purchase_units", out var purchaseUnits) || purchaseUnits.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"PayPal order {paypalOrderId} does not contain any purchase units.");
        }

        var purchaseUnit = purchaseUnits[0];
        var payments = purchaseUnit.GetProperty("payments");
        if (!payments.TryGetProperty("captures", out var captures) || captures.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"PayPal order {paypalOrderId} does not contain any capture records.");
        }

        var capture = captures[0];
        var captureStatus = capture.GetProperty("status").GetString() ?? string.Empty;
        if (!string.Equals(captureStatus, "COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"PayPal capture for order {paypalOrderId} is not completed. Current status: {captureStatus}.");
        }

        var amount = capture.GetProperty("amount");
        var actualCurrency = amount.GetProperty("currency_code").GetString() ?? string.Empty;
        var actualAmountValue = amount.GetProperty("value").GetString() ?? "0";
        var actualAmount = decimal.Parse(actualAmountValue, CultureInfo.InvariantCulture);

        if (!string.Equals(actualCurrency, expectedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"PayPal currency mismatch for order {paypalOrderId}. Expected {expectedCurrency}, received {actualCurrency}.");
        }

        if (actualAmount != decimal.Round(expectedAmount, 2, MidpointRounding.AwayFromZero))
        {
            throw new InvalidOperationException($"PayPal amount mismatch for order {paypalOrderId}. Expected {expectedAmount:F2}, received {actualAmount:F2}.");
        }

        string? merchantId = null;
        string? merchantEmail = null;
        if (purchaseUnit.TryGetProperty("payee", out var payee))
        {
            merchantId = payee.TryGetProperty("merchant_id", out var merchantIdElement)
                ? merchantIdElement.GetString()
                : null;
            merchantEmail = payee.TryGetProperty("email_address", out var merchantEmailElement)
                ? merchantEmailElement.GetString()
                : null;
        }

        if (!string.IsNullOrWhiteSpace(_opts.MerchantId) && !string.Equals(_opts.MerchantId, merchantId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"PayPal merchant mismatch for order {paypalOrderId}. Expected merchant ID {_opts.MerchantId}, received {merchantId ?? "<missing>"}.");
        }

        if (!string.IsNullOrWhiteSpace(_opts.MerchantEmail) && !string.Equals(_opts.MerchantEmail, merchantEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"PayPal merchant mismatch for order {paypalOrderId}. Expected merchant email {_opts.MerchantEmail}, received {merchantEmail ?? "<missing>"}.");
        }

        string? payerId = null;
        string? payerEmail = null;
        if (root.TryGetProperty("payer", out var payer))
        {
            payerId = payer.TryGetProperty("payer_id", out var payerIdElement)
                ? payerIdElement.GetString()
                : null;
            payerEmail = payer.TryGetProperty("email_address", out var payerEmailElement)
                ? payerEmailElement.GetString()
                : null;
        }

        DateTime? capturedAtUtc = null;
        if (capture.TryGetProperty("create_time", out var createTimeElement)
            && DateTime.TryParse(createTimeElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsedCaptureTime))
        {
            capturedAtUtc = parsedCaptureTime;
        }

        var verificationSummary = JsonSerializer.Serialize(new
        {
            orderId = paypalOrderId,
            orderStatus = status,
            captureId = capture.GetProperty("id").GetString(),
            captureStatus,
            amount = actualAmount.ToString("F2", CultureInfo.InvariantCulture),
            currency = actualCurrency,
            merchantId,
            merchantEmail,
            payerId,
            payerEmail,
            capturedAtUtc
        });

        return new PayPalVerificationResult
        {
            PayPalOrderId = paypalOrderId,
            CaptureId = capture.GetProperty("id").GetString() ?? string.Empty,
            Status = captureStatus,
            Amount = actualAmount,
            Currency = actualCurrency,
            MerchantId = merchantId,
            MerchantEmail = merchantEmail,
            CapturedAtUtc = capturedAtUtc,
            PayerId = payerId,
            PayerEmail = payerEmail,
            VerificationSummaryJson = verificationSummary
        };
    }
}
