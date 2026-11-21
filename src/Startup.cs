using System;
using Microsoft.Win32;

namespace JWFixMonitor
{
    /// <summary>Avvio automatico con Windows (solo per l'utente corrente).</summary>
    internal static class Startup
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "JWFixMonitor";

        public static bool IsEnabled
        {
            get
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                    {
                        return key?.GetValue(ValueName) is string;
                    }
                }
                catch
                {
                    return false;
                }
            }
        }

        public static void Set(bool enabled, string exePath)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (enabled)
                    {
                        key.SetValue(ValueName, "\"" + exePath + "\"");
                    }
                    else
                    {
                        key.DeleteValue(ValueName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("Avvio automatico non modificabile: " + ex.Message);
            }
        }
    }
}
