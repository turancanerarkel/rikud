using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rikud.DLL.Models
{
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
