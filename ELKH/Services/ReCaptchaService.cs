using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ELKH.Models;

namespace ELKH.Services
{
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

            var url = $"{_opts.VerifyUrl}?secret={_opts.SecretKey}&response={token}";
            if (!string.IsNullOrWhiteSpace(remoteIp))
                url += $"&remoteip={remoteIp}";

            var resp = await _http.PostAsync(url, null);
            if (!resp.IsSuccessStatusCode) return false;

            var json = await resp.Content.ReadAsStringAsync();

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("success", out var success))
                    return success.GetBoolean();
            }
            catch
            {
                // ignore parse errors
            }

            return false;
        }
    }
}