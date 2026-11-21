using System;
using System.Collections.Generic;
using System.Linq;

namespace JWFixMonitor
{
    /// <summary>Rettangolo in pixel fisici (sinistra, alto, destra, basso).</summary>
    internal struct Box
    {
        public int Left, Top, Right, Bottom;

        public Box(int left, int top, int right, int bottom)
        {
            Left = left; Top = top; Right = right; Bottom = bottom;
        }

        public int Width => Right - Left;
        public int Height => Bottom - Top;
        public bool IsEmpty => Width <= 0 || Height <= 0;

        public override string ToString() => $"({Left},{Top}) {Width}x{Height}";
    }

    internal sealed class MonitorDesc
    {
        public string Device;   // es. \\.\DISPLAY2
        public Box Bounds;
        public bool IsPrimary;
        public int Number;      // numerazione da sinistra a destra, da 1
        public string Path = "";  // percorso del monitor (DisplayConfig), legato a connettore + monitor
        public string Model = ""; // codice EDID, es. SAM0F9E
        public string Name = "";  // nome del monitor, es. SAMSUNG

        public string Label =>
            $"Schermo {Number} — " + (string.IsNullOrEmpty(Name) ? "" : Name + " ") +
            $"{Bounds.Width}×{Bounds.Height}" + (IsPrimary ? " (principale)" : "");
    }

    /// <summary>Schermo scelto dall'utente, con quanto serve per ritrovarlo se Windows lo rinomina.</summary>
    internal sealed class MonitorChoice
    {
        public string Device = "";
        public string Path = "";
        public string Model = "";
        public int Width, Height;

        public static MonitorChoice Auto => new MonitorChoice();

        public bool IsAuto => string.IsNullOrEmpty(Device) && string.IsNullOrEmpty(Path) && string.IsNullOrEmpty(Model);

        public static MonitorChoice From(MonitorDesc m) => new MonitorChoice
        {
            Device = m.Device ?? "", Path = m.Path ?? "", Model = m.Model ?? "",
            Width = m.Bounds.Width, Height = m.Bounds.Height
        };
    }

    /// <summary>Calcoli puri, senza chiamate a Windows: testabili.</summary>
    internal static class Geometry
    {
        // bordi invisibili tipici di Windows 10/11 quando DWM non risponde
        public static readonly Box DefaultInsets = new Box(8, 1, 8, 8);

        /// <summary>
        /// Bordi invisibili della finestra: differenza tra rettangolo della finestra
        /// e cornice visibile (DWM). Valori assurdi vengono sostituiti dai default.
        /// </summary>
        public static Box Insets(Box windowRect, Box? visibleFrame)
        {
            if (visibleFrame == null || visibleFrame.Value.IsEmpty)
            {
                return DefaultInsets;
            }

            var v = visibleFrame.Value;
            var insets = new Box(
                v.Left - windowRect.Left,
                v.Top - windowRect.Top,
                windowRect.Right - v.Right,
                windowRect.Bottom - v.Bottom);

            bool ok(int n) => n >= 0 && n <= 64;
            return ok(insets.Left) && ok(insets.Top) && ok(insets.Right) && ok(insets.Bottom)
                ? insets
                : DefaultInsets;
        }

        /// <summary>
        /// Rettangolo da passare a SetWindowPos perché la parte visibile della
        /// finestra copra esattamente il monitor.
        /// </summary>
        public static Box TargetWindowRect(Box monitor, Box insets) =>
            new Box(
                monitor.Left - insets.Left,
                monitor.Top - insets.Top,
                monitor.Right + insets.Right,
                monitor.Bottom + insets.Bottom);

        /// <summary>Rettangolo w×h ingrandito/rimpicciolito per stare nella casella, centrato, senza deformarlo.</summary>
        public static Box Fit(int w, int h, Box box)
        {
            if (w <= 0 || h <= 0 || box.IsEmpty)
            {
                return new Box();
            }

            var scale = Math.Min((double)box.Width / w, (double)box.Height / h);
            var fw = (int)Math.Round(w * scale);
            var fh = (int)Math.Round(h * scale);
            var left = box.Left + (box.Width - fw) / 2;
            var top = box.Top + (box.Height - fh) / 2;
            return new Box(left, top, left + fw, top + fh);
        }

