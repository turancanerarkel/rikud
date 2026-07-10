using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Rikud.DLL.AppDbContext;
using Rikud.DLL.Models;
using Rikud.WebUI.Models;
using System.Diagnostics;
using System.Security.Claims;

namespace Rikud.WebUI.Controllers
{
    [Authorize(AuthenticationSchemes = "RikudUI")]
    public class HomeController(RikudDbContext _db) : Controller
    {
        public async Task<IActionResult> Index()
        {
            var companyName = HttpContext.User.FindFirst(ClaimTypes.Country)?.Value;
            ViewBag.CompanyName = companyName;

            var duyurular = await _db.Announcements
       .AsNoTracking()
       .OrderBy(x => x.SortedBy)
       .Select(x => x.DuyuruText)
       .ToListAsync();

            ViewBag.DuyuruList = duyurular;

            return View();
        }

        //[HttpGet]
        //public async Task<IActionResult> LivePrices()
        //{
        //    const decimal eftDiscount = 0.05m; // EFT’de indirim YOK; satış 0.95'e bölünür.


        //    var since = DateTime.Now.AddDays(-2); // örnek: son 2 gün


        //    var mode = await _db.ExchangeModeValues
        //        .AsNoTracking()
        //        .FirstOrDefaultAsync(x => x.SelectedMod);
        //    if (mode is null) return Json(new { ok = false, message = "Seçili mod yok." });

        //    var latest = await _db.Exchanges
        //.AsNoTracking()
        //.Where(x => x.CreatedDate >= since)
        //.GroupBy(x => x.AltinTipId)
        //.Select(g => g.OrderByDescending(x => x.CreatedDate).First())
        //.ToListAsync();

        //    if (latest.Count == 0) return Json(new { ok = false, message = "Exchange verisi yok." });

        //    Exchanges Fallback() => latest[0];

        //    int? idGram = null, idAtaCeyrek = null, idAtaYarim = null, idAtaLira = null,
        //         id22Ayar = null, idZiyCeyrek = null, idZiyYarim = null, idZiyLira = null;

        //    Exchanges Ex(int? tipId)
        //    {
        //        if (tipId.HasValue)
        //        {
        //            var found = latest.FirstOrDefault(x => x.AltinTipId == tipId.Value);
        //            if (found != null) return found;
        //        }
        //        return Fallback();
        //    }

        //    decimal R2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
        //    decimal Mul(decimal k, decimal p) => R2(k * p);

        //    // Alış/Satış (baz) – EFT için baz satış = indirim uygulanmamış "net" EFT fiyatı
        //    decimal Al(Exchanges ex, decimal coef) => Mul(coef, ex.Alis);
        //    decimal SatBase(Exchanges ex, decimal coef) => Mul(coef, ex.Satis); // EFT’de gösterilecek değer
        //    decimal SatListFromEFT(decimal satBase) => R2(satBase / (1m - eftDiscount)); // satış = EFT / 0.95

        //    var gramEx = Ex(idGram);
        //    var ataCyrEx = Ex(idAtaCeyrek);
        //    var ataYarEx = Ex(idAtaYarim);
        //    var ataLiraEx = Ex(idAtaLira);
        //    var ayar22Ex = Ex(id22Ayar);
        //    var ziyCyrEx = Ex(idZiyCeyrek);
        //    var ziyYarEx = Ex(idZiyYarim);
        //    var ziyLiraEx = Ex(idZiyLira);

        //    // Baz EFT satışları
        //    var gram_sat_eft = SatBase(gramEx, mode.GramAltinSatis);
        //    var atacey_sat_eft = SatBase(ataCyrEx, mode.AtaCeyrekSatis);
        //    var atayar_sat_eft = SatBase(ataYarEx, mode.AtaYarimSatis);
        //    var atalir_sat_eft = SatBase(ataLiraEx, mode.AtaLiraSatis);
        //    var bilezik_sat_eft = SatBase(ayar22Ex, mode.YirmiIkiAyarSatis);
        //    var ziycey_sat_eft = SatBase(ziyCyrEx, mode.ZiynetCeyrekSatis);
        //    var ziyyar_sat_eft = SatBase(ziyYarEx, mode.ZiynetYarimSatis);
        //    var zylira_sat_eft = SatBase(ziyLiraEx, mode.ZiynetLiraSatis);

        //    // Liste satışları (EFT / 0.95)
        //    var result = new
        //    {
        //        ok = true,

        //        gram = new
        //        {
        //            al = Al(gramEx, mode.GramAltinAlis),
        //            sat = SatListFromEFT(gram_sat_eft),          // satış = eft / 0.95
        //            sat_discount = gram_sat_eft                  // EFT: indirim yok
        //        },

