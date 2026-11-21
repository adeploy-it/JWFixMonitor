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

        public SwapPanel(TvSwap swap, MediaWindowService service, Action<string> notify)
        {
            _swap = swap;
            _service = service;
            _notify = notify;

            Text = "JWFixMonitor — cosa va in TV";
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = false;
            MinimumSize = new Size(520, 360);
            Size = new Size(820, 600); // 3 colonne di caselle
            Font = new Font("Segoe UI", 10f);

            _back = new Button
            {
                Text = "◀  Torna a JW   (Ctrl+Alt+J)",
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

            Controls.Add(_grid);
            Controls.Add(top);

            _timer = new Timer { Interval = 1500 };
            _timer.Tick += (s, e) => Sync();

            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    Hide();
                }
            };
        }

        /// <summary>Mostra il pannello in basso a destra sullo schermo principale.</summary>
        public void ShowPanel()
        {
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
            var media = _service.Tracked;
            var wanted = new List<IntPtr>();
            if (media != IntPtr.Zero && Native.IsWindow(media))
            {
                wanted.Add(media);
            }

            wanted.AddRange(Native.SendableWindows(media));

            if (!wanted.SequenceEqual(_tiles.Select(t => t.Hwnd)))
            {
                Rebuild(wanted, media);
            }

            var current = _swap.Current;
            foreach (var t in _tiles)
            {
                t.Title = t.IsJw ? "JW Library — video" : Native.TitleOf(t.Hwnd);
                t.InTv = t.IsJw ? current == IntPtr.Zero : t.Hwnd == current;
                t.Minimized = Native.IsIconic(t.Hwnd);
                t.Invalidate();
            }

            _state.Text = current != IntPtr.Zero
                ? "In TV: " + Native.TitleOf(current)
                : "In TV: JW Library. Clicca una finestra per mandarla in TV.";
            _back.Enabled = current != IntPtr.Zero || _service.Covered;
            _back.BackColor = _back.Enabled ? Color.FromArgb(255, 214, 102) : SystemColors.Control;

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
                _notify("Non riesco a mandarla in TV: " + error);
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
                foreach (var t in _tiles)
                {
                    t.Dispose();
                }

                _tiles.Clear();
                _grid.Controls.Clear();
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

                    TextRenderer.DrawText(g, Minimized ? "ridotta a icona" : "anteprima non disponibile", Font,
                        new Rectangle(p.Left, p.Top + 72, p.Width, 24), Color.Gainsboro, TextFormatFlags.HorizontalCenter);
                }

                var text = new Rectangle(Pad, PreviewH + Pad + 2, W - 2 * Pad, H - PreviewH - Pad - 4);
                TextRenderer.DrawText(g, (InTv ? "● IN TV  " : "") + Title, IsJw ? _bold : Font, text,
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
