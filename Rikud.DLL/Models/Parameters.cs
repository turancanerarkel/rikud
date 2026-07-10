using System.ComponentModel.DataAnnotations;

namespace Rikud.DLL.Models
{
    public class Parameters
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string ParameterName { get; set; } = default!;

        [Required, StringLength(300)]
        public string ParameterValue { get; set; } = default!;
    }
}