        //        ataceyrek = new
        //        {
        //            al = Al(ataCyrEx, mode.AtaCeyrekAlis),
        //            sat = SatListFromEFT(atacey_sat_eft),
        //            sat_discount = atacey_sat_eft
        //        },

        //        atayarim = new
        //        {
        //            al = Al(ataYarEx, mode.AtaYarimAlis),
        //            sat = SatListFromEFT(atayar_sat_eft),
        //            sat_discount = atayar_sat_eft
        //        },

        //        atalira = new
        //        {
        //            al = Al(ataLiraEx, mode.AtaLiraAlis),
        //            sat = SatListFromEFT(atalir_sat_eft),
        //            sat_discount = atalir_sat_eft
        //        },

        //        bilezik = new
        //        {
        //            al = Al(ayar22Ex, mode.YirmiIkiAyarAlis),
        //            sat = SatListFromEFT(bilezik_sat_eft),
        //            sat_discount = bilezik_sat_eft
        //        },

        //        ziyceyrek = new
        //        {
        //            al = Al(ziyCyrEx, mode.ZiynetCeyrekAlis),
        //            sat = SatListFromEFT(ziycey_sat_eft),
        //            sat_discount = ziycey_sat_eft
        //        },

        //        ziyyarim = new
        //        {
        //            al = Al(ziyYarEx, mode.ZiynetYarimAlis),
        //            sat = SatListFromEFT(ziyyar_sat_eft),
        //            sat_discount = ziyyar_sat_eft
        //        },

        //        ziylira = new
        //        {
        //            al = Al(ziyLiraEx, mode.ZiynetLiraAlis),
        //            sat = SatListFromEFT(zylira_sat_eft),
        //            sat_discount = zylira_sat_eft
        //        }
        //    };

        //    return Json(result);
        //}


