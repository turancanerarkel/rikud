using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rikud.DLL.AppDbContext;
using Rikud.DLL.Models;
using Rikud.WebAdmin.GlobalHelpers;
using Rikud.WebAdmin.Models;
using Rikud.WebAdmin.ViewModels;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Claims;
using static Rikud.WebAdmin.Controllers.HomeController;

namespace Rikud.WebAdmin.Controllers
{
    [Authorize]
    public class HomeController(RikudDbContext _db) : Controller
    {
        public IActionResult Index() => View();

        public Task<IActionResult> Kullanicilar()
        {
            ViewBag.UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Task.FromResult<IActionResult>(View());
        }

        public async Task<IActionResult> _KullaniciList()
        {
            var users = await _db.Users.Where(x => x.Id != 1).AsNoTracking().ToListAsync();

            foreach (var item in users)
            {
                item.Role = _db.Roles.FirstOrDefault(x => x.Id == item.RolId)!.RolAdi;
            }

            return Json(users);
        }

        public async Task<IActionResult> KullaniciEkle()
        {
            ViewBag.Roller = await _db.Set<Roles>().AsNoTracking().Where(x => x.Id != 1).ToListAsync();
            return View(new Users() { Active = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KullaniciEkle(Users user)
        {
            ViewBag.Roller = await _db.Set<Roles>().AsNoTracking().Where(x => x.Id != 1).ToListAsync();
            ModelState.Remove("PasswordHash");
            ModelState.Remove("PasswordSalt");
            // Kullanıcı adı kontrolü
            if (await _db.Users.AnyAsync(x => x.Username == user.Username))
            {
                ModelState.AddModelError("Username", "Bu kullanıcı adı zaten kayıtlı.");
            }

            // Admin / LocalAdmin tekil kontrolü
            if (user.RolId == 1 || user.RolId == 2)
            {
                bool anotherAdminExists = await _db.Users
                    .AnyAsync(x => x.RolId == 1 || x.RolId == 2);

                if (anotherAdminExists)
                    ModelState.AddModelError("RolId", "Administrator veya LocalAdmin rolleri yalnızca bir kullanıcıya atanabilir.");
            }

            if (!ModelState.IsValid)
                return View(user);

            // Şifre hash/salt oluştur
            if (string.IsNullOrWhiteSpace(user.Password))
            {
                ModelState.AddModelError("Password", "Şifre zorunludur.");
                return View(user);
            }

            var (hash, salt) = PasswordHasher.Create(user.Password);

            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            user.Password = null; // plain text taşımayalım
            user.Active = user.Active;
            user.SessionCount = user.SessionCount == 0 ? 1 : user.SessionCount;
            user.CreatedDate = DateTime.Now;
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Kullanıcı başarıyla eklendi.";
            return RedirectToAction(nameof(Kullanicilar));
        }

        public async Task<IActionResult> KullaniciDuzenle(int Id)
        {
            if (Id == 1)
                return View(nameof(Kullanicilar));

            if (!await _db.Users.AnyAsync(x => x.Id == Id))
                return View(nameof(Kullanicilar));

            var user = _db.Users.FirstOrDefault(x => x.Id == Id);
            ViewBag.Roller = await _db.Set<Roles>().AsNoTracking().ToListAsync();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KullaniciDuzenle(Users user)
        {
            var entity = await _db.Users.FirstOrDefaultAsync(x => x.Id == user.Id);
            if (entity == null)
                return RedirectToAction(nameof(Kullanicilar));

            if (user.Id == 1)
                return View(nameof(Kullanicilar));

            ModelState.Remove("PasswordHash");
            ModelState.Remove("PasswordSalt");

            // Şifre boş geldiyse Password’u da doğrulama dışı bırak
            if (string.IsNullOrWhiteSpace(user.Password))
                ModelState.Remove("Password");
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;


            // Admin / LocalAdmin tekil olsun
            if (user.RolId == 1 || user.RolId == 2)
            {
                bool anotherAdminExists = await _db.Users
                    .AnyAsync(x => x.Id != user.Id && (x.RolId == 1 || x.RolId == 2));

                if (anotherAdminExists)
                {
                    ModelState.AddModelError("RolId",
                        "Administrator veya LocalAdmin rolü başka bir kullanıcıya atanmış. " +
                        "Bu rolleri aynı anda yalnızca bir kullanıcı alabilir.");
                }
            }

            if (user.Id == Convert.ToInt32(userIdClaim) && !user.Active)
            {
                ModelState.AddModelError("UserId",
                       "Kendi kullanıcınızın durumunu pasife çekemezsiniz. " +
                       "Bunu admin kullanıcısı yapmalıdır.");
            }


            if (!ModelState.IsValid)
            {
                ViewBag.Roller = await _db.Set<Roles>().AsNoTracking().ToListAsync();
                return View(user); // formu hatalarla geri göster
            }

            // Alanlar? güncelle
            entity.Username = user.Username?.Trim();
            entity.RolId = user.RolId;
            entity.SessionCount = user.SessionCount;
            entity.Active = user.Active;
            entity.CompanyName = user.CompanyName;

            // ?ifre girilmi?se güncelle, bo?sa dokunma
            if (!string.IsNullOrWhiteSpace(user.Password))
            {
                var (hash, salt) = GlobalHelpers.PasswordHasher.Create(user.Password);
                entity.PasswordHash = hash;
                entity.PasswordSalt = salt;
            }

            await _db.SaveChangesAsync();

            if (!user.Active)
            {
                var sessions = await _db.UserSessions
                    .Where(x => x.UserId == entity.Id)
                    .ToListAsync();

                _db.UserSessions.RemoveRange(sessions);

                var entity2 = await _db.Users.FirstOrDefaultAsync(x => x.Id == entity.Id);
                entity2.SessionCount = 0;
                await _db.SaveChangesAsync();
            }
            else
            {
                // Oturum sınırı kontrolü
                var sessions = await _db.UserSessions
                    .Where(x => x.UserId == entity.Id)
                    .OrderByDescending(x => x.Id)           // veya LoginTime/LastAccessTime varsa ona göre sırala
                    .ToListAsync();

                int extra = sessions.Count - user.SessionCount;
                if (extra > 0)
                {
                    // Tutulacak ilk N oturumu bırak, fazlalıkları sil
                    var toRemove = sessions.Skip(user.SessionCount).Take(extra).ToList();
                    _db.UserSessions.RemoveRange(toRemove);
                    await _db.SaveChangesAsync();
                }
            }



            TempData["Success"] = "Kullanıcı güncellendi.";
            return RedirectToAction(nameof(Kullanicilar));
        }

        public async Task<IActionResult> KullaniciSil(int Id)
        {
            if (!await _db.Users.AnyAsync(x => x.Id == Id))
                return View(nameof(Kullanicilar));

            var user = _db.Users.FirstOrDefault(x => x.Id == Id);
            user.Active = false;
            _db.Users.Update(user);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Kullanicilar));
        }

        public Task<IActionResult> KullaniciOturumlari()
        {
            return Task.FromResult<IActionResult>(View());
        }

        public async Task<IActionResult> _KullaniciOturumlari()
        {
            var userSessions = await _db.UserSessions.Where(x => x.UserId != 1).AsNoTracking().ToListAsync();

            var userList = await _db.Users.Where(x => x.Id != 1).AsNoTracking().ToListAsync();
            foreach (var item in userList)
            {
                int i = 1;
                foreach (var item2 in userSessions.Where(x => x.UserId == item.Id))
                {
                    item2.Oturum = i;
                    item2.Username = userList.FirstOrDefault(x => x.Id == item2.UserId)!.Username;
                    i++;
                }
            }
            return Json(userSessions);
        }

        public async Task<IActionResult> OturumSil(int Id)
        {
            if (!await _db.UserSessions.AnyAsync(x => x.Id == Id))
                return View(nameof(KullaniciOturumlari));

            var userSession = _db.UserSessions.FirstOrDefault(x => x.Id == Id);
            _db.UserSessions.Remove(userSession);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(KullaniciOturumlari));
        }

        public async Task<IActionResult> DovizModlari()
        {
            var liste = await _db.ExchangeModeValues.AsNoTracking().ToListAsync();

            ViewBag.DovizModlari = liste;

            return View();
        }

        public async Task<IActionResult> _ModeDovizList()
        {
            var exchangeModeValues = await _db.ExchangeModeValues.AsNoTracking().ToListAsync();
            var users = await _db.Users.AsNoTracking().ToListAsync();
            var modes = await _db.Mods.AsNoTracking().ToListAsync();
            foreach (var item in exchangeModeValues)
            {
                item.CreatedByStr = users.FirstOrDefault(x => x.Id == item.CreatedBy)!.Username;
                item.ModAdi = modes.FirstOrDefault(x => x.Id == item.ModId)!.ModeName;
            }
            return Json(exchangeModeValues);
        }

        public async Task<IActionResult> DovizModuEkle()
        {
            ViewBag.Modlar = await _db.Set<Modes>().AsNoTracking().ToListAsync();
            return View(new ExcModValVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DovizModuEkle(ExcModValVm vm)
        {
            ModelState.Remove("CreatedByStr");
            ModelState.Remove("ModAdi");

            if (!ModelState.IsValid)
            {
                ViewBag.Modlar = await _db.Set<Modes>().AsNoTracking().ToListAsync();
                return View(vm);
            }

            if (await _db.ExchangeModeValues.AnyAsync(x => x.ModId == vm.ModId))
            {
                ViewBag.Modlar = await _db.Set<Modes>().AsNoTracking().ToListAsync();
                ModelState.AddModelError(string.Empty, "Aynı modda 2 tane ekleyemezsiniz.");
                return View(vm);
            }

            if (await _db.ExchangeModeValues.Where(x => x.SelectedMod).CountAsync() == 0 && !vm.SelectedMod)
            {
                ViewBag.Modlar = await _db.Set<Modes>().AsNoTracking().ToListAsync();
                ModelState.AddModelError(string.Empty, "En az bir mod varsayılan seçilmelidir");
                return View(vm);
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return RedirectToAction("Login", "User");

            var culture = new System.Globalization.CultureInfo("tr-TR");

            // String'leri decimal(18,4)'e çevir
            decimal TryParse(string s)
                => decimal.TryParse(s?.Replace(".", ","), System.Globalization.NumberStyles.Any, culture, out var d) ? d : 0;

            var model = new ExchangeModeValues
            {
                ModId = vm.ModId,

                GramAltinAlis = TryParse(vm.GramAltinAlis),
                GramAltinSatis = TryParse(vm.GramAltinSatis),
                AtaCeyrekAlis = TryParse(vm.AtaCeyrekAlis),
                AtaCeyrekSatis = TryParse(vm.AtaCeyrekSatis),
                AtaYarimAlis = TryParse(vm.AtaYarimAlis),
                AtaYarimSatis = TryParse(vm.AtaYarimSatis),
                AtaLiraAlis = TryParse(vm.AtaLiraAlis),
                AtaLiraSatis = TryParse(vm.AtaLiraSatis),
                YirmiIkiAyarAlis = TryParse(vm.YirmiIkiAyarAlis),
                YirmiIkiAyarSatis = TryParse(vm.YirmiIkiAyarSatis),
                ZiynetCeyrekAlis = TryParse(vm.ZiynetCeyrekAlis),
                ZiynetCeyrekSatis = TryParse(vm.ZiynetCeyrekSatis),
                ZiynetYarimAlis = TryParse(vm.ZiynetYarimAlis),
                ZiynetYarimSatis = TryParse(vm.ZiynetYarimSatis),
                ZiynetLiraAlis = TryParse(vm.ZiynetLiraAlis),
                ZiynetLiraSatis = TryParse(vm.ZiynetLiraSatis),

                CreatedDate = DateTime.Now,
                CreatedBy = userId,
                SelectedMod = vm.SelectedMod
            };

            await _db.ExchangeModeValues.AddAsync(model);
            await _db.SaveChangesAsync();

            if (await _db.ExchangeModeValues.AnyAsync(x => x.Id != model.Id && x.SelectedMod))
            {
                var mods = await _db.ExchangeModeValues.Where(x => x.Id != model.Id && x.SelectedMod).ToListAsync();
                foreach (var item in mods)
                {
                    item.SelectedMod = false;
                    _db.ExchangeModeValues.Update(item);
                    await _db.SaveChangesAsync();
                }
            }

            TempData["Success"] = "Döviz modu başarıyla eklendi.";
            return RedirectToAction(nameof(DovizModlari));
        }

        [HttpGet]
        public async Task<IActionResult> DovizModDuzenle(int id)
        {
            var entity = await _db.ExchangeModeValues.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return RedirectToAction(nameof(DovizModlari));

            var tr = new System.Globalization.CultureInfo("tr-TR");
            string F(decimal d) => d.ToString("0.####", tr); // virgüllü yazdır

            var vm = new Rikud.WebAdmin.ViewModels.ExcModValVm
            {
                Id = entity.Id,
                ModId = entity.ModId,

                GramAltinAlis = F(entity.GramAltinAlis),
                GramAltinSatis = F(entity.GramAltinSatis),
                AtaCeyrekAlis = F(entity.AtaCeyrekAlis),
                AtaCeyrekSatis = F(entity.AtaCeyrekSatis),
                AtaYarimAlis = F(entity.AtaYarimAlis),
                AtaYarimSatis = F(entity.AtaYarimSatis),
                AtaLiraAlis = F(entity.AtaLiraAlis),
                AtaLiraSatis = F(entity.AtaLiraSatis),
                YirmiIkiAyarAlis = F(entity.YirmiIkiAyarAlis),
                YirmiIkiAyarSatis = F(entity.YirmiIkiAyarSatis),
                ZiynetCeyrekAlis = F(entity.ZiynetCeyrekAlis),
                ZiynetCeyrekSatis = F(entity.ZiynetCeyrekSatis),
                ZiynetYarimAlis = F(entity.ZiynetYarimAlis),
                ZiynetYarimSatis = F(entity.ZiynetYarimSatis),
                ZiynetLiraAlis = F(entity.ZiynetLiraAlis),
                ZiynetLiraSatis = F(entity.ZiynetLiraSatis),

                SelectedMod = entity.SelectedMod,
                CreatedDate = entity.CreatedDate,
                CreatedBy = entity.CreatedBy,
                CreatedByStr = await _db.Users
                                        .Where(u => u.Id == entity.CreatedBy)
                                        .Select(u => u.Username)
                                        .FirstOrDefaultAsync() ?? "-",
                ModAdi = await _db.Mods
                                  .Where(m => m.Id == entity.ModId)
                                  .Select(m => m.ModeName)
                                  .FirstOrDefaultAsync() ?? "-"
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DovizModDuzenle(ExcModValVm vm)
        {
            if (!ModelState.IsValid)
            {
                // Mod/Kullanıcı adları tekrar dolu gelsin
                vm.ModAdi = await _db.Mods.Where(m => m.Id == vm.ModId).Select(m => m.ModeName).FirstOrDefaultAsync() ?? "-";
                vm.CreatedByStr = await _db.Users.Where(u => u.Id == vm.CreatedBy).Select(u => u.Username).FirstOrDefaultAsync() ?? "-";
                return View(vm);
            }


            if (await _db.ExchangeModeValues.Where(x => x.SelectedMod).CountAsync() == 0 && !vm.SelectedMod)
            {
                ViewBag.Modlar = await _db.Set<Modes>().AsNoTracking().ToListAsync();
                ModelState.AddModelError(string.Empty, "En az bir mod varsayılan seçilmelidir");
                return View(vm);
            }

            var entity = await _db.ExchangeModeValues.FirstOrDefaultAsync(x => x.Id == vm.Id);
            if (entity == null) return RedirectToAction(nameof(DovizModlari));

            // virgül/nokta güvenli parse
            decimal P(string s)
            {
                var tr = new System.Globalization.CultureInfo("tr-TR");
                if (decimal.TryParse((s ?? "").Replace(".", ","), System.Globalization.NumberStyles.Any, tr, out var d))
                    return decimal.Round(d, 4);
                return 0m;
            }

            // ModId ve Created* değişmiyor
            entity.GramAltinAlis = P(vm.GramAltinAlis);
            entity.GramAltinSatis = P(vm.GramAltinSatis);
            entity.AtaCeyrekAlis = P(vm.AtaCeyrekAlis);
            entity.AtaCeyrekSatis = P(vm.AtaCeyrekSatis);
            entity.AtaYarimAlis = P(vm.AtaYarimAlis);
            entity.AtaYarimSatis = P(vm.AtaYarimSatis);
            entity.AtaLiraAlis = P(vm.AtaLiraAlis);
            entity.AtaLiraSatis = P(vm.AtaLiraSatis);
            entity.YirmiIkiAyarAlis = P(vm.YirmiIkiAyarAlis);
            entity.YirmiIkiAyarSatis = P(vm.YirmiIkiAyarSatis);
            entity.ZiynetCeyrekAlis = P(vm.ZiynetCeyrekAlis);
            entity.ZiynetCeyrekSatis = P(vm.ZiynetCeyrekSatis);
            entity.ZiynetYarimAlis = P(vm.ZiynetYarimAlis);
            entity.ZiynetYarimSatis = P(vm.ZiynetYarimSatis);
            entity.ZiynetLiraAlis = P(vm.ZiynetLiraAlis);
            entity.ZiynetLiraSatis = P(vm.ZiynetLiraSatis);
            entity.SelectedMod = vm.SelectedMod;
            await _db.SaveChangesAsync();

            if (await _db.ExchangeModeValues.AnyAsync(x => x.Id != entity.Id && x.SelectedMod))
            {
                var mods = await _db.ExchangeModeValues.Where(x => x.Id != entity.Id && x.SelectedMod).ToListAsync();
                foreach (var item in mods)
                {
                    item.SelectedMod = false;
                    _db.ExchangeModeValues.Update(item);
                    await _db.SaveChangesAsync();
                }
            }

            TempData["Success"] = "Döviz modu güncellendi.";
            return RedirectToAction(nameof(DovizModlari));
        }

        public async Task<IActionResult> DovizModSil(int Id)
        {
            if (!await _db.ExchangeModeValues.AnyAsync(x => x.Id == Id))
                return View(nameof(DovizModlari));

            var exchangeModeValues = _db.ExchangeModeValues.FirstOrDefault(x => x.Id == Id);
            _db.ExchangeModeValues.Remove(exchangeModeValues);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(DovizModlari));
        }

        public JsonResult ModDefaultKaydet([FromBody] SiraList siraList)
        {
            List<bool> ints = new List<bool>();

            if (siraList.siraList.Where(x => x.DefaultChecked).Count() <= 0)
            {
                return Json("Mutalaka 1 adet varsayılan olmalıdır. Lütfen kontrol ediniz");
            }
            if (siraList.siraList.Where(x => x.DefaultChecked).Count() > 1)
            {
                return Json("Aynı anda birden fazla vasayılan seçilemez. Lütfen kontrol ediniz");
            }

            foreach (var item in siraList.siraList)
            {
                var sira = _db.ExchangeModeValues.FirstOrDefault(x => x.Id == item.Id);
                sira.SelectedMod = item.DefaultChecked;
                _db.ExchangeModeValues.Update(sira);
                _db.SaveChanges();
            }

            return Json("");
        }

        public class SiraList
        {
            public List<siraList> siraList { get; set; }

        }

        public class siraList
        {
            public int Id { get; set; }
            public bool DefaultChecked { get; set; }
        }


        public async Task<IActionResult> Duyurular()
        {
            ViewBag.DuyuruList = await _db.Announcements.AsNoTracking().ToListAsync();
            return View();

        }

        public async Task<IActionResult> _DuyuruList()
        {
            var announcements = await _db.Announcements.OrderBy(x => x.SortedBy).AsNoTracking().ToListAsync();

            return Json(announcements);
        }

        [HttpPost]
        public JsonResult DuyuruSırayiKaydet([FromBody] DuyuruList duyuruList)
        {
            List<int> ints = new List<int>();
            foreach (var item in duyuruList.duyuruList)
            {
                if (Convert.ToInt32(item.SortedBy) <= 0)
                {
                    return Json("Order 0 ve altında verilemez. Lütfen kontrol ediniz");
                }
                if (ints.Contains(Convert.ToInt32(item.SortedBy)))
                {
                    return Json("Aynı order verilmiş. Lütfen kontrol ediniz");
                }
                else
                {
                    ints.Add(Convert.ToInt32(item.SortedBy));
                }
            }
            foreach (var item in duyuruList.duyuruList)
            {
                var duyuru = _db.Announcements.FirstOrDefault(x => x.Id == item.Id);
                duyuru.SortedBy = Convert.ToInt32(item.SortedBy);
                _db.Announcements.Update(duyuru);
                _db.SaveChanges();
            }

            return Json("");
        }

        public class DuyuruList
        {
            public List<duyuruList> duyuruList { get; set; }

        }

        public class duyuruList
        {
            public int Id { get; set; }
            public string SortedBy { get; set; }
        }

        public Task<IActionResult> DuyuruEkle()
        {
            return Task.FromResult<IActionResult>(View(new AnnouncementsVm()));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DuyuruEkle(AnnouncementsVm model)
        {
            ModelState.Remove("SortedBy");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var entity = new Announcements
            {
                DuyuruText = model.DuyuruText,
                SortedBy = _db.Announcements.Count() + 1
            };

            _db.Announcements.Add(entity);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Duyurular));
        }

        public async Task<IActionResult> DuyuruDuzenle(int id)
        {
            var entity = await _db.Announcements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return RedirectToAction(nameof(Duyurular));

            return View(entity);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DuyuruDuzenle(Announcements model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Veritabanındaki orijinal kaydı bul
            var entity = await _db.Announcements.FirstOrDefaultAsync(x => x.Id == model.Id);
            if (entity == null)
            {
                return RedirectToAction(nameof(Duyurular));
            }

            // Güncellenebilir alanlar
            entity.DuyuruText = model.DuyuruText;
            await _db.SaveChangesAsync();

            TempData["Success"] = "Duyuru başarıyla güncellendi.";
            return RedirectToAction(nameof(Duyurular));
        }

        public async Task<IActionResult> DuyuruSil(int Id)
        {
            if (!await _db.Announcements.AnyAsync(x => x.Id == Id))
                return View(nameof(Duyurular));

            var duyuru = _db.Announcements.FirstOrDefault(x => x.Id == Id);
            _db.Announcements.Remove(duyuru);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Duyurular));
        }

        public Task<IActionResult> DovizGecmisi()
        {
            DateTime startDate = DateTime.Now.Date;
            ViewBag.StartDate = startDate.ToString("dd.MM.yyyy");
            return Task.FromResult<IActionResult>(View());
        }

        [HttpGet]
        public async Task<IActionResult> _ExchangeHistory(string date, int fromHour, int toHour)
        {
            // Validasyonlar
            if (string.IsNullOrWhiteSpace(date))
                return Json(new { ok = false, message = "Tarih zorunludur." });

            if (fromHour < 0 || fromHour > 23 || toHour < 1 || toHour > 24)
                return Json(new { ok = false, message = "Saat aralığı 00-23 (başlangıç) ve 01-24 (bitiş) arasında olmalıdır." });

            if (toHour - fromHour != 1)
                return Json(new { ok = false, message = "Saat aralığı tam 1 saat olmalıdır. (Örn: 10 → 11)" });

            // Tarih parse (yerel TR saati)
            if (!DateTime.TryParseExact(date, "dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeLocal, out var day))
            {
                return Json(new { ok = false, message = "Tarih formatı geçersiz." });
            }

            // Aralık
            var start = new DateTime(day.Year, day.Month, day.Day, fromHour, 0, 0, DateTimeKind.Local);
            var end = new DateTime(day.Year, day.Month, day.Day, toHour, 0, 0, DateTimeKind.Local);

            // Sorgu: ExchangeHistroies (aralığa düşenler)
            var q = _db.ExchangeHistroies
                .AsNoTracking()
                .Where(x => x.CreatedDate >= start && x.CreatedDate < end)
                .OrderBy(x => x.CreatedDate);

            var items = await q
                .Select(x => new
                {
                    // Grid’de kullandıklarımız:
                    gramAltinAlis = x.GramAltinAlis,
                    gramAltinSatis = x.GramAltinSatis,
                    gramAltinSatisEFT = x.GramAltinSatisEFT,
                    ataCeyrekAlis = x.AtaCeyrekAlis,
                    ataCeyrekSatis = x.AtaCeyrekSatis,
                    ataCeyrekSatisEFT = x.AtaCeyrekSatisEFT,
                    ataYarimAlis = x.AtaYarimAlis,
                    ataYarimSatis = x.AtaYarimSatis,
                    ataYarimSatisEFT = x.AtaYarimSatisEFT,
                    ataLiraAlis = x.AtaLiraAlis,
                    ataLiraSatis = x.AtaLiraSatis,
                    ataLiraSatisEFT = x.AtaLiraSatisEFT,
                    yirmiIkiAyarAlis = x.YirmiIkiAyarAlis,
                    yirmiIkiAyarSatis = x.YirmiIkiAyarSatis,
                    yirmiIkiAyarSatisEFT = x.YirmiIkiAyarSatisEFT,
                    ziynetCeyrekAlis = x.ZiynetCeyrekAlis,
                    ziynetCeyrekSatis = x.ZiynetCeyrekSatis,
                    ziynetCeyrekSatisEFT = x.ZiynetCeyrekSatisEFT,
                    ziynetYarimAlis = x.ZiynetYarimAlis,
                    ziynetYarimSatis = x.ZiynetYarimSatis,
                    ziynetYarimSatisEFT = x.ZiynetYarimSatisEFT,
                    ziynetLiraAlis = x.ZiynetLiraAlis,
                    ziynetLiraSatis = x.ZiynetLiraSatis,
                    ziynetLiraSatisEFT = x.ZiynetLiraSatisEFT,
                    createdDateText = x.CreatedDate.ToString("dd.MM.yyyy HH:mm")
                })
                .ToListAsync();

            return Json(new
            {
                ok = true,
                count = items.Count,
                items
            });
        }


        public async Task<IActionResult> Parameterler()
        {
            return View(await _db.Parameters.FirstOrDefaultAsync(x => x.Id == 1));
        }


        // POST: Parameter/Parameterler
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Parameterler(Parameters model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Lütfen gerekli alanları doldurunuz.";
                return View(model);
            }

            try
            {
                var prm = await _db.Set<Parameters>().FirstOrDefaultAsync(x => x.Id == model.Id);
                if (prm == null)
                {
                    TempData["Error"] = "Parametre bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                // ParameterName değiştirilemez!
                prm.ParameterValue = model.ParameterValue.Trim();

                _db.Update(prm);
                await _db.SaveChangesAsync();

                TempData["Success"] = $"“{prm.ParameterName}” parametresi başarıyla güncellendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.";
                return View(model);
            }
        }



        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
