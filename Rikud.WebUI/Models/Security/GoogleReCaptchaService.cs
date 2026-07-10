using Microsoft.Extensions.Options;

namespace Rikud.WebUI.Models.Security
{
    public class GoogleReCaptchaService : IReCaptchaService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ReCAPTCHASettings _opt;

        public GoogleReCaptchaService(IHttpClientFactory httpClientFactory, IOptions<ReCAPTCHASettings> opt)
        {
            _httpClientFactory = httpClientFactory;
            _opt = opt.Value;
        }

        public async Task<bool> ValidateAsync(string token, string? remoteIp = null)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(_opt.SecretKey))
                return false;

            var client = _httpClientFactory.CreateClient();
            var url = $"https://www.google.com/recaptcha/api/siteverify?secret={_opt.SecretKey}&response={token}"
                      + (string.IsNullOrEmpty(remoteIp) ? "" : $"&remoteip={remoteIp}");

            var resp = await client.GetFromJsonAsync<ReCaptchaVerifyResponse>(url);
            // v2 checkbox'ta genelde score yok; success yeterli.
            return resp?.Success == true;
        }
    }
}
