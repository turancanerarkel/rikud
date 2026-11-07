using Microsoft.EntityFrameworkCore;
using Rikud.WatchDog;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;

namespace RikudWatchdog
{
    internal class Program
    {
        // Yapılandırma Ayarları
        private const string TASK_NAME = "rikud doviz güncelleme ekranı"; // Task Scheduler'daki tam ad
        private const string PROCESS_NAME = "Rikud.ConsoleApp"; // .exe uzantısı olmadan process adı
        private const int DATA_TIMEOUT_MINUTES = 30; // Kaç dakika veri gelmezse reset atılsın?
        private const int CHECK_INTERVAL_SECONDS = 60; // Kontrol döngüsü kaç saniyede bir çalışsın?

        // Reset sonrası hemen tekrar reset atmamak için bekleme süresi (Cooldown)
        private static DateTime _lastRestartTime = DateTime.MinValue;

        // Arşiv: günde 1 kez (03:00 civarı), arka planda
        private static DateTime _lastArchiveDate = DateTime.MinValue;
        private static int _archiveRunning = 0;
        private const int ARCHIVE_HOUR = 3;
        private const int ARCHIVE_RETENTION_DAYS = 7;

        static async Task Main(string[] args)
        {
            Console.WriteLine($"[{DateTime.Now}] Watchdog başlatıldı...");
            Console.WriteLine($"Hedef Task: {TASK_NAME}");
            Console.WriteLine($"Hedef Process: {PROCESS_NAME}");
            Console.WriteLine("--------------------------------------------------");

            while (true)
            {
                try
                {
                    TryKickDailyArchive();
                    await CheckSystemStatus();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now}] HATA OLUŞTU: {ex.Message}");
                }

