using Newtonsoft.Json;

namespace Rikud.WebAdmin.Models.Security
{
    public class ReCAPTCHASettings
    {
        public string SiteKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
    }
}
