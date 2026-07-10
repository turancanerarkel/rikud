using System.ComponentModel.DataAnnotations;

namespace Rikud.WebUI.ViewModels
{
    public class LoginVm
    {
        [Required(ErrorMessage = "Kullanici adi zorunludur.")]
        [StringLength(150, ErrorMessage = "Kullanici adi en fazla 150 karakterdir.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Sifre zorunludur.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
