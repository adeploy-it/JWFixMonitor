using System;
using System.Drawing;
using System.Windows.Forms;

namespace JWFixMonitor
{
    /// <summary>Pannellino: scegli una finestra da mandare in TV al posto di JW, poi torna a JW.</summary>
    internal sealed class SwapPanel : Form
    {
        private readonly TvSwap _swap;
        private readonly MediaWindowService _service;
        private readonly Action<string> _notify;
        private readonly ListView _list;
        private readonly Button _send;
        private readonly Button _back;
        private readonly Label _state;

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
            MinimumSize = new Size(420, 300);
            Size = new Size(520, 380);
            Font = new Font("Segoe UI", 10f);

            _state = new Label { Dock = DockStyle.Top, Height = 32, Padding = new Padding(8, 8, 8, 0) };

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            _list.Columns.Add("Finestra", 330);
            _list.Columns.Add("Programma", 140);
            _list.DoubleClick += (s, e) => SendSelected();
            _list.SelectedIndexChanged += (s, e) => _send.Enabled = _list.SelectedItems.Count == 1;
            _list.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    SendSelected();
                }
            };

            _send = new Button { Text = "Manda in TV", AutoSize = true, Enabled = false };
            _send.Click += (s, e) => SendSelected();
            _back = new Button { Text = "Torna a JW (Ctrl+Alt+J)", AutoSize = true };
            _back.Click += (s, e) => BackToJw();
            var refresh = new Button { Text = "Aggiorna", AutoSize = true };
            refresh.Click += (s, e) => RefreshList();

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                Padding = new Padding(6),
                FlowDirection = FlowDirection.LeftToRight
            };
            buttons.Controls.AddRange(new Control[] { _send, _back, refresh });

            Controls.Add(_list);
            Controls.Add(buttons);
            Controls.Add(_state);

            AcceptButton = _send;
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    Hide();
                }
            };
        }

        /// <summary>Mostra il pannello sopra l'area di notifica, sullo schermo principale.</summary>
        public void ShowPanel()
        {
            RefreshList();
            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 12, wa.Bottom - Height - 12);
            Show();
            Activate();
            _list.Focus();
        }

        public void RefreshList()
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var h in Native.SendableWindows(_service.Tracked))
            {
                var item = new ListViewItem(Native.TitleOf(h)) { Tag = h };
                item.SubItems.Add(Native.ProcessNameOf(h));
                if (h == _swap.Current)
                {
                    item.Font = new Font(_list.Font, FontStyle.Bold);
                }

                _list.Items.Add(item);
            }

            _list.EndUpdate();
            _send.Enabled = false;
            RefreshState();
        }

        public void RefreshState()
        {
            var current = _swap.Current;
            _state.Text = current != IntPtr.Zero
                ? "In TV: " + Native.TitleOf(current)
                : "In TV: JW Library. Scegli una finestra e premi \"Manda in TV\".";
            _back.Enabled = current != IntPtr.Zero;
        }

        private void SendSelected()
        {
            if (_list.SelectedItems.Count != 1)
            {
                return;
            }

            var hwnd = (IntPtr)_list.SelectedItems[0].Tag;
            if (_swap.Send(hwnd, out var error))
            {
                Hide(); // via dai piedi: la finestra scelta ora è in primo piano in TV
            }
            else
            {
                _notify("Non riesco a mandarla in TV: " + error);
                RefreshList();
            }
        }

        private void BackToJw()
        {
            _swap.Restore("pannello");
            RefreshList();
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
    }
}
