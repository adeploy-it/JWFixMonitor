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
        private bool _offerReopen;   // l'ultima notifica mostrata propone la riapertura
        private int _reopening;      // 1 mentre la riapertura è in corso

        public TrayApp()
        {
            _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            _settings = Settings.Load();

            _statusItem = new ToolStripMenuItem("Avvio…") { Enabled = false };
            _enabledItem = new ToolStripMenuItem("Attivo", null, (s, e) => Toggle(ref _settings.Enabled));
            _monitorMenu = new ToolStripMenuItem("Schermo della finestra media");
            _topMostItem = new ToolStripMenuItem("Sempre in primo piano", null, (s, e) => Toggle(ref _settings.TopMost));
            _notifyItem = new ToolStripMenuItem("Mostra notifiche", null, (s, e) => Toggle(ref _settings.Notifications));
            _startupItem = new ToolStripMenuItem("Avvia con Windows", null, (s, e) =>
            {
                Startup.Set(!Startup.IsEnabled, Application.ExecutablePath);
                RefreshMenu();
            });

            var menu = new ContextMenuStrip();
            menu.Items.Add(_statusItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_enabledItem);
            menu.Items.Add(_monitorMenu);
            menu.Items.Add(_topMostItem);
            menu.Items.Add(_notifyItem);
            menu.Items.Add(_startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Sistema di nuovo adesso", null, (s, e) => _service.PlaceNow());
            menu.Items.Add("Riapri finestra media", null, (s, e) => Reopen());
            menu.Items.Add("Apri il registro (log)", null, (s, e) => OpenLog());
            menu.Items.Add("Esci", null, (s, e) => ExitThread());
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
            _service.MediaWindowClosed += () => _ui.Post(_ => ShowBalloon(
                "La finestra media di JW Library è stata chiusa. Clicca per riaprirla.", offerReopen: true, timeoutMs: 10000), null);
            _icon.BalloonTipClicked += (s, e) =>
            {
                if (_offerReopen)
                {
                    _offerReopen = false;
                    Reopen();
                }
            };
            _icon.BalloonTipClosed += (s, e) => _offerReopen = false;

            // eventi di Windows: invece di controllare di continuo, mi sveglio quando
            // compare una finestra o quando quella media si sposta o si chiude
            _winEventProc = OnWinEvent;
            const uint hookFlags = Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS;
            _hooks = new[]
            {
                Native.SetWinEventHook(Native.EVENT_OBJECT_SHOW, Native.EVENT_OBJECT_SHOW, IntPtr.Zero, _winEventProc, 0, 0, hookFlags),
                Native.SetWinEventHook(Native.EVENT_OBJECT_DESTROY, Native.EVENT_OBJECT_DESTROY, IntPtr.Zero, _winEventProc, 0, 0, hookFlags),
                Native.SetWinEventHook(Native.EVENT_OBJECT_LOCATIONCHANGE, Native.EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, _winEventProc, 0, 0, hookFlags),
            };

            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            Log.Write("JWFixMonitor avviato. Schermi: " + string.Join(", ",
                Native.Monitors().ConvertAll(m => m.Device + " " + m.Bounds + (m.IsPrimary ? " principale" : ""))));

            _service.Start();
            RefreshMenu();
        }

        private void ShowBalloon(string text, bool offerReopen, int timeoutMs = 3000)
        {
            _offerReopen = offerReopen;
            _icon.ShowBalloonTip(timeoutMs, "JWFixMonitor", text, ToolTipIcon.Info);
        }

        /// <summary>Riapre la finestra media in JW Library, su un thread a parte; una volta sola alla volta.</summary>
        private void Reopen()
        {
            if (Interlocked.CompareExchange(ref _reopening, 1, 0) != 0)
            {
                return;
            }

            var thread = new Thread(() =>
            {
                try
                {
                    if (JwlReopener.TryReopen(out var error))
                    {
                        _service.PlaceNow();
                    }
                    else
                    {
                        Log.Write("Riapertura finestra media non riuscita: " + error);
                        _ui.Post(_ => ShowBalloon(
                            "Non riesco a riaprirla (" + error + "): in JW Library vai su Home → Impostazioni → Riapri",
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

        protected override void ExitThreadCore()
        {
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
            RefreshMenu();
        }

        private void RefreshMenu()
        {
            _enabledItem.Checked = _settings.Enabled;
            _topMostItem.Checked = _settings.TopMost;
            _notifyItem.Checked = _settings.Notifications;
            _startupItem.Checked = Startup.IsEnabled;
            ShowStatus(_service?.Status ?? "Avvio…");

            _monitorMenu.DropDownItems.Clear();

            var auto = new ToolStripMenuItem("Automatico (schermo secondario)", null, (s, e) => ChooseMonitor(""))
            {
                Checked = string.IsNullOrEmpty(_settings.TargetDevice)
            };
            _monitorMenu.DropDownItems.Add(auto);
            _monitorMenu.DropDownItems.Add(new ToolStripSeparator());

            var found = false;
            foreach (var m in Native.Monitors())
            {
                var device = m.Device;
                var selected = string.Equals(device, _settings.TargetDevice, StringComparison.OrdinalIgnoreCase);
                found |= selected;
                _monitorMenu.DropDownItems.Add(new ToolStripMenuItem(m.Label, null, (s, e) => ChooseMonitor(device))
                {
                    Checked = selected
                });
            }

            if (!string.IsNullOrEmpty(_settings.TargetDevice) && !found)
            {
                _monitorMenu.DropDownItems.Add(new ToolStripMenuItem(
                    "Schermo scelto non collegato (" + _settings.TargetDevice.TrimStart('\\', '.') + ")")
                {
                    Enabled = false,
                    Checked = true
                });
            }
        }

        private void ChooseMonitor(string device)
        {
            _settings.TargetDevice = device;
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
                MessageBox.Show("Non riesco ad aprire il registro:\n" + ex.Message, "JWFixMonitor");
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
