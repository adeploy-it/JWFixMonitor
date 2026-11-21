using System;

namespace JWFixMonitor
{
    /// <summary>Quello che serve sapere di una finestra per decidere se si può mandare in TV.</summary>
    internal struct WindowFacts
    {
        public string Title;
        public string ClassName;
        public bool Visible;
        public bool Minimized;
        public bool Cloaked;      // nascosta da DWM: app sospesa o altro desktop virtuale
        public bool ToolWindow;   // WS_EX_TOOLWINDOW
        public bool AppWindow;    // WS_EX_APPWINDOW
        public bool Owned;        // ha una finestra proprietaria (tipico dei dialoghi)
        public bool OwnProcess;   // è di JWFixMonitor
        public bool IsMediaWindow;
    }

    /// <summary>Calcoli puri sulla scelta delle finestre: testabili.</summary>
    internal static class WindowFilter
    {
        private static readonly string[] ShellClasses =
            { "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW", "Windows.UI.Core.CoreWindow" };

        /// <summary>Stessa regola della lista Alt+Tab, senza JWFixMonitor, JW media e la shell.</summary>
        public static bool CanSend(WindowFacts f)
        {
            if (!f.Visible || f.Cloaked || f.ToolWindow || f.OwnProcess || f.IsMediaWindow ||
                string.IsNullOrWhiteSpace(f.Title))
            {
                return false;
            }

            if (f.Owned && !f.AppWindow)
            {
                return false;
            }

            return Array.IndexOf(ShellClasses, f.ClassName ?? "") < 0;
        }
    }
}
