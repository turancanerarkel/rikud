using Microsoft.EntityFrameworkCore;
using Rikud.DLL.Models;

namespace Rikud.DLL.AppDbContext
{
    public class RikudDbContext : DbContext
    {
        public DbSet<Users> Users => Set<Users>();
        public DbSet<UserSessions> UserSessions => Set<UserSessions>();
        public DbSet<Modes> Mods => Set<Modes>();
        public DbSet<Parameters> Parameters => Set<Parameters>();
        public DbSet<Roles> Roles => Set<Roles>();
        public DbSet<Exchanges> Exchanges => Set<Exchanges>();
        public DbSet<ExchangeModeValues> ExchangeModeValues => Set<ExchangeModeValues>();
        public DbSet<Announcements> Announcements => Set<Announcements>();
        public DbSet<ExchangeHistroies> ExchangeHistroies => Set<ExchangeHistroies>();

        public RikudDbContext(DbContextOptions<RikudDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder b)
        {
            // User
            b.Entity<Users>(e =>
            {
                e.ToTable("Kullanicilar");
                e.Property(p => p.Id).ValueGeneratedOnAdd();
                e.Property(p => p.Username).HasMaxLength(150).IsUnicode(false);
                e.Property(p => p.CreatedDate).HasDefaultValueSql("GETDATE()");
                e.HasIndex(p => p.Username).IsUnique(); // İstersen tekil yap
            });

            // UserSession
            b.Entity<UserSessions>(e =>
            {
                e.ToTable("KullaniciOturumlari");
                e.Property(p => p.Id).ValueGeneratedOnAdd();
                e.Property(p => p.SessionKey).HasDefaultValueSql("NEWID()");
                e.Property(p => p.CreatedDate).HasDefaultValueSql("GETDATE()");
                e.HasOne(p => p.User).WithMany(u => u.Sessions).HasForeignKey(p => p.UserId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(p => p.SessionKey).IsUnique();
            });

            // Mod
            b.Entity<Modes>(e =>
            {
                e.ToTable("Modlar");
                e.Property(p => p.ModeName).HasMaxLength(50);
            });

            //Rol
            b.Entity<Roles>(e =>
            {
                e.ToTable("Roller");
                e.Property(p => p.RolAdi).HasMaxLength(50);
            });

            // Döviz
            b.Entity<Exchanges>(e =>
            {
                e.ToTable("Dovizler");
                e.Property(p => p.Alis).HasMaxLength(50);
                e.Property(p => p.Satis).HasMaxLength(50);
            });

            // DovizModDegerleri
            b.Entity<ExchangeModeValues>(e =>
            {
                e.ToTable("DovizModDegerleri");
            });

            // DovizGecmisi
            b.Entity<ExchangeHistroies>(e =>
            {
                e.ToTable("DovizGecmisi");
            });

            // Duyurular
            b.Entity<Announcements>(e =>
            {
                e.ToTable("Duyurular");
                e.Property(p => p.DuyuruText).HasMaxLength(1000);
            });


            // Parameter
            b.Entity<Parameters>(e =>
            {
                e.ToTable("Parametreler");
                e.Property(p => p.ParameterName).HasMaxLength(150);
                e.Property(p => p.ParameterValue).HasMaxLength(300);
                e.HasIndex(p => p.ParameterName).IsUnique(); // Aynı isim bir kez olsun istersen
            });
        }
    }
}
