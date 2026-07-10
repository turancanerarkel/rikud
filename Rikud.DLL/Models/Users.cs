using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rikud.DLL.Models
{
    public class Users
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Kullanici adi zorunludur.")]
        [StringLength(150, ErrorMessage = "Kullanici adi en fazla 150 karakterdir.")]
        public string Username { get; set; } = default!;

        [Required] // nvarchar(MAX)
        public string PasswordHash { get; set; } = default!;

        [Required] // nvarchar(MAX)
        public string PasswordSalt { get; set; } = default!;
        
        public string CompanyName { get; set; } = default!;
        
        public int SessionCount { get; set; }
        public int RolId { get; set; }

        public bool Active { get; set; }

        public DateTime CreatedDate { get; set; }

        // Nav.
        public ICollection<UserSessions> Sessions { get; set; } = new List<UserSessions>();

        [NotMapped]
        [Required(ErrorMessage = "Şifre zorunludur.")]
        public string Password { get; set; }=string.Empty;
        
        [NotMapped]
        public string Role { get; set; } = string.Empty;



    }
}
