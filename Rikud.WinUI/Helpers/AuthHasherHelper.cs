using System;
using System.Security.Cryptography;
using System.Text;

namespace Rikud.WinUI.Helpers
{
    public static class AuthHasherHelper
    {
        public static byte[] GenerateSalt(int size = 16)
        {
            var salt = new byte[size];
            using (var rng = RandomNumberGenerator.Create()) 
            {
                rng.GetBytes(salt);
            }
            return salt;

        }
        // SHA-256 hesapla (password + cpuId + hddId + salt)
        public static byte[] ComputeHash(string password, string cpuId, string hddId, byte[] salt)
        {
            // Boş gelebilecek alanları güvenli hale getir
            string cpu = cpuId?.Trim() ?? "";
            string hdd = hddId?.Trim() ?? "";

            // Aynı sırada birleştir (sıra ve ayraç tutarlılığı kritik!)
            string combined = $"{password}||{cpu}||{hdd}";

            // combined + salt => SHA-256
            byte[] combinedBytes = Encoding.UTF8.GetBytes(combined);
            byte[] toHash = new byte[combinedBytes.Length + salt.Length];
            Buffer.BlockCopy(combinedBytes, 0, toHash, 0, combinedBytes.Length);
            Buffer.BlockCopy(salt, 0, toHash, combinedBytes.Length, salt.Length);

            using (var sha = SHA256.Create())
                return sha.ComputeHash(toHash);
        }

        // Sabit-zamanlı karşılaştırma (timing attack azaltmak için)
        public static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        // Görsel/DB saklama kolaylığı için Base64 yardımcıları
        public static string ToBase64(byte[] data) => Convert.ToBase64String(data);
        public static byte[] FromBase64(string b64) => Convert.FromBase64String(b64);

    }
}
