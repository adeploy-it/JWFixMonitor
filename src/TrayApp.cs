using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace JWFixMonitor
{
    /// <summary>Icona nell'area di notifica, menu e collegamento agli eventi di Windows.</summary>
    internal sealed class TrayApp : ApplicationContext
    {
        private readonly Settings _settings;
        private readonly MediaWindowService _service;
        private readonly NotifyIcon _icon;
        private readonly SynchronizationContext _ui;
        private readonly Native.WinEventProc _winEventProc; // riferimento tenuto vivo per il GC
        private readonly IntPtr[] _hooks;

        private readonly ToolStripMenuItem _statusItem;
        private readonly ToolStripMenuItem _enabledItem;
        private readonly ToolStripMenuItem _monitorMenu;
        private readonly ToolStripMenuItem _topMostItem;
        private readonly ToolStripMenuItem _notifyItem;
        private readonly ToolStripMenuItem _startupItem;
        private readonly TvSwap _swap;
        private readonly SwapPanel _panel;
        private readonly Hotkeys _hotkeys;
        private readonly ToolStripMenuItem _checkUpdatesItem;
        private readonly ToolStripMenuItem _downloadItem;
        private readonly UpdateChecker _updates;
        private Action _balloonClick; // cosa fa il clic sull'ultima notifica (riapri, scarica…)
        private int _reopening;      // 1 mentre la riapertura è in corso

        public TrayApp()
        {
            _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            _settings = Settings.Load();

            _statusItem = new ToolStripMenuItem(L.T("Starting")) { Enabled = false };
            _enabledItem = new ToolStripMenuItem(L.T("Enabled"), null, (s, e) => Toggle(ref _settings.Enabled));
            _monitorMenu = new ToolStripMenuItem(L.T("MonitorMenu"));
            _topMostItem = new ToolStripMenuItem(L.T("TopMost"), null, (s, e) => Toggle(ref _settings.TopMost));
            _notifyItem = new ToolStripMenuItem(L.T("Notifications"), null, (s, e) => Toggle(ref _settings.Notifications));
            _startupItem = new ToolStripMenuItem(L.T("StartWithWindows"), null, (s, e) =>
            {
                Startup.Set(!Startup.IsEnabled, Application.ExecutablePath);
                RefreshMenu();
            });
            _checkUpdatesItem = new ToolStripMenuItem(L.T("CheckUpdates"), null, (s, e) => Toggle(ref _settings.CheckUpdates));
            _downloadItem = new ToolStripMenuItem("", null, (s, e) => OpenDownload(_updates.Available)) { Visible = false };
            _downloadItem.Font = new Font(_downloadItem.Font, FontStyle.Bold);

            var menu = new ContextMenuStrip();
            menu.Items.Add(_downloadItem);
            menu.Items.Add(_statusItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_enabledItem);
            menu.Items.Add(_monitorMenu);
            menu.Items.Add(_topMostItem);
            menu.Items.Add(_notifyItem);
            menu.Items.Add(_startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("PlaceNow"), null, (s, e) => _service.PlaceNow());
            menu.Items.Add(L.T("ReopenMedia"), null, (s, e) => Reopen());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("WhatsOnTv"), null, (s, e) => _panel.ShowPanel());
            menu.Items.Add(L.T("BackToJwMenu"), null, (s, e) => BackToJw("menu"));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_checkUpdatesItem);
            menu.Items.Add(L.T("CheckUpdatesNow"), null, (s, e) => CheckUpdatesNow());
            menu.Items.Add(L.T("OpenLog"), null, (s, e) => OpenLog());
            menu.Items.Add(L.T("Exit"), null, (s, e) => ExitThread());
            menu.Opening += (s, e) => RefreshMenu();

            _icon = new NotifyIcon
            {
                Icon = LoadIcon(),
                Text = "JWFixMonitor",
                ContextMenuStrip = menu,
                Visible = true
            };

            _service = new MediaWindowService(_settings);
            _service.StatusChanged += status => _ui.Post(_ => ShowStatus(status), null);
            _service.Notification += text => _ui.Post(_ => ShowBalloon(text, offerReopen: false), null);

            // finestra media chiusa: propongo di riaprirla. È una domanda, non un
            // semplice avviso, quindi compare anche con "Mostra notifiche" spento
            _service.MediaWindowClosed += OnMediaWindowClosed;
            _icon.BalloonTipClicked += (s, e) =>
            {
                var action = _balloonClick;
                _balloonClick = null;
                action?.Invoke();
            };
            _icon.BalloonTipClosed += (s, e) => _balloonClick = null;

            // nuove versioni: controllo su GitHub all'avvio e ogni 12 ore
            _updates = new UpdateChecker(typeof(TrayApp).Assembly.GetName().Version);
            _updates.Found += release => _ui.Post(_ => OnUpdateFound(release), null);

            // un'altra finestra in TV al posto di JW: pannello (clic sull'icona) e scorciatoie
            _swap = new TvSwap(_service);
            _panel = new SwapPanel(_swap, _service, text => ShowBalloon(text, offerReopen: false, timeoutMs: 5000))
            {
                Icon = _icon.Icon
            };
            _icon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    _panel.ShowPanel();
                }
            };
            _hotkeys = new Hotkeys();
            _hotkeys.AddCtrlAlt(Keys.T, SendForegroundToTv);
            _hotkeys.AddCtrlAlt(Keys.J, () => BackToJw("Ctrl+Alt+J"));
            _hotkeys.AddCtrlAlt(Keys.P, () => _panel.ShowPanel());

            // eventi di Windows: invece di controllare di continuo, mi sveglio quando
            // compare una finestra o quando quella media si sposta o si chiude
            _winEventProc = OnWinEvent;
            const uint hookFlags = Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS;
            _hooks = new[]
            {
                Native.SetWinEventHook(Native.EVENT_OBJECT_SHOW, Native.EVENT_OBJECT_SHOW, IntPtr.Zero, _winEventProc, 0, 0, hookFlags),
                Native.SetWinEventHook(Native.EVENT_OBJECT_DESTROY, Native.EVENT_OBJECT_DESTROY, IntPtr.Zero, _winEventProc, 0, 0, hookFlags),
                Native.SetWinEventHook(Native.EVENT_OBJECT_LOCATIONCHANGE, Native.EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, _winEventProc, 0, 0, hookFlags),
                Native.SetWinEventHook(Native.EVENT_OBJECT_HIDE, Native.EVENT_OBJECT_HIDE, IntPtr.Zero, _winEventProc, 0, 0, hookFlags),
            };

            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            Log.Write("JWFixMonitor avviato. Schermi: " + string.Join(", ",
                Native.Monitors().ConvertAll(m => m.Device + " " + m.Name + " " + m.Model + " " + m.Bounds + (m.IsPrimary ? " principale" : ""))));

            // avvio automatico attivo: aggiorno il percorso, nel caso il programma sia stato spostato
            if (Startup.IsEnabled)
            {
                Startup.Set(true, Application.ExecutablePath);
            }

            _service.Start();
            if (_settings.CheckUpdates)
            {
                _updates.Start();
            }

            RefreshMenu();
        }

        private void ShowBalloon(string text, bool offerReopen, int timeoutMs = 3000) =>
            ShowBalloon(text, offerReopen ? Reopen : (Action)null, timeoutMs);

        private void ShowBalloon(string text, Action onClick, int timeoutMs)
        {
            _balloonClick = onClick;
            _icon.ShowBalloonTip(timeoutMs, "JWFixMonitor", text, ToolTipIcon.Info);
        }

        private static string CurrentVersion
        {
            get
            {
                var v = typeof(TrayApp).Assembly.GetName().Version;
                return v.Major + "." + v.Minor + "." + Math.Max(v.Build, 0);
            }
        }

        /// <summary>Trovata in automatico una versione nuova: la propongo (una volta per versione).</summary>
        private void OnUpdateFound(Release release)
        {
            RefreshMenu();
            ShowBalloon(L.F("UpdateAvailable", release.Tag.TrimStart('v', 'V'), CurrentVersion),
                () => OpenDownload(release), timeoutMs: 15000);
        }

        private void CheckUpdatesNow()
        {
            _updates.CheckNow((release, error) => _ui.Post(_ =>
            {
                RefreshMenu();
                if (release != null)
                {
                    OnUpdateFound(release);
                }
                else if (error != null)
                {
                    ShowBalloon(L.F("UpdateCheckFailed", error), offerReopen: false, timeoutMs: 5000);
                }
                else
                {
                    ShowBalloon(L.F("UpToDate", CurrentVersion), offerReopen: false);
                }
            }, null));
        }

        /// <summary>Apre nel browser la pagina della release, da cui scaricare il nuovo exe.</summary>
        private static void OpenDownload(Release release)
        {
            var url = release?.PageUrl ?? Updates.ReleasesPage;
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Write("Impossibile aprire " + url + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Chiamato dal servizio (thread di lavoro). Aspetto un attimo: se nel frattempo
        /// JW Library si è chiuso del tutto, o la finestra media è già tornata, non chiedo nulla.
        /// </summary>
        private void OnMediaWindowClosed()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(2000);
                if (_service.Tracked != IntPtr.Zero || !JwlReopener.IsMainWindowOpen())
                {
                    return;
                }

                _ui.Post(__ => ShowBalloon(
                    L.T("MediaClosedOffer"),
                    offerReopen: true, timeoutMs: 10000), null);
            });
        }

        /// <summary>Riapre la finestra media in JW Library, su un thread a parte; una volta sola alla volta.</summary>
        private void Reopen()
        {
            if (_service.Tracked != IntPtr.Zero)
            {
                ShowBalloon(L.T("MediaAlreadyOpen"), offerReopen: false);
                return;
            }

            if (Interlocked.CompareExchange(ref _reopening, 1, 0) != 0)
            {
                return;
            }

            var thread = new Thread(() =>
            {
                try
                {
                    if (JwlReopener.TryReopen(() => _service.Tracked != IntPtr.Zero, out var error))
                    {
                        _service.PlaceNow();
                    }
                    else
                    {
                        Log.Write("Riapertura finestra media non riuscita: " + error);
                        _ui.Post(_ => ShowBalloon(
                            L.F("ReopenFailed", error),
                            offerReopen: false, timeoutMs: 10000), null);
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _reopening, 0);
                }
            }) { IsBackground = true, Name = "JWFixMonitor riapertura" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        /// <summary>Ctrl+Alt+T: manda in TV la finestra in primo piano.</summary>
        private void SendForegroundToTv()
        {
            var fg = Native.GetForegroundWindow();
            var root = fg == IntPtr.Zero ? IntPtr.Zero : Native.GetAncestor(fg, Native.GA_ROOT);
            if (root == IntPtr.Zero || root == _panel.Handle)
            {
                _panel.ShowPanel();
                return;
            }

            if (!_swap.Send(root, out var error))
            {
                ShowBalloon(L.F("SendFailed", error), offerReopen: false, timeoutMs: 5000);
            }

            _panel.RefreshState();
        }

        private void BackToJw(string reason)
        {
            if (_swap.Current == IntPtr.Zero && !_service.Covered)
            {
                ShowBalloon(L.T("AlreadyJw"), offerReopen: false);
                return;
            }

            _swap.Restore(reason);
            if (_panel.Visible)
            {
                _panel.RefreshList();
            }
        }

        /// <summary>La finestra in TV è stata chiusa o nascosta (es. fine di Zoom): torna JW.</summary>
        private void OnSwappedWindowGone(IntPtr hwnd)
        {
            var timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += (s, e) =>
            {
                timer.Dispose();
                if (_swap.Current == hwnd && (!Native.IsWindow(hwnd) || !Native.IsWindowVisible(hwnd)))
                {
                    _swap.Restore("la finestra in TV è stata chiusa");
                    ShowBalloon(L.T("SwappedClosed"), offerReopen: false);
                    if (_panel.Visible)
                    {
                        _panel.RefreshList();
                    }
                }
            };
            timer.Start();
        }

        protected override void ExitThreadCore()
        {
            if (_swap.Current != IntPtr.Zero)
            {
                _swap.Restore("chiusura");
            }

            _hotkeys.Dispose();
            _panel.Dispose();
            _updates.Dispose();

            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;

            foreach (var hook in _hooks)
            {
                if (hook != IntPtr.Zero)
                {
                    Native.UnhookWinEvent(hook);
                }
            }

            _service.Dispose();
            _icon.Visible = false;
            _icon.Dispose();
            Log.Write("JWFixMonitor chiuso");
            base.ExitThreadCore();
        }

        // ------------------------------------------------------------------

        private void OnWinEvent(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            if (idObject != Native.OBJID_WINDOW || idChild != 0 || hwnd == IntPtr.Zero)
            {
                return;
            }

            if ((evt == Native.EVENT_OBJECT_DESTROY || evt == Native.EVENT_OBJECT_HIDE) && hwnd == _swap.Current)
            {
                OnSwappedWindowGone(hwnd);
                return;
            }

            switch (evt)
            {
                case Native.EVENT_OBJECT_SHOW:
                    if (Native.ClassOf(hwnd) == "ApplicationFrameWindow")
                    {
                        _service.Wake();
                    }

                    break;

                case Native.EVENT_OBJECT_DESTROY:
                case Native.EVENT_OBJECT_LOCATIONCHANGE:
                    if (hwnd == _service.Tracked)
                    {
                        _service.Wake();
                    }

                    break;
            }
        }

        private void OnDisplayChanged(object sender, EventArgs e)
        {
            Log.Write("Configurazione schermi cambiata");
            _service.PlaceNow();
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                _service.PlaceNow();
            }
        }

        private void Toggle(ref bool value)
        {
            value = !value;
            Apply();
        }

        private void Apply()
        {
            _settings.Save();
            _service.UpdateSettings(_settings);
            if (_settings.CheckUpdates)
            {
                _updates.Start();
            }
            else
            {
                _updates.Stop();
            }

            RefreshMenu();
        }

        private void RefreshMenu()
        {
            _enabledItem.Checked = _settings.Enabled;
            _topMostItem.Checked = _settings.TopMost;
            _notifyItem.Checked = _settings.Notifications;
            _startupItem.Checked = Startup.IsEnabled;
            _checkUpdatesItem.Checked = _settings.CheckUpdates;
            var available = _updates?.Available;
            _downloadItem.Visible = available != null;
            if (available != null)
            {
                _downloadItem.Text = L.F("DownloadUpdate", available.Tag.TrimStart('v', 'V'));
            }
            ShowStatus(_service?.Status ?? L.T("Starting"));

            _monitorMenu.DropDownItems.Clear();

            var choice = _settings.Choice;
            var auto = new ToolStripMenuItem(L.T("AutoSecondary"), null, (s, e) => ChooseMonitor(MonitorChoice.Auto))
            {
                Checked = choice.IsAuto
            };
            _monitorMenu.DropDownItems.Add(auto);
            _monitorMenu.DropDownItems.Add(new ToolStripSeparator());

            var monitors = Native.Monitors();
            var chosen = Geometry.FindChosen(monitors, choice);
            foreach (var m in monitors)
            {
                var pick = MonitorChoice.From(m);
                _monitorMenu.DropDownItems.Add(new ToolStripMenuItem(m.Label, null, (s, e) => ChooseMonitor(pick))
                {
                    Checked = m == chosen
                });
            }

            if (!choice.IsAuto && chosen == null)
            {
                _monitorMenu.DropDownItems.Add(new ToolStripMenuItem(
                    L.F("ChosenNotConnected", (choice.Device ?? "").TrimStart('\\', '.')))
                {
                    Enabled = false,
                    Checked = true
                });
            }
        }

        private void ChooseMonitor(MonitorChoice choice)
        {
            _settings.Choice = choice;
            Log.Write(choice.IsAuto ? "Schermo media: automatico"
                : $"Schermo media scelto: {choice.Device} {choice.Model} {choice.Width}x{choice.Height}");
            Apply();
        }

        private void ShowStatus(string status)
        {
            _statusItem.Text = status;
            var tip = "JWFixMonitor — " + status;
            _icon.Text = tip.Length > 63 ? tip.Substring(0, 62) + "…" : tip; // limite di Windows
        }

        private static void OpenLog()
        {
            try
            {
                if (!System.IO.File.Exists(Log.FilePath))
                {
                    Log.Write("Registro creato");
                }

                Process.Start(new ProcessStartInfo(Log.FilePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(L.F("LogOpenFailed", ex.Message), "JWFixMonitor");
            }
        }

        private static Icon LoadIcon()
        {
            try
            {
                using (var stream = typeof(TrayApp).Assembly.GetManifestResourceStream("JWFixMonitor.app.ico"))
                {
                    if (stream != null)
                    {
                        return new Icon(stream, SystemInformation.SmallIconSize);
                    }
                }
            }
            catch
            {
                // uso l'icona di sistema
            }

            return SystemIcons.Application;
        }
    }
}
