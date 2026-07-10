using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rikud.DLL.AppDbContext;
using Rikud.DLL.Models;
using Rikud.WebUI.ViewModels;
using Rikud.WebUI.GlobalHelpers;
using System.Security.Claims;
using Rikud.WebUI.Models.Security;
using UAParser;

namespace Rikud.WebUI.Controllers
{
    public class UserController(RikudDbContext _db, IReCaptchaService _reCaptcha) : Controller
    {
        public IActionResult Login() => View(new LoginVm());

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> Login(LoginVm vm, [FromForm(Name = "g-recaptcha-response")] string recaptchaToken)
        {
            ModelState.Remove("g-recaptcha-response");

            if (!ModelState.IsValid) return View(vm);

            // reCAPTCHA
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (!await _reCaptcha.ValidateAsync(recaptchaToken, ip))
            {
                ModelState.AddModelError("Recaptcha", "Güvenlik doğrulaması başarısız. Lütfen tekrar deneyin.");
                return View(vm);
            }

            // 1) Kullanıcı bul
            var user = await _db.Set<Users>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Username == vm.Username && x.Active);

            if (user is null)
            {
                ModelState.AddModelError(string.Empty, "Kullanıcı adı veya şifre hatalı.");
                return View(vm);
            }

            // 2) Şifre doğrula (salt+hash)
            if (!PasswordHasher.Verify(vm.Password, user.PasswordSalt, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Kullanıcı adı veya şifre hatalı.");
                return View(vm);
            }
            // Yeni SessionKey oluştur
            var sessionKey = Guid.NewGuid();
            // 3) Cookie oluştur
            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, _db.Roles.FirstOrDefault(x=>x.Id==user.RolId)!.RolAdi),
            new Claim("SessionKey", sessionKey.ToString()),
            new Claim(ClaimTypes.Country, user.CompanyName)

        };
            var identity = new ClaimsIdentity(claims, "RikudUI");

            await HttpContext.SignInAsync("RikudUI", new ClaimsPrincipal(identity), new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTime.UtcNow.AddDays(365)
            });

            var uaParser = Parser.GetDefault();
            var clientInfo = uaParser.Parse(Request.Headers["User-Agent"]);


            string browser = clientInfo.UA.Family + " " + clientInfo.UA.Major;

            string realIp= Request.Headers["CF-Connection-IP"];
            if (string.IsNullOrEmpty(realIp))
            {
                realIp = ip;
            }

            _db.UserSessions.Add(new UserSessions
            {
                UserId = user.Id,
                SessionKey = sessionKey,
                CreatedDate = DateTime.Now,
                IpAddress = realIp ?? "Bilinmiyor",
                Browser = browser
            });
            await _db.SaveChangesAsync();

            // Oturum limiti login'de UYGULANMAZ; SessionGuardMiddleware her istekte uygular.
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var sessionKeyClaim = User.FindFirst("SessionKey")?.Value;

                if (int.TryParse(userIdClaim, out var userId) && Guid.TryParse(sessionKeyClaim, out var sessionKey))
                {
                    var session = await _db.UserSessions
                        .FirstOrDefaultAsync(x => x.UserId == userId && x.SessionKey == sessionKey);
                    if (session != null)
                    {
                        _db.UserSessions.Remove(session);
                        await _db.SaveChangesAsync();
                    }
                }
            }

            await HttpContext.SignOutAsync("RikudUI");
            return RedirectToAction(nameof(Login));
        }

    }
}

