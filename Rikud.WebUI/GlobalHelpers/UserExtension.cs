using System.Security.Claims;

namespace Rikud.WebUI.GlobalHelpers
{
    public class UserExtension
    {
        public static bool IsRole(string role)
        {
            var accessor = new HttpContextAccessor();

            var cookieValue = accessor.HttpContext.User.FindFirst(x => x.Type == ClaimTypes.Role)?.Value;

            return cookieValue.Split(",").Any(x => x == role);
        }

    }
}