        [HttpGet]
        public async Task<IActionResult> LivePrices([FromServices] IMemoryCache cache)
        {
            // Veriyi cache'de arıyoruz. Yoksa veya 1 saniyelik süresi dolmuşsa içerdeki bloğa girip DB'den çekecek.
            var result = await cache.GetOrCreateAsync<object>("LivePricesData", async entry =>
            {
                // Cache 2 sn: DB'ye en fazla ~1 sorgu/2sn gider (paylaşımlı cache).
                // Frontend poll'ü de 2 sn olduğu için görsel gecikme olmaz.
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(2);

                const decimal eftDiscount = 0.05m;
                var since = DateTime.Now.AddDays(-2);

                var mode = await _db.ExchangeModeValues
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.SelectedMod);

                if (mode is null) return new { ok = false, message = "Seçili mod yok." };

                var latest = await _db.Exchanges
                    .AsNoTracking()
                    .Where(x => x.CreatedDate >= since)
                    .GroupBy(x => x.AltinTipId)
                    .Select(g => g.OrderByDescending(x => x.CreatedDate).First())
                    .ToListAsync();

                if (latest.Count == 0) return new { ok = false, message = "Exchange verisi yok." };

                Exchanges Fallback() => latest[0];

                // DİKKAT: Buradaki ID'leri veritabanındaki gerçek altın ID'lerinle değiştirmelisin!
                // Yoksa hepsi Fallback() değerini yani aynı fiyatı döner.
                int? idGram = 1, idAtaCeyrek = 2, idAtaYarim = 3, idAtaLira = 4,
                     id22Ayar = 5, idZiyCeyrek = 6, idZiyYarim = 7, idZiyLira = 8;

                Exchanges Ex(int? tipId)
                {
                    if (tipId.HasValue)
                    {
                        var found = latest.FirstOrDefault(x => x.AltinTipId == tipId.Value);
                        if (found != null) return found;
                    }
                    return Fallback();
                }

                decimal R2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
                decimal Mul(decimal k, decimal p) => R2(k * p);

                decimal Al(Exchanges ex, decimal coef) => Mul(coef, ex.Alis);
                decimal SatBase(Exchanges ex, decimal coef) => Mul(coef, ex.Satis);
                decimal SatListFromEFT(decimal satBase) => R2(satBase / (1m - eftDiscount));

                var gramEx = Ex(idGram);
                var ataCyrEx = Ex(idAtaCeyrek);
                var ataYarEx = Ex(idAtaYarim);
                var ataLiraEx = Ex(idAtaLira);
                var ayar22Ex = Ex(id22Ayar);
                var ziyCyrEx = Ex(idZiyCeyrek);
                var ziyYarEx = Ex(idZiyYarim);
                var ziyLiraEx = Ex(idZiyLira);

                // Baz EFT satışları
                var gram_sat_eft = SatBase(gramEx, mode.GramAltinSatis);
                var atacey_sat_eft = SatBase(ataCyrEx, mode.AtaCeyrekSatis);
                var atayar_sat_eft = SatBase(ataYarEx, mode.AtaYarimSatis);
                var atalir_sat_eft = SatBase(ataLiraEx, mode.AtaLiraSatis);
                var bilezik_sat_eft = SatBase(ayar22Ex, mode.YirmiIkiAyarSatis);
                var ziycey_sat_eft = SatBase(ziyCyrEx, mode.ZiynetCeyrekSatis);
                var ziyyar_sat_eft = SatBase(ziyYarEx, mode.ZiynetYarimSatis);
                var zylira_sat_eft = SatBase(ziyLiraEx, mode.ZiynetLiraSatis);

                // Hazırlanan veriyi döndür (Cache'e yazılacak)
                return new
                {
                    ok = true,
                    gram = new { al = Al(gramEx, mode.GramAltinAlis), sat = SatListFromEFT(gram_sat_eft), sat_discount = gram_sat_eft },
                    ataceyrek = new { al = Al(ataCyrEx, mode.AtaCeyrekAlis), sat = SatListFromEFT(atacey_sat_eft), sat_discount = atacey_sat_eft },
                    atayarim = new { al = Al(ataYarEx, mode.AtaYarimAlis), sat = SatListFromEFT(atayar_sat_eft), sat_discount = atayar_sat_eft },
                    atalira = new { al = Al(ataLiraEx, mode.AtaLiraAlis), sat = SatListFromEFT(atalir_sat_eft), sat_discount = atalir_sat_eft },
                    bilezik = new { al = Al(ayar22Ex, mode.YirmiIkiAyarAlis), sat = SatListFromEFT(bilezik_sat_eft), sat_discount = bilezik_sat_eft },
                    ziyceyrek = new { al = Al(ziyCyrEx, mode.ZiynetCeyrekAlis), sat = SatListFromEFT(ziycey_sat_eft), sat_discount = ziycey_sat_eft },
                    ziyyarim = new { al = Al(ziyYarEx, mode.ZiynetYarimAlis), sat = SatListFromEFT(ziyyar_sat_eft), sat_discount = ziyyar_sat_eft },
                    ziylira = new { al = Al(ziyLiraEx, mode.ZiynetLiraAlis), sat = SatListFromEFT(zylira_sat_eft), sat_discount = zylira_sat_eft }
                };
            });

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> PricesAt10([FromServices] IMemoryCache cache)
        {
            // Bu veri günde bir kez (10:00) değişir; 60 sn cache DB'yi rahatlatır, görünür gecikme olmaz.
            var result = await cache.GetOrCreateAsync<object>("PricesAt10Data", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);

                // 1) Seçili modu al (katsayılar burada)
                var mode = await _db.ExchangeModeValues
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.SelectedMod);

                if (mode is null)
                    return new { ok = false, message = "Seçili mod yok." };

                // 2) Yardımcılar
                DateTime At10(DateTime d) =>
                    new DateTime(d.Year, d.Month, d.Day, 10, 0, 0, DateTimeKind.Local);

                decimal R2(decimal v) =>
                    Math.Round(v, 2, MidpointRounding.AwayFromZero);

                // Verilen güne ait target'a (10:00) en yakın tek kayıt.
                // SARGABLE: aralık + OrderBy ile iki index seek (full scan / .Date CAST yok).
                async Task<Exchanges?> GetNearestSingleAsync(DateTime target)
                {
                    var dayStart = target.Date;
                    var dayEnd = dayStart.AddDays(1);

                    var before = await _db.Exchanges
                        .AsNoTracking()
                        .Where(x => x.CreatedDate >= dayStart && x.CreatedDate <= target)
                        .OrderByDescending(x => x.CreatedDate)
                        .FirstOrDefaultAsync();

                    var after = await _db.Exchanges
                        .AsNoTracking()
                        .Where(x => x.CreatedDate > target && x.CreatedDate < dayEnd)
                        .OrderBy(x => x.CreatedDate)
                        .FirstOrDefaultAsync();

                    if (before is null) return after;
                    if (after is null) return before;
                    return (target - before.CreatedDate) <= (after.CreatedDate - target) ? before : after;
                }

                var exToday = await GetNearestSingleAsync(At10(DateTime.Now));

                var todayDate = DateTime.Now.Date;

