using System;
using System.Threading;
using System.Windows.Forms;

namespace JWFixMonitor
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            using (var mutex = new Mutex(true, @"Local\JWFixMonitor-single-instance", out var isFirst))
            {
                if (!isFirst)
                {
                    MessageBox.Show(L.T("AlreadyRunning"),
                        "JWFixMonitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) => Log.Write("Errore interfaccia: " + e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Write("Errore grave: " + e.ExceptionObject);

                Application.Run(new TrayApp());
                GC.KeepAlive(mutex);
            }
        }
    }
}
