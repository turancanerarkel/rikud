using Rikud.WinUI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Rikud.WinUI.Helpers
{
    public static class CredentialHelper
    {
        private const string FolderName = "Kuder\\KuderApp"; // kendi uygulama adın
        private const string FileName = "credentials.xml";

        public static string GetFilePath()
        {
            string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); // %AppData%
            string dir = Path.Combine(baseDir, FolderName);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return Path.Combine(dir, FileName);
        }

        public static void Save(CredentialRecord rec)
        {
            string path = GetFilePath();
            var xs = new XmlSerializer(typeof(CredentialRecord));
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                xs.Serialize(fs, rec);
            }
        }

        public static CredentialRecord Load()
        {
            string path = GetFilePath();
            if (!File.Exists(path)) return null;

            var xs = new XmlSerializer(typeof(CredentialRecord));
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return (CredentialRecord)xs.Deserialize(fs);
            }
        }
    }
}
