using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rikud.DLL.Models
{
        public class Exchanges
        {
            [Key]
            public int Id { get; set; }
            public int AltinTipId { get; set; } = default!;
            [Column(TypeName = "decimal(18,4)")]
            public decimal Alis { get; set; } = default!;
            [Column(TypeName = "decimal(18,4)")]
            public decimal Satis { get; set; } = default!;
            public DateTime CreatedDate { get; set; } = default!;
        }
}
