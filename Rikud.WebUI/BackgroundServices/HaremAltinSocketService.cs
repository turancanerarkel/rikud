using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Rikud.DLL.AppDbContext;
using Rikud.DLL.Models;
using SocketIOClient;
using SocketIOClient.Transport;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace Rikud.WebUI.BackgroundServices
{
    // ⚠️ KULLANILMIYOR: Bu servis Program.cs'te AddHostedService ile KAYITLI DEĞİL, çalışmıyor.
    // Aynı işi (Harem/Rona socket + 10 sn'de bir Dovizler/DovizGecmisi yazma) Rikud.ConsoleApp yapıyor.
    // Buraya AddHostedService EKLEMEYİN: ConsoleApp ile ÇİFT yazma + ikinci dış socket bağlantısı + ekstra DB yükü olur.
    // İleride ya tamamen silinmeli ya da ConsoleApp'in yerine geçmeli (ikisi birden asla).
    public sealed class HaremAltinSocketService(ILogger<HaremAltinSocketService> _logger, IMemoryCache _cache, IServiceScopeFactory _scopeFactory) : BackgroundService
    {
        // Harem için Socket.IO
        private SocketIOClient.SocketIO? _socket;

        // Rona için ClientWebSocket
        private ClientWebSocket? _ws;

        private readonly object _lock = new();
        private (decimal Alis, decimal Satis, DateTime Tarih)? _lastSnapshot;
        private int globalType = 1;
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Ana döngü: parametreyi izler, uygun veri kaynağını çalıştırır ve 10sn'de bir DB'ye yazar
            using var dbTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            using var paramTimer = new PeriodicTimer(TimeSpan.FromSeconds(60));

            var currentType = await GetDataUrlTypeAsync(stoppingToken);
            var dataCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            Task dataTask = currentType == 2
                ? RunRonaAsync(dataCts.Token)
                : RunHaremAsync(dataCts.Token);

            _logger.LogInformation("DataUrlType={Type} ile başlatıldı.", currentType);

            // BEKLEME TASK’LARINI DIŞARIDA OLUŞTUR
            Task<bool> dbTickTask = dbTimer.WaitForNextTickAsync(stoppingToken).AsTask();
            Task<bool> prmTickTask = paramTimer.WaitForNextTickAsync(stoppingToken).AsTask();

            while (!stoppingToken.IsCancellationRequested)
            {
                var completed = await Task.WhenAny(dbTickTask, prmTickTask);

                if (completed == dbTickTask)
                {
                    // timer dispose/iptal durumunda false dönebilir
                    if (!await dbTickTask) break;

                    // === 10 sn’de bir DB yaz ===
                    try
                    {
                        (decimal Alis, decimal Satis, DateTime Tarih)? snap;
                        lock (_lock) snap = _lastSnapshot;

                        var currentType2 = await GetDataUrlTypeAsync(stoppingToken);
                        globalType = currentType;

                        if (snap is not null)
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<RikudDbContext>();

                            var entity = new Exchanges
                            {
                                AltinTipId = globalType,
                                Alis = snap.Value.Alis,
                                Satis = snap.Value.Satis,
                                CreatedDate = DateTime.Now
                            };
                            db.Set<Exchanges>().Add(entity);
                            await db.SaveChangesAsync(stoppingToken);

                            var mode = await db.ExchangeModeValues.AsNoTracking()
                                .FirstOrDefaultAsync(x => x.SelectedMod, stoppingToken);

                            if (mode is not null)
                            {
                                decimal F(decimal c, decimal v) => Math.Round(c * v, 4, MidpointRounding.AwayFromZero);
                                decimal SatFromEft(decimal eft) => Math.Round(eft / (1 - 0.05m), 4, MidpointRounding.AwayFromZero);

                                var gram_eft = F(mode.GramAltinSatis, entity.Satis);
                                var atacey_eft = F(mode.AtaCeyrekSatis, entity.Satis);
                                var atayar_eft = F(mode.AtaYarimSatis, entity.Satis);
                                var atalira_eft = F(mode.AtaLiraSatis, entity.Satis);
                                var ayar22_eft = F(mode.YirmiIkiAyarSatis, entity.Satis);
                                var ziycey_eft = F(mode.ZiynetCeyrekSatis, entity.Satis);
                                var ziyyar_eft = F(mode.ZiynetYarimSatis, entity.Satis);
                                var zylira_eft = F(mode.ZiynetLiraSatis, entity.Satis);

                                var hist = new ExchangeHistroies
                                {
                                    ModId = mode.Id,
                                    GramAltinAlis = F(mode.GramAltinAlis, entity.Alis),
                                    GramAltinSatisEFT = gram_eft,
                                    GramAltinSatis = SatFromEft(gram_eft),
                                    AtaCeyrekAlis = F(mode.AtaCeyrekAlis, entity.Alis),
                                    AtaCeyrekSatisEFT = atacey_eft,
                                    AtaCeyrekSatis = SatFromEft(atacey_eft),
                                    AtaYarimAlis = F(mode.AtaYarimAlis, entity.Alis),
                                    AtaYarimSatisEFT = atayar_eft,
                                    AtaYarimSatis = SatFromEft(atayar_eft),
                                    AtaLiraAlis = F(mode.AtaLiraAlis, entity.Alis),
                                    AtaLiraSatisEFT = atalira_eft,
                                    AtaLiraSatis = SatFromEft(atalira_eft),
                                    YirmiIkiAyarAlis = F(mode.YirmiIkiAyarAlis, entity.Alis),
                                    YirmiIkiAyarSatisEFT = ayar22_eft,
                                    YirmiIkiAyarSatis = SatFromEft(ayar22_eft),
                                    ZiynetCeyrekAlis = F(mode.ZiynetCeyrekAlis, entity.Alis),
                                    ZiynetCeyrekSatisEFT = ziycey_eft,
                                    ZiynetCeyrekSatis = SatFromEft(ziycey_eft),
                                    ZiynetYarimAlis = F(mode.ZiynetYarimAlis, entity.Alis),
                                    ZiynetYarimSatisEFT = ziyyar_eft,
                                    ZiynetYarimSatis = SatFromEft(ziyyar_eft),
                                    ZiynetLiraAlis = F(mode.ZiynetLiraAlis, entity.Alis),
                                    ZiynetLiraSatisEFT = zylira_eft,
                                    ZiynetLiraSatis = SatFromEft(zylira_eft),
                                    CreatedDate = DateTime.Now
                                };

                                db.Set<ExchangeHistroies>().Add(hist);
                                await db.SaveChangesAsync(stoppingToken);
                            }
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "DB kaydı sırasında hata.");
                    }

                    // SADECE TAMAMLANAN TARAF İÇİN YENİ BEKLEME OLUŞTUR
                    dbTickTask = dbTimer.WaitForNextTickAsync(stoppingToken).AsTask();
                }
                else // parametre kontrol tick'i tamamlandı
                {
                    if (!await prmTickTask) break;

                    try
                    {
                        var newType = await GetDataUrlTypeAsync(stoppingToken);
                        if (newType != currentType)
                        {
                            _logger.LogWarning("DataUrlType değişti: {Old} -> {New}. Kaynak yeniden başlatılıyor.", currentType, newType);
                            currentType = newType;

                            // Eski akışı durdur
                            try { dataCts.Cancel(); } catch { }
                            try { await dataTask; } catch { }

                            await CloseRonaAsync();
                            await CloseHaremAsync();

                            // Yeni akışı başlat
                            dataCts.Dispose();
                            dataCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                            dataTask = (currentType == 2)
                                ? RunRonaAsync(dataCts.Token)
                                : RunHaremAsync(dataCts.Token);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Parametre kontrolünde hata.");
                    }

                    // SADECE TAMAMLANAN TARAF İÇİN YENİ BEKLEME OLUŞTUR
                    prmTickTask = paramTimer.WaitForNextTickAsync(stoppingToken).AsTask();
                }
            }

            // kapanış
            try { await dataTask; } catch { }
            await CloseRonaAsync();
            await CloseHaremAsync();
            dataCts.Dispose();
        }

        private async Task RunHaremAsync(CancellationToken ct)
        {
            await BuildAndConnectHaremAsync(ct);

            // Harem tarafında tüm iş olay dinleyicide yapılır.
            // Bağlantı koparsa OnDisconnected içinde reconnect akışı tetiklenir.
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(500, ct);
            }
        }

        private async Task BuildAndConnectHaremAsync(CancellationToken ct)
        {
            var sock = new SocketIOClient.SocketIO("https://api.haremaltin.com", new SocketIOOptions
            {
                Transport = TransportProtocol.WebSocket,
                Reconnection = false,
                EIO = SocketIO.Core.EngineIO.V4,
                ConnectionTimeout = TimeSpan.FromSeconds(10),
            });

            sock.OnConnected += (_, __) => _logger.LogInformation("HaremAltin: Bağlandı.");
            sock.OnDisconnected += async (_, reason) =>
            {
                _logger.LogWarning("HaremAltin: Koptu ({Reason}).", reason);
                try { await Task.Delay(TimeSpan.FromSeconds(10), ct); } catch { }
                if (!ct.IsCancellationRequested) await ReconnectHaremAsync(ct);
            };

            // price_changed -> snapshot güncelle
            sock.On("price_changed", async resp =>
            {
                try
                {
                    var root = resp.GetValue<JsonElement>();
                    if (!root.TryGetProperty("data", out var dataEl)) return;

                    JsonElement valueObj;
                    if (dataEl.ValueKind == JsonValueKind.Object && dataEl.EnumerateObject().MoveNext())
                        valueObj = dataEl.EnumerateObject().First().Value;
                    else
                        valueObj = dataEl;

                    if (valueObj.TryGetProperty("code", out var doviz) && doviz.GetString() == "ALTIN")
                    {
                        decimal alis = 0, satis = 0;
                        if (valueObj.TryGetProperty("alis", out var alEl))
                            alis = ParseDecimalSafe(alEl.GetString());
                        if (valueObj.TryGetProperty("satis", out var saEl))
                            satis = ParseDecimalSafe(saEl.GetString());

                        DateTime tarih = DateTime.UtcNow;
                        if (root.TryGetProperty("meta", out var metaEl) && metaEl.TryGetProperty("tarih", out var tEl))
                            TryParseTrDate(tEl.GetString(), out tarih);
                        else if (valueObj.TryGetProperty("tarih", out var t2El))
                            TryParseTrDate(t2El.GetString(), out tarih);

                        lock (_lock)
                            _lastSnapshot = (alis, satis, tarih);

                        _logger.LogDebug("HaremAltin snapshot: alis={Alis} satis={Satis} tarih={Tarih}",
                            alis, satis, tarih);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "HaremAltin price_changed parse hatası");
                }

                await Task.CompletedTask;
            });

            _socket = sock;

            for (int i = 0; i < 3 && !ct.IsCancellationRequested; i++)
            {
                try { await sock.ConnectAsync(ct); return; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "HaremAltin: bağlanma başarısız, 2 sn sonra denenecek.");
                    await Task.Delay(2000, ct);
                }
            }
        }

        private async Task ReconnectHaremAsync(CancellationToken ct)
        {
            try { if (_socket is not null && _socket.Connected) await _socket.DisconnectAsync(); } catch { }
            _socket = null;
            await BuildAndConnectHaremAsync(ct);
        }

        private async Task CloseHaremAsync()
        {
            try
            {
                if (_socket is not null)
                {
                    if (_socket.Connected)
                        await _socket.DisconnectAsync();
                    _socket.Dispose();
                }
            }
            catch { }
            finally { _socket = null; }
        }

        // === Kaynak 2: RonaAPI (WebSocket, wss://ronaapi.codeflight.com.tr/ws) ===
        private async Task RunRonaAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                _ws = new ClientWebSocket();
                try
                {
                    var url = new Uri("wss://ronaapi.codeflight.com.tr/ws");
                    _logger.LogInformation("[Rona] Bağlanılıyor: {Url}", url);
                    await _ws.ConnectAsync(url, ct);
                    _logger.LogInformation("[Rona] Bağlandı.");

                    await ReceiveRonaLoop(_ws, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Rona] Bağlantı/okuma hatası. 3 sn sonra tekrar denenecek.");
                    try { await Task.Delay(TimeSpan.FromSeconds(3), ct); } catch { }
                }
                finally
                {
                    await CloseRonaAsync();
                }
            }
        }

        private async Task ReceiveRonaLoop(ClientWebSocket ws, CancellationToken ct)
        {
            var buffer = new byte[8192];
            using var ms = new MemoryStream();

            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                ms.SetLength(0);
                WebSocketReceiveResult? result;
                do
                {
                    result = await ws.ReceiveAsync(buffer, ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogWarning("[Rona] Sunucu kapattı: {Status} {Reason}", result.CloseStatus, result.CloseStatusDescription);
                        return;
                    }
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var text = Encoding.UTF8.GetString(ms.ToArray());
                    HandleRonaMessage(text);
                }
            }
        }

        private void HandleRonaMessage(string json)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };

            try
            {
                var list = JsonSerializer.Deserialize<List<RonaParityItem>>(json, options);
                if (list is null || list.Count == 0) return;

                // Category="03-ALTIN" && Parity="HAS/TRY" kaydı
                var hasTry = list.FirstOrDefault(x =>
                    string.Equals(x.Category, "03-ALTIN", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.Parity, "HAS/TRY", StringComparison.OrdinalIgnoreCase));

                if (hasTry is not null)
                {
                    // Rona mesajından "alış/satış" verilerini alıyoruz: Price1 / Price2
                    var alis = hasTry.Price1;
                    var satis = hasTry.Price2;

                    // Timestamp string gelebiliyor; parse edilemezse Now
                    DateTime tarih = DateTime.Now;
                    if (!string.IsNullOrWhiteSpace(hasTry.Timestamp))
                    {
                        DateTime.TryParse(hasTry.Timestamp, new CultureInfo("tr-TR"),
                            DateTimeStyles.AssumeLocal, out tarih);
                        if (tarih == default) tarih = DateTime.Now;
                    }

                    lock (_lock)
                        _lastSnapshot = (alis, satis, tarih);

                    _logger.LogDebug("[Rona] HAS/TRY snapshot: alis={Alis} satis={Satis} tarih={Tarih}",
                        alis, satis, tarih);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Rona] JSON parse hatası");
            }
        }

        private async Task CloseRonaAsync()
        {
            try
            {
                if (_ws is not null)
                {
                    if (_ws.State == WebSocketState.Open)
                        await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                    _ws.Dispose();
                }
            }
            catch { }
            finally { _ws = null; }
        }

        // === Ortak yardımcılar ===
        private async Task<int> GetDataUrlTypeAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<RikudDbContext>();

                var prm = await db.Set<Parameters>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ParameterName == "DataUrlType", ct);

                if (prm == null) return 1; // default: Harem
                if (int.TryParse(prm.ParameterValue, out var type) && (type == 1 || type == 2))
                    return type;

                return 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DataUrlType okunamadı, varsayılan 1 kullanılacak.");
                return 1;
            }
        }

        private static decimal ParseDecimalSafe(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0m;

            value = value.Replace(".", ",");

            if (decimal.TryParse(value, NumberStyles.Any, new CultureInfo("tr-TR"), out var d))
                return d;

            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out d))
                return d;

            return 0m;
        }

        private static bool TryParseTrDate(string? s, out DateTime dt)
        {
            dt = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(s)) return false;

            var formats = new[]
            {
                "dd-MM-yyyy HH:mm:ss",
                "dd.MM.yyyy HH:mm:ss",
                "dd/MM/yyyy HH:mm:ss",
                "dd-MM-yyyy H:mm:ss",
                "dd.MM.yyyy H:mm:ss"
            };
            return DateTime.TryParseExact(s, formats, new CultureInfo("tr-TR"),
                DateTimeStyles.AssumeLocal, out dt);
        }

        // Rona JSON modeli
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
    }

}