                // Bekle
                await Task.Delay(TimeSpan.FromSeconds(CHECK_INTERVAL_SECONDS));
            }
        }

        private static async Task CheckSystemStatus()
        {
            // Eğer yeni reset attıysak, sistemin toparlaması için 5 dakika müdahale etme
            if ((DateTime.Now - _lastRestartTime).TotalMinutes < 5)
            {
                Console.WriteLine($"[{DateTime.Now}] Sistem yeni başlatıldı, cooldown bekleniyor...");
                return;
            }

            bool isProcessRunning = IsProcessRunning(PROCESS_NAME);

            // 1. KONTROL: Process hiç çalışmıyorsa direkt başlat
            if (!isProcessRunning)
            {
                Console.WriteLine($"[{DateTime.Now}] DİKKAT: {PROCESS_NAME} çalışmıyor! Task başlatılıyor...");
                RestartTask();
                return;
            }
            var opt = new DbContextOptionsBuilder<RikudDbContext>()
    .UseSqlServer("Data Source=localhost;Persist Security Info=True;initial catalog=RIKUDDB;User ID=user_rikud;Password=wr1He7r_ph5trl@tIk;Pooling=True;Min Pool Size=1;Max Pool Size=10;MultipleActiveResultSets=False;Connect Timeout=15;Encrypt=True;TrustServerCertificate=True;",
         b => b.CommandTimeout(30))
    .Options;

            // 2. KONTROL: Process çalışıyor ama veri yazıyor mu? (Stale Data Check)
            using (var db = new RikudDbContext(opt))
            {
                // En son eklenen kaydın tarihini al
                var lastRecord = await db.Exchanges
                    .AsNoTracking()
                    .OrderByDescending(x => x.CreatedDate)
                    .Select(x => x.CreatedDate)
                    .FirstOrDefaultAsync();

                if (lastRecord == default)
                {
                    Console.WriteLine($"[{DateTime.Now}] Veritabanı boş veya erişilemiyor.");
                    // Veritabanı boşsa da reset atmak isteyebilirsiniz, duruma göre burayı açın:
                    // RestartTask(); 
                    return;
                }

                var timeDiff = DateTime.Now - lastRecord;
                Console.WriteLine($"[{DateTime.Now}] Son Veri: {lastRecord} ({timeDiff.TotalMinutes:N0} dk önce). Process: {(isProcessRunning ? "Aktif" : "Kapalı")}");

                // Eğer son 30 dakikadır veri yoksa
                if (timeDiff.TotalMinutes > DATA_TIMEOUT_MINUTES)
                {
                    Console.WriteLine($"[{DateTime.Now}] KRİTİK: {DATA_TIMEOUT_MINUTES} dakikadır yeni veri yok! Sistem yeniden başlatılıyor...");
                    RestartTask();
                }
            }
        }

        // --- YARDIMCI METOTLAR ---

        private static bool IsProcessRunning(string processName)
        {
            try
            {
                return Process.GetProcessesByName(processName).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static void RestartTask()
        {
            try
            {
                _lastRestartTime = DateTime.Now;

                // 1. Task Scheduler Görevini Durdur
                Console.WriteLine("1. Task durduruluyor...");
                RunCmd($"schtasks /End /TN \"{TASK_NAME}\"");
                Thread.Sleep(2000);

                // 2. Process hala açıksa zorla öldür (Kill)
                var runningProcesses = Process.GetProcessesByName(PROCESS_NAME);
                foreach (var proc in runningProcesses)
                {
                    try
                    {
                        Console.WriteLine($"2. Process Kill ediliyor (PID: {proc.Id})...");
                        proc.Kill();
                        proc.WaitForExit(5000);
                    }
                    catch { /* Zaten kapanmış olabilir */ }
                    finally
                    {
                        try { proc.Dispose(); } catch { }
                    }
                }

                // Kill sonrası mutex'in OS tarafından bırakılması için kısa bekleme
                Thread.Sleep(1500);

                // 3. Task Scheduler Görevini Tekrar Başlat
                Console.WriteLine("3. Task tekrar başlatılıyor...");
                RunCmd($"schtasks /Run /TN \"{TASK_NAME}\"");

                // 4. Gerçekten ayağa kalktı mı? (abandoned mutex / SQL timeout crash'ini yakala)
                Thread.Sleep(5000);
                if (IsProcessRunning(PROCESS_NAME))
                    Console.WriteLine($">> {PROCESS_NAME} ayakta (PID kontrol OK).");
                else
                    Console.WriteLine($">> UYARI: {PROCESS_NAME} 5 sn sonra hala yok! Manuel kontrol et (mutex/SQL).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Restart sırasında hata: {ex.Message}");
            }
        }

        private static void TryKickDailyArchive()
        {
            // Gece 03:00–03:59 arası, günde bir kez. İlk kurulumda SSMS'ten
            // EXEC usp_Rikud_ArchiveOldData çalıştır; burası sonrası için.
            if (DateTime.Now.Hour != ARCHIVE_HOUR) return;
            if (_lastArchiveDate.Date == DateTime.Now.Date) return;
            if (Interlocked.CompareExchange(ref _archiveRunning, 1, 0) != 0) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    Console.WriteLine($"[{DateTime.Now}] Arşiv başlıyor (retention={ARCHIVE_RETENTION_DAYS} gün)...");

                    var opt = new DbContextOptionsBuilder<RikudDbContext>()
                        .UseSqlServer("Data Source=localhost;Persist Security Info=True;initial catalog=RIKUDDB;User ID=user_rikud;Password=wr1He7r_ph5trl@tIk;Pooling=True;Min Pool Size=1;Max Pool Size=10;MultipleActiveResultSets=False;Connect Timeout=30;Encrypt=True;TrustServerCertificate=True;",
                            b => b.CommandTimeout(0)) // arşiv uzun sürebilir
                        .Options;

                    await using var db = new RikudDbContext(opt);
                    await db.Database.ExecuteSqlRawAsync(
                        "EXEC dbo.usp_Rikud_ArchiveOldData @RetentionDays={0}, @BatchSize={1}",
                        ARCHIVE_RETENTION_DAYS, 20000);

                    _lastArchiveDate = DateTime.Now.Date;
                    Console.WriteLine($"[{DateTime.Now}] Arşiv tamamlandı.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now}] Arşiv HATA: {ex.Message}");
                }
                finally
                {
                    Interlocked.Exchange(ref _archiveRunning, 0);
                }
            });
        }

        private static void RunCmd(string arguments)
        {
            // Windows komut satırında schtasks çalıştırmak için
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C {arguments}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var proc = Process.Start(startInfo))
            {
                proc?.WaitForExit();
                // Çıktıları loglamak isterseniz:
                // var output = proc.StandardOutput.ReadToEnd();
            }
        }
    }
}