                var lastAvailableDate = await _db.Exchanges
                    .AsNoTracking()
                    .Where(x => x.CreatedDate < todayDate) // Bugünden eski kayıtlar (sargable, index kullanır)
                    .OrderByDescending(x => x.CreatedDate)
                    .Select(x => x.CreatedDate)
                    .FirstOrDefaultAsync();

                Exchanges? exYesterday = null;
                if (lastAvailableDate != default)
                    exYesterday = await GetNearestSingleAsync(At10(lastAvailableDate));

                if (exToday is null || exYesterday is null)
                    return new { ok = false, message = "10:00 verisi bulunamadı." };

                // Ortak hesaplayıcı: aynı katsayı ile (today.Satis vs yesterday.Satis) yüzde değişim
                object MakeChange(decimal coefSatis, Exchanges todayEx, Exchanges yestEx)
                {
                    var todayVal = R2(coefSatis * todayEx.Satis);
                    var yestVal = R2(coefSatis * yestEx.Satis);

                    if (yestVal == 0)
                        return new { pct = 0m, sign = (string)null };

                    var pct = Math.Round(((todayVal - yestVal) / yestVal) * 100m, 2, MidpointRounding.AwayFromZero);

                    string? sign = null;
                    if (pct > 0) sign = "positive";
                    else if (pct < 0) sign = "negative";

                    return new { pct, sign };
                }

                return new Dictionary<string, object>
                {
                    ["ok"] = true,
                    ["gram"] = MakeChange(mode.GramAltinSatis, exToday, exYesterday),
                    ["ataceyrek"] = MakeChange(mode.AtaCeyrekSatis, exToday, exYesterday),
                    ["atayarim"] = MakeChange(mode.AtaYarimSatis, exToday, exYesterday),
                    ["atalira"] = MakeChange(mode.AtaLiraSatis, exToday, exYesterday),
                    ["bilezik"] = MakeChange(mode.YirmiIkiAyarSatis, exToday, exYesterday),
                    ["ziyceyrek"] = MakeChange(mode.ZiynetCeyrekSatis, exToday, exYesterday),
                    ["ziyyarim"] = MakeChange(mode.ZiynetYarimSatis, exToday, exYesterday),
                    ["ziylira"] = MakeChange(mode.ZiynetLiraSatis, exToday, exYesterday),
                };
            });

            return Json(result);
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

        //public IActionResult LoginControl()
        //{
        //    string deger = HttpContext.Session.GetString("Logoff");
        //    string userId = HttpContext.Session.GetString("UserId");

        //    if (!string.IsNullOrEmpty(deger) && deger == "1" && userId == HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        //    {
        //        HttpContext.Session.Remove("Logoff");
        //        HttpContext.Session.Remove("UserId");
        //        return RedirectToAction("Logout", "User");
        //    }

        //    return Json("");
        //}

        // Anasayfadan 30 sn'de bir POST ile çağrılır. Geriye VERİ DÖNMEZ.
        // Login'de verdiğimiz claim'deki SessionKey'i alır. (Devamı: oturum kontrol mantığı eklenecek.)
        [HttpPost]
        public async Task<IActionResult> SessionPing()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var sessionKeyClaim = User.FindFirst("SessionKey")?.Value;

            if (int.TryParse(userIdClaim, out var userId) && Guid.TryParse(sessionKeyClaim, out var sessionKey))
            {
                // Kullanıcının izinli oturum adedi
                var allowed = await _db.Users
                    .Where(x => x.Id == userId)
                    .Select(x => x.SessionCount)
                    .FirstOrDefaultAsync();
                if (allowed < 1) allowed = 1;

                // UserSessions'ta bu kullanıcının mevcut oturum sayısı
                var sessionCount = await _db.UserSessions
                    .CountAsync(x => x.UserId == userId);

                // Sayı izinli adedin ÜSTÜNDEYSE devreye gir; altında/eşitse hiçbir şey yapma
                if (sessionCount > allowed)
                {
                    // Oturumların EN ESKİsi (ilki)
                    var oldest = await _db.UserSessions
                        .Where(x => x.UserId == userId)
                        .OrderBy(x => x.Id)
                        .FirstOrDefaultAsync();

                    // En eski oturum BU cihazsa (claim'deki SessionKey ile eşitse) bu cihazı logoff et
                    if (oldest != null && oldest.SessionKey == sessionKey)
                    {
                        _db.UserSessions.Remove(oldest);
                        await _db.SaveChangesAsync();
                        //await HttpContext.SignOutAsync("RikudUI");
                        return RedirectToAction("Logout", "User");
                    }
                }
            }

            return new EmptyResult(); // geriye veri dönmüyoruz
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

    }
}
