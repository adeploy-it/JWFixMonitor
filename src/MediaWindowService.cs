using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Automation;

namespace JWFixMonitor
{
    /// <summary>
    /// Trova la finestra media di JW Library, la fa uscire dallo schermo intero UWP
    /// (Win+Shift+Invio) e la incolla allo schermo scelto. Lavora su un thread
    /// dedicato: si sveglia sugli eventi di Windows e, per sicurezza, ogni 3 secondi.
    /// </summary>
    internal sealed class MediaWindowService : IDisposable
    {
        private static readonly string[] JwlProcesses = { "JWLibrary", "JWLibrary.Forms.UWP" };
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan ConvertCooldown = TimeSpan.FromSeconds(10);
        private const int MaxPlacementsPerMinute = 6;

        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly Thread _worker;
        private readonly Queue<DateTime> _placements = new Queue<DateTime>();
        private readonly object _settingsLock = new object();

        private Settings _settings;
        private volatile bool _stopping;
        private volatile bool _forcePlace;
        private volatile bool _covered;       // in TV c'è un'altra finestra
        private DateTime _lastRaise = DateTime.MinValue;
        private IntPtr _lastAbove;
        private long _tracked;               // HWND della finestra media già convertita
        private IntPtr _trackedCore;
        private bool _disabledByUs;
        private bool _announced;
        private DateTime _lastConvertAttempt = DateTime.MinValue;
        private string _status = L.T("Starting");

        public event Action<string> StatusChanged;
        public event Action<string> Notification;

        /// <summary>La finestra media agganciata non esiste più (chiusa in JW Library).</summary>
        public event Action MediaWindowClosed;

        public MediaWindowService(Settings settings)
        {
            _settings = settings.Clone();
            _worker = new Thread(Run) { IsBackground = true, Name = "JWFixMonitor worker" };
        }

        public IntPtr Tracked => new IntPtr(Interlocked.Read(ref _tracked));

        public string Status => _status;

        public void Start() => _worker.Start();

        public void UpdateSettings(Settings settings)
        {
            lock (_settingsLock)
            {
                _settings = settings.Clone();
            }

            _forcePlace = true;
            Wake();
        }

        public void Wake() => _wake.Set();

