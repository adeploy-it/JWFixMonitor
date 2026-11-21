using System;

namespace JWFixMonitor
{
    /// <summary>
    /// Manda in TV un'altra finestra (PDF, seconda finestra di Zoom…) al posto della
    /// finestra media di JW, e la rimette dov'era quando si torna a JW. Una alla volta.
    /// Da usare dal thread dell'interfaccia.
    /// </summary>
    internal sealed class TvSwap
    {
        private readonly MediaWindowService _service;
        private IntPtr _window;
        private Native.WINDOWPLACEMENT _placement;
        private bool _wasTopMost;

        public TvSwap(MediaWindowService service)
        {
            _service = service;
        }

        /// <summary>La finestra in TV al posto di JW, o zero.</summary>
        public IntPtr Current => _window;

        public bool Send(IntPtr hwnd, out string error)
        {
            error = null;
            if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd))
            {
                error = L.T("WindowGone");
                return false;
            }

            if (hwnd == _window)
            {
                Native.ForceForeground(hwnd);
                return true;
            }

            if (!WindowFilter.CanSend(Native.FactsOf(hwnd, _service.Tracked)))
            {
                error = L.T("CannotSend");
                return false;
            }

            var target = _service.CurrentTarget();
            if (target == null)
            {
                error = L.T("NoTv");
                return false;
            }

            var placement = Native.PlacementOf(hwnd);
            if (placement == null)
            {
                error = L.T("NoPlacement");
                return false;
            }

            if (_window != IntPtr.Zero)
            {
                PutBack(); // un'altra finestra era già in TV: torna al suo posto
            }

            _window = hwnd;
            _placement = placement.Value;
            _wasTopMost = Native.IsTopMost(hwnd);
            _service.SetCovered(true);

            // la porto sulla TV a dimensione normale, poi la ingrandisco lì
            if (Native.IsIconic(hwnd) || Native.IsZoomed(hwnd))
            {
                Native.ShowWindow(hwnd, Native.SW_RESTORE);
            }

            var b = target.Bounds;
            Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, b.Left + 40, b.Top + 40,
                Math.Max(200, b.Width - 80), Math.Max(200, b.Height - 80), Native.SWP_NOOWNERZORDER);
            Native.ShowWindow(hwnd, Native.SW_MAXIMIZE);
            Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE);
            Native.ForceForeground(hwnd);

            Log.Write($"In TV al posto di JW: \"{Native.TitleOf(hwnd)}\" ({Native.ProcessNameOf(hwnd)}) su {target.Label}");
            return true;
        }

        /// <summary>Rimette la finestra dov'era e riporta JW davanti in TV.</summary>
        public void Restore(string reason)
        {
            if (_window == IntPtr.Zero)
            {
                _service.SetCovered(false);
                return;
            }

            PutBack();
            _service.SetCovered(false);
            Log.Write("Tornato a JW in TV: " + reason);
        }

        private void PutBack()
        {
            var hwnd = _window;
            _window = IntPtr.Zero;
            if (!Native.IsWindow(hwnd))
            {
                return;
            }

            if (!_wasTopMost)
            {
                Native.SetWindowPos(hwnd, Native.HWND_NOTOPMOST, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            }

            var wp = _placement;
            if (wp.showCmd == Native.SW_MINIMIZE || wp.showCmd == 2 /* SW_SHOWMINIMIZED */)
            {
                wp.showCmd = 7; // SW_SHOWMINNOACTIVE: torna nella barra senza rubare il fuoco
            }

            Native.SetWindowPlacement(hwnd, ref wp);
        }
    }
}
