using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rikud.ConsoleApp
{
    public class RikudDbContext : DbContext
    {
        public RikudDbContext(DbContextOptions<RikudDbContext> options) : base(options) { }

        public DbSet<Exchanges> Exchanges => Set<Exchanges>();
        public DbSet<Parameters> Parameters => Set<Parameters>();
        public DbSet<ExchangeHistroies> ExchangeHistroies => Set<ExchangeHistroies>();
        public DbSet<ExchangeModeValues> ExchangeModeValues => Set<ExchangeModeValues>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            // Döviz
            b.Entity<Exchanges>(e =>
            {
                e.ToTable("Dovizler");
                e.Property(p => p.Alis).HasMaxLength(50);
                e.Property(p => p.Satis).HasMaxLength(50);
            });

            // Parameter
            b.Entity<Parameters>(e =>
            {
                e.ToTable("Parametreler");
                e.Property(p => p.ParameterName).HasMaxLength(150);
                e.Property(p => p.ParameterValue).HasMaxLength(300);
                e.HasIndex(p => p.ParameterName).IsUnique(); // Aynı isim bir kez olsun istersen
            });

            // Mod
            b.Entity<Modes>(e =>
            {
                e.ToTable("Modlar");
                e.Property(p => p.ModeName).HasMaxLength(50);
            });

            // DovizGecmisi
            b.Entity<ExchangeHistroies>(e =>
            {
                e.ToTable("DovizGecmisi");
            });

            // DovizModDegerleri
            b.Entity<ExchangeModeValues>(e =>
            {
                e.ToTable("DovizModDegerleri");
            });
        }

    }

    public class Parameters
    {
        public int Id { get; set; }
        public string ParameterName { get; set; } = "";
        public string ParameterValue { get; set; } = "";
    }

    public class Exchanges
    {
        public int Id { get; set; }
        public int AltinTipId { get; set; }
        public decimal Alis { get; set; }
        public decimal Satis { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class ExchangeHistroies
    {
        [Key]
        public int Id { get; set; }
        public int ModId { get; set; } = default!;

        [Column(TypeName = "decimal(18,4)")]
        public decimal GramAltinAlis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal GramAltinSatis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal GramAltinSatisEFT { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaCeyrekAlis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaCeyrekSatisEFT { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaCeyrekSatis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaYarimAlis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaYarimSatisEFT { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaYarimSatis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaLiraAlis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaLiraSatisEFT { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaLiraSatis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal YirmiIkiAyarAlis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal YirmiIkiAyarSatisEFT { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal YirmiIkiAyarSatis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetCeyrekAlis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetCeyrekSatisEFT { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetCeyrekSatis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetYarimAlis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetYarimSatisEFT { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetYarimSatis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetLiraAlis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetLiraSatisEFT { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetLiraSatis { get; set; } = default!;
        public DateTime CreatedDate { get; set; } = default!;


    }

    public class Modes
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        public string ModeName { get; set; } = default!;
    }

    public class ExchangeModeValues
    {
        [Key]
        public int Id { get; set; }
        public int ModId { get; set; } = default!;

        [Column(TypeName = "decimal(18,4)")]
        public decimal GramAltinAlis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal GramAltinSatis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaCeyrekAlis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaCeyrekSatis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaYarimAlis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaYarimSatis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaLiraAlis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaLiraSatis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal YirmiIkiAyarAlis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal YirmiIkiAyarSatis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetCeyrekAlis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetCeyrekSatis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetYarimAlis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetYarimSatis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetLiraAlis { get; set; } = default!;
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetLiraSatis { get; set; } = default!;

        public DateTime CreatedDate { get; set; } = default!;
        public int CreatedBy { get; set; } = default!;
        public bool SelectedMod { get; set; } = default!;

        [NotMapped]
        public string CreatedByStr { get; set; } = default!;

        [NotMapped]
        public string ModAdi { get; set; } = default!;

    }
}
