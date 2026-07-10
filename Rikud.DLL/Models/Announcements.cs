using System.ComponentModel.DataAnnotations;

namespace Rikud.DLL.Models
{
    public class Announcements
    {
        [Key]
        public int Id { get; set; }
        [Required, StringLength(1000)]
        public string DuyuruText { get; set; }
        public int SortedBy { get; set; }
    }
}
