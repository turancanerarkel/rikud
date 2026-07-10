using System.Security.Cryptography;

namespace Rikud.WebAdmin.GlobalHelpers
{
    public static class PasswordHasher
    {
        public static (string hash, string salt) Create(string password)
        {
            // 16 byte salt
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            // 100.000 iterasyon PBKDF2-HMACSHA256
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                100_000,
                HashAlgorithmName.SHA256,
                32);

            return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
        }

        public static bool Verify(string password, string saltBase64, string hashBase64)
        {
            var salt = Convert.FromBase64String(saltBase64);
            using var pbkdf2 = new Rfc2898DeriveBytes(
                password, salt, 100_000, HashAlgorithmName.SHA256);
            var computed = pbkdf2.GetBytes(32);
            return Convert.ToBase64String(computed) == hashBase64;
        }
    }

}
