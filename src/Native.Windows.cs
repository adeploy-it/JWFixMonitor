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

        // ---------------- miniature dal vivo (come la barra delle applicazioni) ----------------

        private const int DWM_TNP_RECTDESTINATION = 0x1, DWM_TNP_OPACITY = 0x4, DWM_TNP_VISIBLE = 0x8;

        [StructLayout(LayoutKind.Sequential)]
        private struct DWM_THUMBNAIL_PROPERTIES
        {
            public int dwFlags;
            public RECT rcDestination;
            public RECT rcSource;
            public byte opacity;
            [MarshalAs(UnmanagedType.Bool)] public bool fVisible;
            [MarshalAs(UnmanagedType.Bool)] public bool fSourceClientAreaOnly;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx, cy; }

        [DllImport("dwmapi.dll")] private static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);
        [DllImport("dwmapi.dll")] private static extern int DwmUnregisterThumbnail(IntPtr thumb);
        [DllImport("dwmapi.dll")] private static extern int DwmUpdateThumbnailProperties(IntPtr thumb, ref DWM_THUMBNAIL_PROPERTIES props);
        [DllImport("dwmapi.dll")] private static extern int DwmQueryThumbnailSourceSize(IntPtr thumb, out SIZE size);

        /// <summary>Registra una miniatura di src disegnata su dest. Zero se non si può.</summary>
        public static IntPtr RegisterThumbnail(IntPtr dest, IntPtr src) =>
            DwmRegisterThumbnail(dest, src, out var thumb) == 0 ? thumb : IntPtr.Zero;

        public static void UnregisterThumbnail(IntPtr thumb)
        {
            if (thumb != IntPtr.Zero)
            {
                DwmUnregisterThumbnail(thumb);
            }
        }

        /// <summary>Disegna la miniatura dentro box (coordinate client di dest), senza deformarla.</summary>
        public static void ShowThumbnail(IntPtr thumb, Box box, bool visible)
        {
            if (thumb == IntPtr.Zero)
            {
                return;
            }

            var fit = DwmQueryThumbnailSourceSize(thumb, out var size) == 0
                ? Geometry.Fit(size.cx, size.cy, box)
                : box;
            var props = new DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = DWM_TNP_RECTDESTINATION | DWM_TNP_OPACITY | DWM_TNP_VISIBLE,
                rcDestination = new RECT { Left = fit.Left, Top = fit.Top, Right = fit.Right, Bottom = fit.Bottom },
                opacity = 255,
                fVisible = visible && !fit.IsEmpty
            };
            DwmUpdateThumbnailProperties(thumb, ref props);
        }

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