        /// <summary>
        /// Un'altra finestra è in TV al posto di JW: la finestra media resta agganciata
        /// ma non viene più rimessa in primo piano. Togliendo la copertura torna davanti.
        /// </summary>
        public void SetCovered(bool covered)
        {
            _covered = covered;
            var frame = Tracked;
            if (covered && frame != IntPtr.Zero && Native.IsWindow(frame))
            {
                Native.SetWindowPos(frame, Native.HWND_NOTOPMOST, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            }

            if (covered)
            {
                Wake();
            }
            else
            {
                PlaceNow();
            }
        }

        public bool Covered => _covered;

        /// <summary>Lo schermo dove va la finestra media (o null se c'è un solo schermo).</summary>
        public MonitorDesc CurrentTarget()
        {
            var frame = Tracked;
            return Geometry.ChooseMonitor(Native.Monitors(), CurrentSettings().Choice,
                frame != IntPtr.Zero ? Native.MonitorDeviceOf(frame) : "");
        }

        public void PlaceNow()
        {
            lock (_placements)
            {
                _placements.Clear();
            }

            _forcePlace = true;
            Wake();
        }

        public void Dispose()
        {
            _stopping = true;
            _wake.Set();
            _worker.Join(TimeSpan.FromSeconds(3));
            Release("chiusura");
        }

        // ------------------------------------------------------------------

        private void Run()
        {
            Log.Write("Servizio avviato");

            while (!_stopping)
            {
                _wake.WaitOne(PollInterval);
                if (_stopping)
                {
                    break;
                }

                // raggruppa raffiche di eventi in un solo controllo
                Thread.Sleep(250);
                _wake.Reset();

                try
                {
                    Tick();
                }
                catch (ElementNotAvailableException)
                {
                    // la finestra sta cambiando: riprovo al prossimo giro
                }
                catch (Exception ex)
                {
                    Log.Write("Errore: " + ex);
                }
            }
        }

        private Settings CurrentSettings()
        {
            lock (_settingsLock)
            {
                return _settings.Clone();
            }
        }

        private void Tick()
        {
            var s = CurrentSettings();

            if (!s.Enabled)
            {
                Release("pausa");
                SetStatus(L.T("Paused"));
                return;
            }

            var frame = Tracked;
            if (frame != IntPtr.Zero && !Native.IsWindow(frame))
            {
                Log.Write("La finestra media è stata chiusa");
                ForgetTracked();
                frame = IntPtr.Zero;
                MediaWindowClosed?.Invoke();
            }

            if (frame == IntPtr.Zero)
            {
                if (!FindMediaWindow(out frame, out var core))
                {
                    SetStatus(IsJwlRunning() ? L.T("WaitingMedia") : L.T("JwNotRunning"));
                    return;
                }

                if (!IsConverted(frame))
                {
                    if (!TryConvert(frame, core))
                    {
                        return;
                    }
                }

                Log.Write("Finestra media trovata e pronta");
                _trackedCore = core;
                _announced = false;
                _forcePlace = true; // applica sempre bordo e ritaglio alla finestra nuova
                Interlocked.Exchange(ref _tracked, frame.ToInt64());
            }

            if (_covered)
            {
                // JW resta dietro all'altra finestra finché non si torna a JW
                if (Native.IsTopMost(frame))
                {
                    Native.SetWindowPos(frame, Native.HWND_NOTOPMOST, 0, 0, 0, 0,
                        Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                }

                SetStatus(L.T("OtherOnTv"));
                return;
            }

            Place(frame, s);
        }

        // ---------------------------- ricerca ------------------------------

        /// <summary>
        /// La finestra media è una ApplicationFrameWindow "sempre in primo piano"
        /// che contiene una CoreWindow del processo JW Library.
        /// </summary>
        private static bool FindMediaWindow(out IntPtr frame, out IntPtr core)
        {
            var candidates = Native.TopLevelWindows();
            foreach (var h in Native.RootWindowsOnMonitors())
            {
                if (!candidates.Contains(h))
                {
                    candidates.Add(h);
                }
            }

            foreach (var h in candidates)
            {
                if (!Native.IsWindowVisible(h) || !Native.IsTopMost(h) ||
                    Native.ClassOf(h) != "ApplicationFrameWindow")
                {
                    continue;
                }

                var c = Native.FindWindowEx(h, IntPtr.Zero, "Windows.UI.Core.CoreWindow", null);
                if (c != IntPtr.Zero && IsJwlProcess(c))
                {
                    frame = h;
                    core = c;
                    return true;
                }
            }

            frame = core = IntPtr.Zero;
            return false;
        }

        private static bool IsJwlProcess(IntPtr hwnd)
        {
            Native.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0)
            {
                return false;
            }

            try
            {
                using (var p = Process.GetProcessById((int)pid))
                {
                    return Array.IndexOf(JwlProcesses, p.ProcessName) >= 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool IsJwlRunning()
        {
            foreach (var name in JwlProcesses)
            {
                var procs = Process.GetProcessesByName(name);
                var found = procs.Length > 0;
                foreach (var p in procs)
                {
                    p.Dispose();
                }

                if (found)
                {
                    return true;
                }
            }

            return false;
        }

        // --------------------------- conversione ---------------------------

        /// <summary>
        /// Dopo Win+Shift+Invio la finestra diventa spostabile: UI Automation espone
        /// il TransformPattern. È lo stesso segnale usato (con successo) da JwlMediaWin.
        /// </summary>
        private static bool IsConverted(IntPtr frame)
        {
            try
            {
                var el = AutomationElement.FromHandle(frame);
                return (bool)el.GetCurrentPropertyValue(AutomationElement.IsTransformPatternAvailableProperty);
            }
            catch
            {
                return false;
            }
        }

        private bool TryConvert(IntPtr frame, IntPtr core)
        {
            // pausa tra un tentativo e l'altro: un secondo Win+Shift+Invio
            // rimetterebbe la finestra a schermo intero
            if (DateTime.UtcNow - _lastConvertAttempt < ConvertCooldown)
            {
                return false;
            }

            _lastConvertAttempt = DateTime.UtcNow;
            SetStatus(L.T("Fixing"));

            if (!Native.IsWindowEnabled(frame))
            {
                Native.EnableWindow(frame, true);
            }

            var previous = Native.GetForegroundWindow();

            if (!Focus(frame, core))
            {
                Log.Write("Non riesco a dare il fuoco alla finestra media, riprovo tra poco");
                return false;
            }

            if (!Native.SendWinShiftEnter())
            {
                Log.Write("SendInput non riuscito");
                return false;
            }

            for (var i = 0; i < 20; i++)
            {
                Thread.Sleep(200);
                if (IsConverted(frame))
                {
                    RestoreForeground(previous, frame, core);
                    return true;
                }
            }

            Log.Write("La finestra non è uscita dallo schermo intero, riprovo tra 10 secondi");
            return false;
        }

        private static bool Focus(IntPtr frame, IntPtr core)
        {
            Native.ForceForeground(frame);

            try
            {
                AutomationElement.FromHandle(core).SetFocus();
            }
            catch
            {
                // non tutte le versioni lo permettono: controllo sotto
            }

            Thread.Sleep(100);

            var fg = Native.GetForegroundWindow();
            if (fg == frame || fg == core || Native.GetAncestor(fg, Native.GA_ROOT) == frame)
            {
                return true;
            }

            try
            {
                return AutomationElement.FromHandle(core).Current.HasKeyboardFocus;
            }
            catch
            {
                return false;
            }
        }

        private static void RestoreForeground(IntPtr previous, IntPtr frame, IntPtr core)
        {
            if (previous != IntPtr.Zero && previous != frame && previous != core && Native.IsWindow(previous))
            {
                Native.ForceForeground(previous);
            }
        }

        // --------------------------- posizione -----------------------------

        private void Place(IntPtr frame, Settings s)
        {
            var monitors = Native.Monitors();
            var target = Geometry.ChooseMonitor(monitors, s.Choice, Native.MonitorDeviceOf(frame));

            if (target == null)
            {
                // TV spenta o scollegata: non lascio una finestra bloccata e in primo
                // piano sopra lo schermo principale
                Release("nessuno schermo secondario");
                SetStatus(L.T("NoSecondary"));
                return;
            }

            var force = _forcePlace;
            _forcePlace = false;

            if (!force && IsPlaced(frame, target, s))
            {
                StayOnTop(frame, target, s);
                SetStatus(L.F("MediaOn", target.Label));
                return;
            }

            if (!AllowPlacement())
            {
                SetStatus(L.T("TooMany"));
                return;
            }

            if (Native.IsIconic(frame))
            {
                Native.ShowWindow(frame, Native.SW_RESTORE);
            }

            Native.SetBorderless(frame, true);

            var zOrder = s.TopMost ? Native.HWND_TOPMOST : Native.HWND_NOTOPMOST;
            const uint flags = Native.SWP_NOACTIVATE | Native.SWP_NOCOPYBITS |
                               Native.SWP_NOSENDCHANGING | Native.SWP_NOOWNERZORDER;

            // più passaggi: se lo schermo ha una scala diversa, JW Library si
            // ridimensiona da solo dopo lo spostamento e va corretto
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var wr = Native.WindowRect(frame);
                if (wr == null)
                {
                    return;
                }

                var insets = Geometry.Insets(wr.Value, Native.VisibleFrame(frame));

                // il bordo da 1px di Windows 11 sta dentro la cornice visibile: lo
                // spingo fuori dallo schermo e ritaglio quello che sborda
                var border = Native.VisibleBorderThickness(frame);
                var want = Geometry.TargetWindowRect(Geometry.Grow(target.Bounds, border), insets);

                Native.SetWindowPos(frame, zOrder, want.Left, want.Top, want.Width, want.Height, flags);
                Native.ClipWindow(frame, border > 0 ? Geometry.RelativeTo(target.Bounds, want) : (Box?)null);
                Thread.Sleep(attempt == 0 ? 300 : 150);

                if (IsPlaced(frame, target, s, checkEnabled: false))
                {
                    break;
                }
            }

            // finestra disabilitata = niente clic, niente pulsanti, niente
            // ridimensionamenti accidentali
            Native.EnableWindow(frame, false);
            _disabledByUs = true;

            var placed = IsPlaced(frame, target, s);
            Log.Write($"Posizionata su {target.Label} {target.Bounds}: " +
                      (placed ? "OK" : $"non precisa, finestra {Native.VisibleFrame(frame)}"));

            SetStatus(placed ? L.F("MediaOn", target.Label) : L.T("NotPrecise"));
            StayOnTop(frame, target, s);

            if (placed && !_announced && s.Notifications)
            {
                _announced = true;
                Notification?.Invoke(L.F("MediaPlaced", target.Label));
            }
        }

        private static bool IsPlaced(IntPtr frame, MonitorDesc target, Settings s, bool checkEnabled = true)
        {
            var visible = Native.VisibleFrame(frame);
            var wr = Native.WindowRect(frame);
            if (wr == null)
            {
                return false;
            }

            // la cornice visibile include il bordo di Windows 11, spinto apposta fuori dallo schermo
            var ok = visible != null
                ? Geometry.Matches(visible.Value, Geometry.Grow(target.Bounds, Native.VisibleBorderThickness(frame)))
                : Geometry.Matches(wr.Value, Geometry.TargetWindowRect(target.Bounds, Geometry.DefaultInsets));

            return ok &&
                   Native.IsTopMost(frame) == s.TopMost &&
                   !Native.IsIconic(frame) &&
                   (!checkEnabled || !Native.IsWindowEnabled(frame));
        }

        /// <summary>
        /// Un'altra finestra "sempre in primo piano" sopra la finestra media sulla TV
        /// (es. il bordo verde di Zoom durante la condivisione): rimetto JW in cima.
        /// Prima, a schermo intero UWP, JW stava sopra a tutto da sola.
        /// </summary>
        private void StayOnTop(IntPtr frame, MonitorDesc target, Settings s)
        {
            if (!s.TopMost || DateTime.UtcNow - _lastRaise < TimeSpan.FromSeconds(1))
            {
                return; // al massimo una volta al secondo: niente gara con chi si rimette sopra
            }

            var above = Native.WindowAbove(frame, target.Bounds);
            if (above == IntPtr.Zero)
            {
                return;
            }

            _lastRaise = DateTime.UtcNow;
            if (above != _lastAbove)
            {
                _lastAbove = above;
                Log.Write($"Sopra la finestra media sulla TV c'era \"{Native.TitleOf(above)}\" " +
                          $"({Native.ClassOf(above)}, {Native.ProcessNameOf(above)}): rimetto JW in cima");
            }

            Native.SetWindowPos(frame, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        private bool AllowPlacement()
        {
            lock (_placements)
            {
                var now = DateTime.UtcNow;
                while (_placements.Count > 0 && now - _placements.Peek() > TimeSpan.FromMinutes(1))
                {
                    _placements.Dequeue();
                }

                if (_placements.Count >= MaxPlacementsPerMinute)
                {
                    return false;
                }

                _placements.Enqueue(now);
                return true;
            }
        }

        // ------------------------------------------------------------------

        /// <summary>Rende di nuovo cliccabile la finestra (pausa, uscita, TV spenta).</summary>
        private void Release(string reason)
        {
            var frame = Tracked;
            if (frame == IntPtr.Zero || !_disabledByUs || !Native.IsWindow(frame))
            {
                return;
            }

            Native.EnableWindow(frame, true);
            Native.SetBorderless(frame, false);
            Native.ClipWindow(frame, null);
            _disabledByUs = false;
            _announced = false;

            if (reason != "chiusura")
            {
                Native.SetWindowPos(frame, Native.HWND_NOTOPMOST, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            }

            Log.Write("Finestra media sbloccata: " + reason);
        }

        private void ForgetTracked()
        {
            Interlocked.Exchange(ref _tracked, 0);
            _trackedCore = IntPtr.Zero;
            _disabledByUs = false;
            _announced = false;
        }

        private void SetStatus(string status)
        {
            if (status == _status)
            {
                return;
            }

            _status = status;
            StatusChanged?.Invoke(status);
        }
    }
}
