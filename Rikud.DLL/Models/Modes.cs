using System.ComponentModel.DataAnnotations;

namespace Rikud.DLL.Models
{
    public class Modes
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        public string ModeName { get; set; } = default!;
    }
}
