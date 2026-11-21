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

        // ---------------- telecomando: input girato alla finestra in TV ----------------

        private const uint MOUSEEVENTF_MOVE = 0x0001, MOUSEEVENTF_RIGHTDOWN = 0x0008, MOUSEEVENTF_RIGHTUP = 0x0010,
            MOUSEEVENTF_WHEEL = 0x0800, MOUSEEVENTF_VIRTUALDESK = 0x4000, MOUSEEVENTF_ABSOLUTE = 0x8000;

        /// <summary>Segno sugli eventi mandati dal telecomando: se tornano al pannello li ignoro.</summary>
        public static readonly IntPtr RemoteMark = new IntPtr(0x4A574658); // "JWFX"

        [DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] public static extern IntPtr GetMessageExtraInfo();

        private static INPUT MouseInput(uint flags, int dx = 0, int dy = 0, uint data = 0) => new INPUT
        {
            type = 0, // INPUT_MOUSE
            u = new InputUnion { mi = new MOUSEINPUT { dx = dx, dy = dy, mouseData = data, dwFlags = flags, dwExtraInfo = RemoteMark } }
        };

        private static INPUT MoveTo(int x, int y)
        {
            int vx = GetSystemMetrics(76), vy = GetSystemMetrics(77), vw = GetSystemMetrics(78), vh = GetSystemMetrics(79);
            var nx = (int)((long)(x - vx) * 65535 / Math.Max(1, vw - 1));
            var ny = (int)((long)(y - vy) * 65535 / Math.Max(1, vh - 1));
            return MouseInput(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK, nx, ny);
        }

        /// <summary>
        /// Va in (x,y), fa l'azione e torna dov'era: un'unica sequenza, eseguita da Windows
        /// in ordine (con SetCursorPos il ritorno arrivava prima dell'azione).
        /// </summary>
        private static void MouseActionAt(int x, int y, params INPUT[] actions)
        {
            GetCursorPos(out var old);
            var seq = new List<INPUT> { MoveTo(x, y) };
            seq.AddRange(actions);
            seq.Add(MoveTo(old.X, old.Y));
            SendInput((uint)seq.Count, seq.ToArray(), Marshal.SizeOf(typeof(INPUT)));
        }

        public static void WheelAt(int x, int y, int delta) =>
            MouseActionAt(x, y, MouseInput(MOUSEEVENTF_WHEEL, data: unchecked((uint)delta)));

        public static void LeftClickAt(int x, int y) =>
            MouseActionAt(x, y, MouseInput(0x0002), MouseInput(0x0004));

        public static void RightClickAt(int x, int y) =>
            MouseActionAt(x, y, MouseInput(MOUSEEVENTF_RIGHTDOWN), MouseInput(MOUSEEVENTF_RIGHTUP));

        /// <summary>Preme e rilascia un tasto (i modificatori tenuti dall'utente restano validi).</summary>
        public static void PressKey(byte vk, bool extended)
        {
            var ext = extended ? KEYEVENTF_EXTENDEDKEY : 0;
            keybd_event(vk, 0, ext, IntPtr.Zero);
            keybd_event(vk, 0, ext | KEYEVENTF_KEYUP, IntPtr.Zero);
        }

        private const uint GW_HWNDPREV = 3;

        /// <summary>
        /// Prima finestra visibile che sta sopra hwnd (ordine z) e copre almeno in parte area:
        /// es. il bordo verde che Zoom disegna sullo schermo condiviso. Zero se nessuna.
        /// </summary>
        public static IntPtr WindowAbove(IntPtr hwnd, Box area)
        {
            var me = (uint)Process.GetCurrentProcess().Id;
            for (var h = GetWindow(hwnd, GW_HWNDPREV); h != IntPtr.Zero; h = GetWindow(h, GW_HWNDPREV))
            {
                if (!IsWindowVisible(h) || IsIconic(h) || IsCloaked(h) || ProcessIdOf(h) == me)
                {
                    continue;
                }

                var r = WindowRect(h);
                if (r != null && Geometry.Intersects(r.Value, area))
                {
                    return h;
                }
            }

            return IntPtr.Zero;
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
