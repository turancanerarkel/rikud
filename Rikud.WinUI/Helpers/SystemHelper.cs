using System;
using System.Management;

namespace Rikud.WinUI.Helpers
{
    public static class SystemHelper
    {
        public static string GetCpuId()
        {
            string cpuId = string.Empty;
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("select ProcessorId from Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                {
                    cpuId = obj["ProcessorId"]?.ToString();
                    break; // ilk bulunanı al
                }
            }
            catch (Exception ex)
            {
                cpuId = "Hata: " + ex.Message;
            }
            return cpuId;
        }

        public static string GetHddId()
        {
            string hddId = string.Empty;
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive");
                foreach (ManagementObject obj in searcher.Get())
                {
                    hddId = obj["SerialNumber"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(hddId))
                        break; // ilk disk numarasını al
                }
            }
            catch (Exception ex)
            {
                hddId = "Hata: " + ex.Message;
            }
            return hddId;
        }

    }
}