        /// <summary>
        /// Punto (px,py) sull'anteprima → stesso punto sulla finestra vera (coordinate schermo).
        /// Falso se il punto è fuori dall'anteprima.
        /// </summary>
        public static bool MapPoint(Box preview, Box window, int px, int py, out int x, out int y)
        {
            x = y = 0;
            if (preview.IsEmpty || window.IsEmpty ||
                px < preview.Left || px >= preview.Right || py < preview.Top || py >= preview.Bottom)
            {
                return false;
            }

            x = window.Left + (int)((long)(px - preview.Left) * window.Width / preview.Width);
            y = window.Top + (int)((long)(py - preview.Top) * window.Height / preview.Height);
            return true;
        }

        /// <summary>Rettangolo allargato di n pixel per lato.</summary>
        public static Box Grow(Box b, int n) => new Box(b.Left - n, b.Top - n, b.Right + n, b.Bottom + n);

        /// <summary>Converte un rettangolo dello schermo in coordinate relative alla finestra.</summary>
        public static Box RelativeTo(Box screen, Box window) =>
            new Box(screen.Left - window.Left, screen.Top - window.Top,
                    screen.Right - window.Left, screen.Bottom - window.Top);

        public static bool Matches(Box a, Box b, int tolerance = 2) =>
            Math.Abs(a.Left - b.Left) <= tolerance &&
            Math.Abs(a.Top - b.Top) <= tolerance &&
            Math.Abs(a.Right - b.Right) <= tolerance &&
            Math.Abs(a.Bottom - b.Bottom) <= tolerance;

        /// <summary>Codice del modello dal percorso del monitor (\\?\DISPLAY#SAM0F9E#...), o "".</summary>
        public static string ModelOf(string path)
        {
            var parts = (path ?? "").Split('#');
            return parts.Length >= 3 ? parts[1] : "";
        }

        public static MonitorDesc ChooseMonitor(IList<MonitorDesc> monitors, string preferredDevice, string currentDevice) =>
            ChooseMonitor(monitors, new MonitorChoice { Device = preferredDevice ?? "" }, currentDevice);

        /// <summary>
        /// Sceglie lo schermo di destinazione.
        /// 1) quello scelto dall'utente, se collegato (vedi FindChosen);
        /// 2) con un solo schermo: nessuno (non ha senso spostare);
        /// 3) quello dove sta già la finestra, se non è il principale;
        /// 4) il primo schermo secondario da sinistra.
        /// </summary>
        public static MonitorDesc ChooseMonitor(IList<MonitorDesc> monitors, MonitorChoice choice, string currentDevice)
        {
            if (monitors == null || monitors.Count == 0)
            {
                return null;
            }

            var chosen = FindChosen(monitors, choice);
            if (chosen != null)
            {
                return chosen;
            }

            if (monitors.Count == 1)
            {
                return null;
            }

            var current = monitors.FirstOrDefault(m =>
                string.Equals(m.Device, currentDevice, StringComparison.OrdinalIgnoreCase));
            if (current != null && !current.IsPrimary)
            {
                return current;
            }

            return monitors.Where(m => !m.IsPrimary)
                .OrderBy(m => m.Bounds.Left).ThenBy(m => m.Bounds.Top)
                .FirstOrDefault();
        }

        /// <summary>
        /// Ritrova lo schermo scelto anche se Windows l'ha rinominato: stesso percorso
        /// monitor; stesso modello se unico (TV e timer possono essere uguali); stesso
        /// nome Windows; stessa risoluzione se unica tra i secondari. Altrimenti null.
        /// </summary>
        public static MonitorDesc FindChosen(IList<MonitorDesc> monitors, MonitorChoice c)
        {
            if (c == null || c.IsAuto)
            {
                return null;
            }

            bool same(string a, string b) => !string.IsNullOrEmpty(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
            MonitorDesc single(IEnumerable<MonitorDesc> found)
            {
                var list = found.Take(2).ToList();
                return list.Count == 1 ? list[0] : null;
            }

            return single(monitors.Where(m => same(c.Path, m.Path)))
                ?? single(monitors.Where(m => same(c.Model, m.Model)))
                ?? single(monitors.Where(m => same(c.Device, m.Device)))
                ?? (c.Width > 0 && c.Height > 0
                    ? single(monitors.Where(m => !m.IsPrimary && m.Bounds.Width == c.Width && m.Bounds.Height == c.Height))
                    : null);
        }

        /// <summary>Numera gli schermi da sinistra a destra (poi dall'alto).</summary>
        public static List<MonitorDesc> Number(IEnumerable<MonitorDesc> monitors)
        {
            var list = monitors.OrderBy(m => m.Bounds.Left).ThenBy(m => m.Bounds.Top).ToList();
            for (var i = 0; i < list.Count; i++)
            {
                list[i].Number = i + 1;
            }

            return list;
        }
    }
}
