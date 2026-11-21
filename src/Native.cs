using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace JWFixMonitor
{
    internal static class Native
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOPMOST = 0x00000008;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_NOCOPYBITS = 0x0100;
        public const uint SWP_NOOWNERZORDER = 0x0200;
        public const uint SWP_NOSENDCHANGING = 0x0400;

        public const uint MONITOR_DEFAULTTONEAREST = 2;
        public const uint MONITORINFOF_PRIMARY = 1;

        public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        public const uint EVENT_OBJECT_DESTROY = 0x8001;
        public const uint EVENT_OBJECT_SHOW = 0x8002;
        public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
        public const int OBJID_WINDOW = 0;

        public const int SW_RESTORE = 9;
        public const int SW_MINIMIZE = 6;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x02, MOUSEEVENTF_LEFTUP = 0x04;

        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] private static extern void mouse_event(uint flags, int dx, int dy, uint data, IntPtr extra);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);

        public static string TitleOf(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            return GetWindowText(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : "";
        }

        /// <summary>Clic sinistro vero in un punto dello schermo; poi rimette il cursore dov'era.</summary>
        public static void ClickAt(int x, int y)
        {
            GetCursorPos(out var old);
            SetCursorPos(x, y);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
            SetCursorPos(old.X, old.Y);
        }
        public const uint GA_ROOT = 2;

        public const uint INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const ushort VK_RETURN = 0x0D;
        public const ushort VK_LSHIFT = 0xA0;
        public const ushort VK_LWIN = 0x5B;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
            public Box ToBox() => new Box(Left, Top, Right, Bottom);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData, dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk, wScan;
            public uint dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data);
        public delegate void WinEventProc(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT point);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X, Y;
        }
        [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX info);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT value, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] private static extern int DwmGetWindowAttributeInt(IntPtr hwnd, int attr, out int value, int size);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr hwnd, IntPtr rgn, bool redraw);

        public const int DWMWA_VISIBLE_FRAME_BORDER_THICKNESS = 37;

        /// <summary>Spessore del bordo che Windows 11 disegna dentro la cornice visibile (0 se non c'è).</summary>
        public static int VisibleBorderThickness(IntPtr hwnd) =>
            DwmGetWindowAttributeInt(hwnd, DWMWA_VISIBLE_FRAME_BORDER_THICKNESS, out var t, sizeof(int)) == 0 && t >= 0 && t <= 8
                ? t
                : 0;

        /// <summary>
        /// Ritaglia la finestra al rettangolo dato (in coordinate della finestra), così
        /// quello che sborda non compare sugli schermi vicini. Box null = nessun ritaglio.
        /// </summary>
        public static void ClipWindow(IntPtr hwnd, Box? clip)
        {
            if (clip == null)
            {
                SetWindowRgn(hwnd, IntPtr.Zero, true);
                return;
            }

            var c = clip.Value;
            var rgn = CreateRectRgn(c.Left, c.Top, c.Right, c.Bottom);
            if (SetWindowRgn(hwnd, rgn, true) == 0)
            {
                DeleteObject(rgn); // in caso di successo la regione appartiene a Windows
            }
        }

        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWCP_DEFAULT = 0, DWMWCP_DONOTROUND = 1;
        private const int DWMWA_COLOR_DEFAULT = unchecked((int)0xFFFFFFFF), DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);

        /// <summary>
        /// Toglie (o rimette) il bordo da 1px e gli angoli arrotondati che Windows 11
        /// disegna attorno alle finestre. Sulle versioni senza questi attributi non fa nulla.
        /// </summary>
        public static void SetBorderless(IntPtr hwnd, bool borderless)
        {
            var corner = borderless ? DWMWCP_DONOTROUND : DWMWCP_DEFAULT;
            var color = borderless ? DWMWA_COLOR_NONE : DWMWA_COLOR_DEFAULT;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref color, sizeof(int));
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool EnableWindow(IntPtr hwnd, bool enable);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint count, INPUT[] inputs, int size);
        [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint pid, uint thread, uint flags);
        [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);

        // ---- piccoli aiuti ----

        public static string ClassOf(IntPtr hwnd)
        {
            var sb = new StringBuilder(128);
            return GetClassName(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : "";
        }

        public static bool IsTopMost(IntPtr hwnd) => (GetWindowLong(hwnd, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0;

        public static Box? WindowRect(IntPtr hwnd) =>
            GetWindowRect(hwnd, out var r) ? r.ToBox() : (Box?)null;

        /// <summary>Cornice visibile (senza bordi invisibili), in pixel fisici.</summary>
        public static Box? VisibleFrame(IntPtr hwnd) =>
            DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var r, Marshal.SizeOf(typeof(RECT))) == 0
                ? r.ToBox()
                : (Box?)null;

        public static List<IntPtr> TopLevelWindows()
        {
            var list = new List<IntPtr>();
            EnumWindows((h, _) => { list.Add(h); return true; }, IntPtr.Zero);
            return list;
        }

        /// <summary>
        /// Finestre radice visibili in alcuni punti di ogni schermo. Serve perché le
        /// finestre UWP a schermo intero stanno in una "band" immersiva che EnumWindows
        /// (su Windows 11 recenti) non restituisce, mentre WindowFromPoint sì.
        /// </summary>
        public static List<IntPtr> RootWindowsOnMonitors()
        {
            var list = new List<IntPtr>();
            foreach (var m in Monitors())
            {
                var b = m.Bounds;
                foreach (var (fx, fy) in new[] { (0.5, 0.5), (0.25, 0.25), (0.75, 0.25), (0.25, 0.75), (0.75, 0.75) })
                {
                    var pt = new POINT { X = b.Left + (int)(b.Width * fx), Y = b.Top + (int)(b.Height * fy) };
                    var h = WindowFromPoint(pt);
                    if (h == IntPtr.Zero)
                    {
                        continue;
                    }

                    var root = GetAncestor(h, GA_ROOT);
                    if (root != IntPtr.Zero && !list.Contains(root))
                    {
                        list.Add(root);
                    }
                }
            }

            return list;
        }

        public static List<MonitorDesc> Monitors()
        {
            var list = new List<MonitorDesc>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr hdc, ref RECT rc, IntPtr d) =>
            {
                var info = new MONITORINFOEX { cbSize = Marshal.SizeOf(typeof(MONITORINFOEX)) };
                if (GetMonitorInfo(h, ref info))
                {
                    list.Add(new MonitorDesc
                    {
                        Device = info.szDevice,
                        Bounds = info.rcMonitor.ToBox(),
                        IsPrimary = (info.dwFlags & MONITORINFOF_PRIMARY) != 0
                    });
                }

                return true;
            }, IntPtr.Zero);

            try
            {
                var ids = MonitorIdentities();
                foreach (var m in list)
                {
                    if (ids.TryGetValue(m.Device, out var id))
                    {
                        m.Path = id.Path;
                        m.Model = Geometry.ModelOf(id.Path);
                        m.Name = id.Name;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("Nomi dei monitor non disponibili: " + ex.Message);
            }

            return Geometry.Number(list);
        }

        // ---------------- DisplayConfig: nome e percorso dei monitor ----------------

        private const uint QDC_ONLY_ACTIVE_PATHS = 2;
        private const uint DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
        private const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;
        private const int PathInfoSize = 72, ModeInfoSize = 64;

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID { public uint Low; public int High; }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_DEVICE_INFO_HEADER { public uint type; public uint size; public LUID adapterId; public uint id; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string viewGdiDeviceName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint flags, outputTechnology;
            public ushort edidManufactureId, edidProductCodeId;
            public uint connectorInstance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string monitorFriendlyDeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string monitorDevicePath;
        }

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPaths, out uint numModes);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(uint flags, ref uint numPaths, IntPtr paths, ref uint numModes, IntPtr modes, IntPtr topologyId);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME request);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME request);

        /// <summary>Per ogni \\.\DISPLAYn attivo: percorso e nome del monitor collegato.</summary>
        private static Dictionary<string, (string Path, string Name)> MonitorIdentities()
        {
            var result = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
            if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out var numPaths, out var numModes) != 0 || numPaths == 0)
            {
                return result;
            }

            var paths = Marshal.AllocHGlobal((int)numPaths * PathInfoSize);
            var modes = Marshal.AllocHGlobal((int)Math.Max(numModes, 1) * ModeInfoSize);
            try
            {
                if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref numPaths, paths, ref numModes, modes, IntPtr.Zero) != 0)
                {
                    return result;
                }

                for (var i = 0; i < numPaths; i++)
                {
                    var p = paths + i * PathInfoSize;
                    // sourceInfo: adapterId (0), id (8); targetInfo: adapterId (20), id (28)
                    var source = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                    source.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                    source.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME));
                    source.header.adapterId = Marshal.PtrToStructure<LUID>(p);
                    source.header.id = (uint)Marshal.ReadInt32(p, 8);

                    var target = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                    target.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                    target.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_TARGET_DEVICE_NAME));
                    target.header.adapterId = Marshal.PtrToStructure<LUID>(p + 20);
                    target.header.id = (uint)Marshal.ReadInt32(p, 28);

                    if (DisplayConfigGetDeviceInfo(ref source) == 0 && DisplayConfigGetDeviceInfo(ref target) == 0 &&
                        !string.IsNullOrEmpty(source.viewGdiDeviceName) && !result.ContainsKey(source.viewGdiDeviceName))
                    {
                        result[source.viewGdiDeviceName] = (target.monitorDevicePath ?? "", (target.monitorFriendlyDeviceName ?? "").Trim());
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(paths);
                Marshal.FreeHGlobal(modes);
            }

            return result;
        }

        public static string MonitorDeviceOf(IntPtr hwnd)
        {
            var h = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf(typeof(MONITORINFOEX)) };
            return h != IntPtr.Zero && GetMonitorInfo(h, ref info) ? info.szDevice : "";
        }

        /// <summary>Porta una finestra in primo piano anche da un processo in background.</summary>
        public static void ForceForeground(IntPtr hwnd)
        {
            var fg = GetForegroundWindow();
            var fgThread = fg == IntPtr.Zero ? 0 : GetWindowThreadProcessId(fg, out _);
            var me = GetCurrentThreadId();
            var attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);

            try
            {
                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
            }
            finally
            {
                if (attached)
                {
                    AttachThreadInput(me, fgThread, false);
                }
            }
        }

        /// <summary>Invia Win+Shift+Invio (fa uscire una finestra UWP dallo schermo intero).</summary>
        public static bool SendWinShiftEnter()
        {
            INPUT Key(ushort vk, bool up, bool extended = false) => new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        dwFlags = (up ? KEYEVENTF_KEYUP : 0) | (extended ? KEYEVENTF_EXTENDEDKEY : 0)
                    }
                }
            };

            var inputs = new[]
            {
                Key(VK_LWIN, false, true),
                Key(VK_LSHIFT, false),
                Key(VK_RETURN, false),
                Key(VK_RETURN, true),
                Key(VK_LSHIFT, true),
                Key(VK_LWIN, true, true),
            };

            return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT))) == inputs.Length;
        }
    }
}
