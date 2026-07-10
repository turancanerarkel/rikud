namespace Rikud.WebUI.Models.Security
{
    public interface IReCaptchaService
    {
        Task<bool> ValidateAsync(string token, string? remoteIp = null);
    }
}
