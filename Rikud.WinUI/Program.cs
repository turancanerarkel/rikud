using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Rikud.WinUI
{
    internal static class Program
    {
        public static string CpuId;
        public static string HddId;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            CpuId = Helpers.SystemHelper.GetCpuId();
            HddId = Helpers.SystemHelper.GetHddId();
            Application.Run(new LoginForm());
        }
    }
}
