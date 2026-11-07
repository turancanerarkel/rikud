using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rikud.DLL.Models
{
    /// <summary>
    /// DovizGecmisi_arc — 7 günden eski geçmiş kayıtlar.
    /// Canlı tabloyla aynı kolonlar + ArchivedDate.
    /// </summary>
    public class ExchangeHistroiesArc
    {
        [Key]
        public int Id { get; set; }
        public int ModId { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal GramAltinAlis { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal GramAltinSatis { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal GramAltinSatisEFT { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaCeyrekAlis { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaCeyrekSatisEFT { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaCeyrekSatis { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaYarimAlis { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaYarimSatisEFT { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaYarimSatis { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaLiraAlis { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaLiraSatisEFT { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal AtaLiraSatis { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal YirmiIkiAyarAlis { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal YirmiIkiAyarSatisEFT { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal YirmiIkiAyarSatis { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetCeyrekAlis { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetCeyrekSatisEFT { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetCeyrekSatis { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetYarimAlis { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetYarimSatisEFT { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetYarimSatis { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetLiraAlis { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetLiraSatisEFT { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal ZiynetLiraSatis { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ArchivedDate { get; set; }
    }
}
