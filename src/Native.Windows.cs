using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace JWFixMonitor
{
    /// <summary>Chiamate Win32 per mandare una finestra qualsiasi in TV e per le scorciatoie.</summary>
    internal static partial class Native
    {
        public const int SW_SHOWNORMAL = 1;
        public const int SW_MAXIMIZE = 3;
        public const uint EVENT_OBJECT_HIDE = 0x8003;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_APPWINDOW = 0x00040000;
        private const uint GW_OWNER = 4;
        private const int DWMWA_CLOAKED = 14;

        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_NOREPEAT = 0x4000;

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        [DllImport("user32.dll")] public static extern bool GetWindowPlacement(IntPtr hwnd, ref WINDOWPLACEMENT wp);
        [DllImport("user32.dll")] public static extern bool SetWindowPlacement(IntPtr hwnd, ref WINDOWPLACEMENT wp);
        [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        public static WINDOWPLACEMENT? PlacementOf(IntPtr hwnd)
        {
            var wp = new WINDOWPLACEMENT { length = Marshal.SizeOf(typeof(WINDOWPLACEMENT)) };
            return GetWindowPlacement(hwnd, ref wp) ? wp : (WINDOWPLACEMENT?)null;
        }

        public static bool IsCloaked(IntPtr hwnd) =>
            DwmGetWindowAttributeInt(hwnd, DWMWA_CLOAKED, out int v, sizeof(int)) == 0 && v != 0;

        public static uint ProcessIdOf(IntPtr hwnd)
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            return pid;
        }

        public static string ProcessNameOf(IntPtr hwnd)
        {
            try
            {
                using (var p = Process.GetProcessById((int)ProcessIdOf(hwnd)))
                {
                    return p.ProcessName;
                }
            }
            catch
            {
                return "";
            }
        }

        public static WindowFacts FactsOf(IntPtr hwnd, IntPtr mediaWindow)
        {
            var ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            return new WindowFacts
            {
                Title = TitleOf(hwnd),
                ClassName = ClassOf(hwnd),
                Visible = IsWindowVisible(hwnd),
                Minimized = IsIconic(hwnd),
                Cloaked = IsCloaked(hwnd),
                ToolWindow = (ex & WS_EX_TOOLWINDOW) != 0,
                AppWindow = (ex & WS_EX_APPWINDOW) != 0,
                Owned = GetWindow(hwnd, GW_OWNER) != IntPtr.Zero,
                OwnProcess = ProcessIdOf(hwnd) == (uint)Process.GetCurrentProcess().Id,
                IsMediaWindow = hwnd == mediaWindow
            };
        }

        /// <summary>Finestre che si possono mandare in TV, nell'ordine di Alt+Tab (più recente prima).</summary>
        public static List<IntPtr> SendableWindows(IntPtr mediaWindow)
        {
            var list = new List<IntPtr>();
            foreach (var h in TopLevelWindows())
            {
                if (WindowFilter.CanSend(FactsOf(h, mediaWindow)))
                {
                    list.Add(h);
                }
            }

            return list;
        }
    }
}
