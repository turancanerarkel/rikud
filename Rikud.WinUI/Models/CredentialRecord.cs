using System;

namespace Rikud.WinUI.Models
{
    [Serializable]
    public class CredentialRecord
    {
        public string Username { get; set; }
        public string SaltB64 { get; set; }
        public string Password { get; set; }
        public int Version { get; set; } = 1; // ileride şema genişletmek için
    }
}
