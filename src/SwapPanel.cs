using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace JWFixMonitor
{
    /// <summary>
    /// Pannellino con le anteprime dal vivo delle finestre: un clic manda la finestra in
    /// TV al posto di JW, la casella "JW Library" (o il pulsante in alto) torna a JW.
    /// Resta aperto dopo l'invio per tornare a JW al volo.
    /// </summary>
    internal sealed class SwapPanel : Form
    {
        private readonly TvSwap _swap;
        private readonly MediaWindowService _service;
        private readonly Action<string> _notify;
        private readonly FlowLayoutPanel _grid;
        private readonly Button _back;
        private readonly Label _state;
        private readonly Timer _timer;
        private readonly List<Tile> _tiles = new List<Tile>();
        private readonly RemoteView _remote;
        private const int RemoteHeight = 400;

        public SwapPanel(TvSwap swap, MediaWindowService service, Action<string> notify)
        {
            _swap = swap;
            _service = service;
            _notify = notify;

            Text = L.T("PanelTitle");
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = true;      // si riduce a icona nella barra delle applicazioni
            MaximizeBox = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = true;
            MinimumSize = new Size(520, 360);
            Size = new Size(820, 600); // 3 colonne di caselle
            Font = new Font("Segoe UI", 10f);

            _back = new Button
            {
                Text = L.T("BackButton"),
                Dock = DockStyle.Right,
                Width = 250,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold)
            };
            _back.Click += (s, e) => BackToJw();

            _state = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 8, 0) };

            var top = new Panel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(6) };
            top.Controls.Add(_state);
            top.Controls.Add(_back);

            _grid = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(6) };
            _grid.Scroll += (s, e) => UpdateThumbs();
            _grid.MouseWheel += (s, e) => UpdateThumbsLater();
            _grid.Layout += (s, e) => UpdateThumbsLater();

            _remote = new RemoteView(this) { Dock = DockStyle.Top, Height = RemoteHeight, Visible = false };

            Controls.Add(_grid);
            Controls.Add(_remote);
            Controls.Add(top);

            _timer = new Timer { Interval = 1500 };
            _timer.Tick += (s, e) => Sync();

            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape && !_remote.Visible)
                {
                    Hide();
                }
            };
        }

        /// <summary>Mostra il pannello in basso a destra sullo schermo principale.</summary>
        public void ShowPanel()
        {
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            var wasVisible = Visible;
            if (!wasVisible)
            {
                Show();
                var wa = Screen.PrimaryScreen.WorkingArea; // dopo Show: la dimensione è già scalata per i DPI
                Location = new Point(wa.Right - Width - 12, wa.Bottom - Height - 12);
            }

            Sync();
            if (!wasVisible)
            {
                _grid.AutoScrollPosition = Point.Empty; // si parte dall'alto: JW Library per prima
                UpdateThumbs();
            }

            Activate();
        }

        /// <summary>Riallinea elenco, evidenziazione e stato (chiamato anche dalle scorciatoie).</summary>
        public void RefreshState()
        {
            if (Visible)
            {
                Sync();
            }
        }

        public void RefreshList() => RefreshState();

        // ------------------------------------------------------------------

        private void Sync()
        {
            if (WindowState == FormWindowState.Minimized)
            {
                return; // ridotto a icona: niente da aggiornare né da ridimensionare
            }

            var media = _service.Tracked;
            var wanted = new List<IntPtr>();
            if (media != IntPtr.Zero && Native.IsWindow(media))
            {
                wanted.Add(media);
            }

            wanted.AddRange(Native.SendableWindows(media));

            // ordine stabile: chi c'era resta al suo posto, le finestre nuove vanno in fondo
            var old = _tiles.Select(t => t.Hwnd).ToList();
            var ordered = old.Where(wanted.Contains).Concat(wanted.Where(h => !old.Contains(h))).ToList();
            if (media != IntPtr.Zero && ordered.Remove(media))
            {
                ordered.Insert(0, media);
            }

            if (!ordered.SequenceEqual(old))
            {
                Rebuild(ordered, media);
            }

            var current = _swap.Current;
            foreach (var t in _tiles)
            {
                t.Title = t.IsJw ? L.T("JwTile") : Native.TitleOf(t.Hwnd);
                t.InTv = t.IsJw ? current == IntPtr.Zero : t.Hwnd == current;
                t.Minimized = Native.IsIconic(t.Hwnd);
                t.Invalidate();
            }

            _state.Text = current != IntPtr.Zero
                ? L.F("InTv", Native.TitleOf(current))
                : L.T("InTvJw");
            _back.Enabled = current != IntPtr.Zero || _service.Covered;
            _back.BackColor = _back.Enabled ? Color.FromArgb(255, 214, 102) : SystemColors.Control;

            ShowRemote(current);
            UpdateThumbs();
        }

        private void Rebuild(List<IntPtr> windows, IntPtr media)
        {
            _grid.SuspendLayout();
            foreach (var t in _tiles)
            {
                t.Dispose();
            }

            _tiles.Clear();
            _grid.Controls.Clear();

            foreach (var h in windows)
            {
                var tile = new Tile(h, h == media, IconOf(h));
                tile.Thumb = Native.RegisterThumbnail(Handle, h);
                tile.Click += (s, e) => OnTileClick(tile);
                _tiles.Add(tile);
                _grid.Controls.Add(tile);
            }

            _grid.ResumeLayout();
        }

        /// <summary>Mostra (o toglie) l'anteprima grande di ciò che è in TV, allungando il pannello.</summary>
        private void ShowRemote(IntPtr current)
        {
            var show = current != IntPtr.Zero && Native.IsWindow(current);
            _remote.SetTarget(show ? current : IntPtr.Zero);
            if (show == _remote.Visible)
            {
                return;
            }

            var wa = Screen.FromControl(this).WorkingArea;
            if (show)
            {
                // più alto ma sempre dentro lo schermo
                var height = Math.Min(Height + RemoteHeight, wa.Height);
                var top = Math.Max(wa.Top, Math.Min(Top, wa.Bottom - height));
                SetBounds(Left, top, Width, height);
                _remote.Visible = true;
                _remote.Focus(); // tastiera e rotellina vanno alla finestra in TV
            }
            else
            {
                _remote.Visible = false;
                var height = Math.Max(MinimumSize.Height, Height - RemoteHeight);
                SetBounds(Left, Math.Min(Top + (Height - height), wa.Bottom - height), Width, height);
            }
        }

        /// <summary>Gira un tasto alla finestra in TV: la porto davanti un attimo e poi torno qui.</summary>
        internal void ForwardKey(Keys key)
        {
            var target = _swap.Current;
            if (target == IntPtr.Zero || !Native.IsWindow(target))
            {
                return;
            }

            var extended = key == Keys.Up || key == Keys.Down || key == Keys.Left || key == Keys.Right ||
                           key == Keys.PageUp || key == Keys.PageDown || key == Keys.Home || key == Keys.End ||
                           key == Keys.Insert || key == Keys.Delete;
            Native.ForceForeground(target);
            System.Threading.Thread.Sleep(30);
            Native.PressKey((byte)key, extended);
            System.Threading.Thread.Sleep(30);
            Native.ForceForeground(Handle);
            _remote.Focus();
        }

        /// <summary>Dopo un clic sulla TV il fuoco va là: lo riporto al pannello per la tastiera.</summary>
        internal void TakeFocusBackSoon()
        {
            var t = new Timer { Interval = 200 };
            t.Tick += (s, e) =>
            {
                t.Dispose();
                if (Visible && _remote.Visible)
                {
                    Native.ForceForeground(Handle);
                    _remote.Focus();
                }
            };
            t.Start();
        }

        private void UpdateThumbsLater()
        {
            if (IsHandleCreated && !IsDisposed)
            {
                BeginInvoke((Action)UpdateThumbs);
            }
        }

        /// <summary>Le miniature DWM sono disegnate sul modulo: le metto sopra le caselle visibili.</summary>
        private void UpdateThumbs()
        {
            if (IsDisposed)
            {
                return;
            }

            _remote.UpdateThumb();

            var view = _grid.RectangleToScreen(_grid.ClientRectangle);
            foreach (var t in _tiles)
            {
                var screen = t.RectangleToScreen(t.PreviewArea);
                var client = RectangleToClient(screen);
                var box = new Box(client.Left, client.Top, client.Right, client.Bottom);
                Native.ShowThumbnail(t.Thumb, box, Visible && !t.Minimized && view.Contains(screen));
            }
        }

        private void OnTileClick(Tile tile)
        {
            if (tile.IsJw)
            {
                BackToJw();
                return;
            }

            if (!_swap.Send(tile.Hwnd, out var error))
            {
                _notify(L.F("SendFailed", error));
            }

            Sync(); // il pannello resta aperto: da qui si torna a JW
        }

        private void BackToJw()
        {
            _swap.Restore("pannello");
            Sync();
        }

        private static Icon IconOf(IntPtr hwnd)
        {
            try
            {
                using (var p = Process.GetProcessById((int)Native.ProcessIdOf(hwnd)))
                {
                    return Icon.ExtractAssociatedIcon(p.MainModule.FileName);
                }
            }
            catch
            {
                return null;
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                _timer.Start();
            }
            else
            {
                // nascosto: niente miniature né controlli in giro
                _timer.Stop();
                _remote.SetTarget(IntPtr.Zero);
                foreach (var t in _tiles)
                {
                    t.Dispose();
                }

                _tiles.Clear();
                _grid.Controls.Clear();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Normal && Visible && IsHandleCreated)
            {
                Sync(); // anche al ritorno da ridotto a icona
            }
        }

        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            UpdateThumbs();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true; // lo nascondo e lo riuso
                Hide();
                return;
            }

            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                foreach (var t in _tiles)
                {
                    t.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Anteprima grande di ciò che è in TV, usata come telecomando: rotellina, clic e
        /// tasti vanno alla finestra vera nello stesso punto.
        /// </summary>
        private sealed class RemoteView : Control
        {
            private const int Pad = 6, HintH = 26;
            private readonly SwapPanel _panel;
            private IntPtr _target;
            private IntPtr _thumb;

            public RemoteView(SwapPanel panel)
            {
                _panel = panel;
                DoubleBuffered = true;
                Cursor = Cursors.Cross;
                SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.ResizeRedraw, true);
            }

            private Rectangle Area => new Rectangle(Pad, Pad, Math.Max(1, Width - 2 * Pad), Math.Max(1, Height - 2 * Pad - HintH));

            /// <summary>Dove sta davvero l'anteprima (stesse proporzioni della finestra), in coordinate del controllo.</summary>
            private Box Preview
            {
                get
                {
                    var wr = Native.WindowRect(_target);
                    var a = Area;
                    return wr == null ? new Box() : Geometry.Fit(wr.Value.Width, wr.Value.Height, new Box(a.Left, a.Top, a.Right, a.Bottom));
                }
            }

            public void SetTarget(IntPtr target)
            {
                if (target == _target)
                {
                    return;
                }

                Native.UnregisterThumbnail(_thumb);
                _thumb = IntPtr.Zero;
                _target = target;
                if (target != IntPtr.Zero && _panel.IsHandleCreated)
                {
                    _thumb = Native.RegisterThumbnail(_panel.Handle, target);
                }

                Invalidate();
            }

            public void UpdateThumb()
            {
                if (_thumb == IntPtr.Zero)
                {
                    return;
                }

                var p = Preview;
                var screen = RectangleToScreen(new Rectangle(p.Left, p.Top, p.Width, p.Height));
                var c = _panel.RectangleToClient(screen);
                Native.ShowThumbnail(_thumb, new Box(c.Left, c.Top, c.Right, c.Bottom), Visible && _panel.Visible && !p.IsEmpty);
            }

            private bool ToScreen(Point pt, out int x, out int y)
            {
                x = y = 0;
                var wr = Native.WindowRect(_target);
                return wr != null && Geometry.MapPoint(Preview, wr.Value, pt.X, pt.Y, out x, out y);
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                Focus(); // la rotellina arriva a chi ha il fuoco
                base.OnMouseEnter(e);
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                // Ctrl tenuto premuto dall'utente vale anche per la finestra in TV: zoom.
                // Un evento col nostro segno è uno dei nostri tornato indietro: lo ignoro
                if (Native.GetMessageExtraInfo() != Native.RemoteMark && ToScreen(e.Location, out var x, out var y))
                {
                    Native.WheelAt(x, y, e.Delta);
                }

                base.OnMouseWheel(e);
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                if (Native.GetMessageExtraInfo() != Native.RemoteMark && ToScreen(e.Location, out var x, out var y))
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        Native.LeftClickAt(x, y);
                    }
                    else if (e.Button == MouseButtons.Right)
                    {
                        Native.RightClickAt(x, y);
                    }

                    _panel.TakeFocusBackSoon();
                }

                base.OnMouseClick(e);
            }

            protected override bool IsInputKey(Keys keyData) => true; // anche frecce, Tab, Invio, Esc

            protected override void OnKeyDown(KeyEventArgs e)
            {
                var k = e.KeyCode;
                if (k != Keys.ShiftKey && k != Keys.ControlKey && k != Keys.Menu && k != Keys.LWin && k != Keys.RWin)
                {
                    _panel.ForwardKey(k);
                    e.Handled = e.SuppressKeyPress = true;
                }

                base.OnKeyDown(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Color.FromArgb(32, 32, 32));
                var hint = new Rectangle(Pad, Height - Pad - HintH, Width - 2 * Pad, HintH);
                TextRenderer.DrawText(g,
                    (Focused ? "● " : "") + L.T("RemoteHint"),
                    Font, hint, Focused ? Color.FromArgb(120, 220, 140) : Color.Gainsboro,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
            protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
            protected override void OnResize(EventArgs e) { base.OnResize(e); UpdateThumb(); }

            protected override void Dispose(bool disposing)
            {
                Native.UnregisterThumbnail(_thumb);
                _thumb = IntPtr.Zero;
                base.Dispose(disposing);
            }
        }

        /// <summary>Casella: anteprima (disegnata da DWM sopra) e titolo sotto.</summary>
        private sealed class Tile : Control
        {
            private const int W = 236, H = 176, Pad = 6, PreviewH = 128;
            private readonly Icon _icon;
            private readonly Font _bold = new Font("Segoe UI", 10f, FontStyle.Bold);
            private bool _hover;

            public Tile(IntPtr hwnd, bool isJw, Icon icon)
            {
                Hwnd = hwnd;
                IsJw = isJw;
                _icon = icon;
                Size = new Size(W, H);
                Margin = new Padding(6);
                Cursor = Cursors.Hand;
                DoubleBuffered = true;
                SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
                SetStyle(ControlStyles.Selectable, false); // il fuoco non fa scorrere l'elenco
            }

            public IntPtr Hwnd { get; }
            public bool IsJw { get; }
            public IntPtr Thumb;
            public string Title = "";
            public bool InTv;
            public bool Minimized;

            public Rectangle PreviewArea => new Rectangle(Pad, Pad, W - 2 * Pad, PreviewH);

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(_hover ? Color.FromArgb(229, 241, 251) : SystemColors.Window);
                g.FillRectangle(Brushes.Black, PreviewArea);

                if (Minimized || Thumb == IntPtr.Zero)
                {
                    var p = PreviewArea;
                    if (_icon != null)
                    {
                        g.DrawIcon(_icon, new Rectangle(p.Left + (p.Width - 32) / 2, p.Top + 30, 32, 32));
                    }

                    TextRenderer.DrawText(g, Minimized ? L.T("Minimized") : L.T("NoPreview"), Font,
                        new Rectangle(p.Left, p.Top + 72, p.Width, 24), Color.Gainsboro, TextFormatFlags.HorizontalCenter);
                }

                var text = new Rectangle(Pad, PreviewH + Pad + 2, W - 2 * Pad, H - PreviewH - Pad - 4);
                TextRenderer.DrawText(g, (InTv ? L.T("InTvBadge") : "") + Title, IsJw ? _bold : Font, text,
                    InTv ? Color.FromArgb(0, 120, 60) : SystemColors.ControlText,
                    TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter);

                using (var pen = new Pen(InTv ? Color.FromArgb(0, 160, 80) : Color.Silver, InTv ? 4 : 1))
                {
                    g.DrawRectangle(pen, 1, 1, W - 3, H - 3);
                }
            }

            protected override void Dispose(bool disposing)
            {
                Native.UnregisterThumbnail(Thumb);
                Thumb = IntPtr.Zero;
                if (disposing)
                {
                    _icon?.Dispose();
                    _bold.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
