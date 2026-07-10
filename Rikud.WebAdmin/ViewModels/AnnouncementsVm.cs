using System.ComponentModel.DataAnnotations;

namespace Rikud.WebAdmin.ViewModels
{
    public class AnnouncementsVm
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Duyuru yazılması zorunludur.")]
        [StringLength(150, ErrorMessage = "Duyuru en fazla 1000 karakterdir.")]
        [Display(Name = "Duyuru Metni")]
        public string DuyuruText { get; set; } = string.Empty;

    }
}
