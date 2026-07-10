using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rikud.DLL.AppDbContext;
using Rikud.DLL.Models;
using Rikud.WebAdmin.Models.Security;
using Rikud.WebAdmin.ViewModels;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Rikud.WebAdmin.Controllers
{
    public class UserController(RikudDbContext _db, IReCaptchaService _reCaptcha) : Controller
    {
        [HttpGet]
        public IActionResult Login() => View(new LoginVm());

        //[HttpGet]
        //public IActionResult Login()
        //{
        //    var test=_db.Users.AsNoTracking().FirstOrDefault(u=>u.Username=="admin");
        //    // === Şifreyi hashleyip kaydet ===
        //    string password = "4U?ld4a@6nape";

        //    // 1) Rastgele salt oluştur
        //    byte[] saltBytes = RandomNumberGenerator.GetBytes(16);

        //    // 2) Hash üret (PBKDF2)
        //    using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 100_000, HashAlgorithmName.SHA256);
        //    byte[] hashBytes = pbkdf2.GetBytes(32);

        //    // 3) Base64'e çevir
        //    string saltBase64 = Convert.ToBase64String(saltBytes);
        //    string hashBase64 = Convert.ToBase64String(hashBytes);

        //    // 4) Kullanıcı oluştur
        //    Users user = new Users
        //    {
        //        Username = "admin",
        //        PasswordHash = hashBase64,
        //        PasswordSalt = saltBase64,
        //        Active = true,
        //        CreatedDate = DateTime.Now,
        //        Role = "Administrator",
        //        SessionCount = 1
        //    };

        //    // 5) Veritabanına ekle
        //    _db.Users.Add(user);
        //    _db.SaveChanges();

        //    return View(new LoginVm());
        //}

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> Login(LoginVm vm, [FromForm(Name = "g-recaptcha-response")] string recaptchaToken)
        {
            ModelState.Remove("g-recaptcha-response");
            if (!ModelState.IsValid) return View(vm);

            //reCAPTCHA
           var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (!await _reCaptcha.ValidateAsync(recaptchaToken, ip))
            {
                ModelState.AddModelError("Recaptcha", "Güvenlik doğrulaması başarısız. Lütfen tekrar deneyin.");
                return View(vm);
            }

            // 1) Kullanıcı bul
            var user = await _db.Set<Users>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Username == vm.Username);


            if (user is null)
            {
                ModelState.AddModelError(string.Empty, "Kullanıcı bulunamadı. Bilgilerinizi kontrol edin.");
                return View(vm);
            }

            if (!user.Active)
            {
                ModelState.AddModelError(string.Empty, "Lisansınız Aktif Değildir.");
                return View(vm);
            }

            // 2) Şifre doğrula (salt+hash)
            if (!VerifyPassword(vm.Password, user.PasswordSalt, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Kullanıcı adı veya şifre hatalı.");
                return View(vm);
            }

            // 3) Cookie oluştur
            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, _db.Roles.FirstOrDefault(x=>x.Id==user.RolId)!.RolAdi)
        };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        private static bool VerifyPassword(string password, string saltBase64, string hashBase64)
        {
            // örnek PBKDF2 doğrulaması
            var salt = Convert.FromBase64String(saltBase64);
            using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(
                password, salt, 100_000, System.Security.Cryptography.HashAlgorithmName.SHA256);
            var computed = pbkdf2.GetBytes(32);
            return Convert.ToBase64String(computed) == hashBase64;
        }
    }
}
