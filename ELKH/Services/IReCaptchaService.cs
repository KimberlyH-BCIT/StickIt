using System.Threading.Tasks;

namespace ELKH.Services;

public interface IReCaptchaService
{
    Task<bool> VerifyAsync(string token, string? remoteIp = null);
}