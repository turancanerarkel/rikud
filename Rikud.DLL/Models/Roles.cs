using System.ComponentModel.DataAnnotations;

namespace Rikud.DLL.Models
{
    public class Roles
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        public string RolAdi { get; set; } = default!;
    }
}
