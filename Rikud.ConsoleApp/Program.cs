using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Rikud.ConsoleApp;
using SocketIOClient;
using SocketIOClient.Transport;
using System.Buffers;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public class Program
{
    // Zaman sınırı: veri bekleme
    private const int SourceWaitSeconds = 5;     // veri bekleme
    private const int LoopSeconds = 3;           // ana döngü aralığı (kuyumcu ekranı ~3 sn)
    private static readonly CancellationTokenSource _cts = new();
    private static readonly HttpClient _httpClient = new HttpClient();
    public static async Task<int> Main(string[] args)
    {
        // Tek instance kilidi.
        // WatchDog process'i Kill() ile öldürürse mutex "abandoned" kalır;
        // WaitOne o durumda AbandonedMutexException fırlatır. Bunu yakalamazsak
        // uygulama anında çöker ve WatchDog sonsuz restart döngüsüne girer.
        using var mutex = new System.Threading.Mutex(false, @"Global\RikudDovizWriter");
        bool hasMutex;
        try
        {
            hasMutex = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            // Önceki instance zorla öldürülmüş; bu instance kilidi devraldı.
            Console.WriteLine("Önceki instance abandoned mutex bıraktı; kilidi devralındı.");
            hasMutex = true;
        }

        if (!hasMutex)
        {
            Console.WriteLine("Zaten çalışıyor. Çıkılıyor.");
            return 0;
        }

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _cts.Cancel();
        };

        try
        {
            var opt = new DbContextOptionsBuilder<RikudDbContext>()
                .UseSqlServer("Data Source=localhost;Persist Security Info=True;initial catalog=RIKUDDB;User ID=user_rikud;Password=wr1He7r_ph5trl@tIk;Pooling=True;Min Pool Size=1;Max Pool Size=20;MultipleActiveResultSets=False;Connect Timeout=15;Encrypt=True;TrustServerCertificate=True;",
                     b => b.CommandTimeout(30))
                .Options;

            using var db = new RikudDbContext(opt);

            // Parametreyi bellekte tut, her 30 sn'de bir yenile
            int altinTipId = await GetAltinTipIdAsync(db);
            var lastTypeRefresh = DateTime.UtcNow;
            var lastHistoryWrite = DateTime.MinValue;

            Console.WriteLine($"Sürekli döngü başlıyor. AltinTipId={altinTipId}");

            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    // Parametreyi her 30 sn'de bir tazele
                    if ((DateTime.UtcNow - lastTypeRefresh).TotalSeconds >= 30)
                    {
                        altinTipId = await GetAltinTipIdAsync(db);
                        lastTypeRefresh = DateTime.UtcNow;
                    }

                    // Kaynaktan snapshot (5 sn timeout), olmazsa fallback
                    using var tmo = new CancellationTokenSource(TimeSpan.FromSeconds(SourceWaitSeconds));
                    (decimal Alis, decimal Satis)? snap =
                        altinTipId == 2
                            ? await TryGetOnceFromHaremAsyncWss(tmo.Token)
                            : await TryGetOnceFromHaremAsync(tmo.Token);

                    if (snap is null)
                    {
                        var last = await db.Exchanges
                            .AsNoTracking()
                            .Where(x => x.AltinTipId == altinTipId)
                            .OrderByDescending(x => x.CreatedDate)
                            .Select(x => new { x.Alis, x.Satis })
                            .FirstOrDefaultAsync(_cts.Token);

                        if (last is null)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Snapshot yok, fallback da yok. Atlanıyor.");
                            await DelayAlignedAsync(_cts.Token);
                            continue;
                        }
                        snap = (last.Alis, last.Satis);
                    }

                    // Canlı fiyat: her döngüde yaz (ekranlar bunu okur)
                    var sql = @"
INSERT INTO Dovizler (AltinTipId, Alis, Satis, CreatedDate)
VALUES (@AltinTipId, @Alis, @Satis, GETDATE());
";

                    await db.Database.ExecuteSqlRawAsync(
                        sql,
                        new SqlParameter("@AltinTipId", altinTipId),
                        new SqlParameter("@Alis", snap.Value.Alis),
                        new SqlParameter("@Satis", snap.Value.Satis));

                    // Geçmiş: 60 sn'de bir yaz. Her tick'te yazmak DovizGecmisi'ni
                    // günde ~8600 satır şişiriyordu (ve satır çok geniş).
                    if ((DateTime.UtcNow - lastHistoryWrite).TotalSeconds >= 60)
                    {
                        var mode = await db.ExchangeModeValues
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x => x.SelectedMod, _cts.Token);

                        if (mode is not null)
                        {
                            decimal F(decimal c, decimal v) => Math.Round(c * v, 4, MidpointRounding.AwayFromZero);
                            decimal SatFromEft(decimal eft) => Math.Round(eft / (1 - 0.05m), 4, MidpointRounding.AwayFromZero);

                            var guncelAlis = snap.Value.Alis;
                            var guncelSatis = snap.Value.Satis;

                            var gram_eft = F(mode.GramAltinSatis, guncelSatis);
                            var atacey_eft = F(mode.AtaCeyrekSatis, guncelSatis);
                            var atayar_eft = F(mode.AtaYarimSatis, guncelSatis);
                            var atalira_eft = F(mode.AtaLiraSatis, guncelSatis);
                            var ayar22_eft = F(mode.YirmiIkiAyarSatis, guncelSatis);
                            var ziycey_eft = F(mode.ZiynetCeyrekSatis, guncelSatis);
                            var ziyyar_eft = F(mode.ZiynetYarimSatis, guncelSatis);
                            var zylira_eft = F(mode.ZiynetLiraSatis, guncelSatis);

                            var hist = new ExchangeHistroies
                            {
                                ModId = mode.Id,
                                GramAltinAlis = F(mode.GramAltinAlis, guncelAlis),
                                GramAltinSatisEFT = gram_eft,
                                GramAltinSatis = SatFromEft(gram_eft),
                                AtaCeyrekAlis = F(mode.AtaCeyrekAlis, guncelAlis),
                                AtaCeyrekSatisEFT = atacey_eft,
                                AtaCeyrekSatis = SatFromEft(atacey_eft),
                                AtaYarimAlis = F(mode.AtaYarimAlis, guncelAlis),
                                AtaYarimSatisEFT = atayar_eft,
                                AtaYarimSatis = SatFromEft(atayar_eft),
                                AtaLiraAlis = F(mode.AtaLiraAlis, guncelAlis),
                                AtaLiraSatisEFT = atalira_eft,
                                AtaLiraSatis = SatFromEft(atalira_eft),
                                YirmiIkiAyarAlis = F(mode.YirmiIkiAyarAlis, guncelAlis),
                                YirmiIkiAyarSatisEFT = ayar22_eft,
                                YirmiIkiAyarSatis = SatFromEft(ayar22_eft),
                                ZiynetCeyrekAlis = F(mode.ZiynetCeyrekAlis, guncelAlis),
                                ZiynetCeyrekSatisEFT = ziycey_eft,
                                ZiynetCeyrekSatis = SatFromEft(ziycey_eft),
                                ZiynetYarimAlis = F(mode.ZiynetYarimAlis, guncelAlis),
                                ZiynetYarimSatisEFT = ziyyar_eft,
                                ZiynetYarimSatis = SatFromEft(ziyyar_eft),
                                ZiynetLiraAlis = F(mode.ZiynetLiraAlis, guncelAlis),
                                ZiynetLiraSatisEFT = zylira_eft,
                                ZiynetLiraSatis = SatFromEft(zylira_eft),
                                CreatedDate = DateTime.Now
                            };

                            db.Set<ExchangeHistroies>().Add(hist);
                            await db.SaveChangesAsync(_cts.Token);
                            lastHistoryWrite = DateTime.UtcNow;
                        }
                    }

                    // Uzun ömürlü context: her döngüde change-tracker'ı temizle.
                    db.ChangeTracker.Clear();
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("Hata: " + ex.Message);
                }

                // 10 sn hizalı bekleme
                await DelayAlignedAsync(_cts.Token);
            }

            Console.WriteLine("Döngü durduruldu.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    private static async Task DelayAlignedAsync(CancellationToken ct)
    {
        // 10 saniyelik hizalama: bir sonraki 0-10-20-30-40-50 saniye başına kadar bekle
        var now = DateTime.UtcNow;
        int nextSlot = ((now.Second / LoopSeconds) + 1) * LoopSeconds;
        if (nextSlot >= 60) nextSlot = 0;

        var next = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, nextSlot, 0, DateTimeKind.Utc);
        if (next <= now) next = next.AddMinutes(1);

        var delay = next - now;
        if (delay < TimeSpan.FromMilliseconds(200)) delay = TimeSpan.FromMilliseconds(200);
        await Task.Delay(delay, ct);
    }

    private static async Task<int> GetAltinTipIdAsync(RikudDbContext db)
    {
        var prm = await db.Parameters
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ParameterName == "DataUrlType");

        if (prm == null) return 1;
        if (int.TryParse(prm.ParameterValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var t)
            && (t == 1 || t == 2))
            return t;

        return 1;
    }

    #region Harem (Socket.IO)

    public static async Task<(decimal Alis, decimal Satis)?> TryGetOnceFromHaremAsyncWss(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<(decimal, decimal)>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Socket.IO Ayarları
        var client = new SocketIOClient.SocketIO("wss://hrmsocketonly.haremaltin.com", new SocketIOClient.SocketIOOptions
        {
            Path = "/socket.io",
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
            Reconnection = false,
            // Harem Altın genelde eski protokol kullanabiliyor, V4 çalışmazsa V3 deneyin.
            EIO = SocketIO.Core.EngineIO.V4,
            ConnectionTimeout = TimeSpan.FromSeconds(5),
            ExtraHeaders = new Dictionary<string, string>
    {
        { "Origin", "https://www.haremaltin.com" },
        { "User-Agent", "Mozilla/5.0" }
    }
        });

        client.OnConnected += (sender, e) => Console.WriteLine("Socket Bağlandı: " + client.Id);

        // --- KRİTİK DÜZELTME BURADA ---
        client.On("price_changed", response =>
        {
            try
            {
                // 1. Kök veriyi al
                var root = response.GetValue<System.Text.Json.JsonElement>();

                // 2. "data" özelliği var mı?
                if (!root.TryGetProperty("data", out var dataEl)) return;

                // 3. Javascript'teki "for (var code in data)" mantığı:
                // Data bir Obje (Dictionary) gibi geliyor, içindeki tüm property'leri gezmeliyiz.
                if (dataEl.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var item in dataEl.EnumerateObject())
                    {
                        var urunDetay = item.Value; // Bu "ALTIN", "USDTRY" vb. objesidir.

                        // Kodun "ALTIN" olup olmadığını kontrol et
                        // (Bazen key "ALTIN" olur, bazen içerideki "code" property'si)
                        bool isGold = (item.Name == "ALTIN");

                        if (!isGold && urunDetay.TryGetProperty("code", out var codeProp))
                        {
                            if (codeProp.GetString() == "ALTIN") isGold = true;
                        }

                        if (isGold)
                        {
                            decimal alis = 0, satis = 0;

                            // String olarak gelen sayıları güvenli parse etme
                            if (urunDetay.TryGetProperty("alis", out var alEl))
                                decimal.TryParse(alEl.GetString()?.Replace(".", ","), out alis); // Türkçe culture safe

                            if (urunDetay.TryGetProperty("satis", out var saEl))
                                decimal.TryParse(saEl.GetString()?.Replace(".", ","), out satis);

                            // Sonucu bulduk, görevi tamamla
                            if (alis > 0 || satis > 0)
                            {
                                // TrySetResult thread-safe şekilde task'ı bitirir
                                tcs.TrySetResult((alis, satis));
                                return; // Döngüden çık
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Parse Hatası: " + ex.Message);
            }
        });

        try
        {
            await client.ConnectAsync();

            // CancellationToken tetiklenirse TCS'yi iptal et
            using (ct.Register(() => tcs.TrySetCanceled()))
            {
                // Veri gelene kadar veya token iptal olana kadar bekle
                var result = await tcs.Task;
                return result;
            }
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Zaman aşımı: İstenen veri (ALTIN) belirtilen sürede gelmedi.");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Genel Hata: " + ex.Message);
            return null;
        }
        finally
        {
            if (client.Connected) await client.DisconnectAsync();
            client.Dispose();
        }
    }

    public static async Task<(decimal Alis, decimal Satis)?> TryGetOnceFromHaremAsync(CancellationToken ct)
    {
        // Yeni REST API Adresi
        string url = "https://rest.datshop.com.tr/api/prices?socket_key=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";

        try
        {
            // 1. API'den veriyi çek (GET isteği)
            var response = await _httpClient.GetAsync(url, ct);

            // Bağlantı başarılı değilse hata fırlatır (Örn: 404, 500)
            response.EnsureSuccessStatusCode();

            // 2. JSON içeriğini string olarak oku
            var jsonString = await response.Content.ReadAsStringAsync(ct);

            // 3. JSON'ı parse et
            using (var doc = JsonDocument.Parse(jsonString))
            {
                var root = doc.RootElement;

                // "data" dizisine eriş
                if (root.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
                {
                    // Dizideki her bir ürünü gez
                    foreach (var item in dataArray.EnumerateArray())
                    {
                        // Symbol değeri "ALTIN" olanı bul
                        if (item.TryGetProperty("symbol", out var symbolProp) &&
                            symbolProp.GetString() == "ALTIN")
                        {
                            decimal alis = 0;
                            decimal satis = 0;

                            // JSON'da "bid" -> Alış
                            if (item.TryGetProperty("bid", out var bidProp))
                            {
                                // API direkt sayı döndüğü için parse etmeye gerek yok, GetDecimal yeterli
                                alis = bidProp.GetDecimal();
                            }

                            // JSON'da "ask" -> Satış
                            if (item.TryGetProperty("ask", out var askProp))
                            {
                                satis = askProp.GetDecimal();
                            }

                            // Değerler alındıysa döndür
                            return (alis, satis);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"API Hatası: {ex.Message}");
        }

        // Veri bulunamazsa veya hata olursa null dön
        return null;
    }


    #endregion

    #region Rona (WebSocket)

    private sealed class RonaParityItem
    {
        public string Category { get; set; } = "";
        public int ProductId { get; set; }
        public string Parity { get; set; } = "";
        public decimal Price1 { get; set; }
        public decimal Price2 { get; set; }
        public string Description { get; set; } = "";
        public double Rate { get; set; }
        public string Timestamp { get; set; } = "";
        public int AfterDotCount { get; set; }
    }

    private static async Task<(decimal Alis, decimal Satis)?> TryGetOnceFromRonaAsync(CancellationToken ct)
    {
        using var ws = new ClientWebSocket();
        try
        {
            await ws.ConnectAsync(new Uri("wss://ronaapi.codeflight.com.tr/ws"), ct);

            var buffer = ArrayPool<byte>.Shared.Rent(8192);
            try
            {
                var seg = new ArraySegment<byte>(buffer);
                using var ms = new System.IO.MemoryStream();

                while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    ms.SetLength(0);
                    WebSocketReceiveResult? result;
                    do
                    {
                        result = await ws.ReceiveAsync(seg, ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                            return null;
                        ms.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var json = Encoding.UTF8.GetString(ms.ToArray());
                        var opt = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            NumberHandling = JsonNumberHandling.AllowReadingFromString
                        };
                        var list = JsonSerializer.Deserialize<System.Collections.Generic.List<RonaParityItem>>(json, opt);
                        if (list is null || list.Count == 0) continue;

                        var hasTry = list.FirstOrDefault(x =>
                            string.Equals(x.Category, "03-ALTIN", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(x.Parity, "HAS/TRY", StringComparison.OrdinalIgnoreCase));

                        if (hasTry is not null)
                        {
                            var alis = hasTry.Price1;
                            var satis = hasTry.Price2;
                            if (alis > 0 || satis > 0)
                                return (alis, satis);
                        }
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return null;
        }
        catch (Exception ex) { Console.WriteLine("hata : " + ex.Message); return null; }
        finally
        {
            try
            {
                if (ws.State == WebSocketState.Open)
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch { }
        }
    }

    #endregion

    #region Helpers

    private static decimal ParseDecimalSafe(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0m;
        value = value.Replace(".", ",");
        if (decimal.TryParse(value, NumberStyles.Any, new CultureInfo("tr-TR"), out var d)) return d;
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d;
        return 0m;
    }

    private static void TrySetResultOnce(TaskCompletionSource<(decimal, decimal)> tcs, (decimal, decimal) val)
    {
        if (!tcs.Task.IsCompleted) tcs.TrySetResult(val);
    }
    private static void TrySetCanceledOnce(TaskCompletionSource<(decimal, decimal)> tcs)
    {
        if (!tcs.Task.IsCompleted) tcs.TrySetCanceled();
    }

    #endregion
}