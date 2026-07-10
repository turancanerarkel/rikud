using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rikud.DLL.Models
{
    public class UserSessions
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public Guid SessionKey { get; set; }

        public string IpAddress { get; set; }
        public string Browser { get; set; }

        public DateTime CreatedDate { get; set; }

        // Nav.
        public Users User { get; set; } = default!;

        [NotMapped]
        public string Username { get; set; }

        [NotMapped]
        public int Oturum { get; set; }

    }
}
