using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace JWFixMonitor
{
    /// <summary>Scorciatoie da tastiera globali (RegisterHotKey) su una finestra invisibile.</summary>
    internal sealed class Hotkeys : NativeWindow, IDisposable
    {
        private readonly Dictionary<int, Action> _actions = new Dictionary<int, Action>();
        private int _nextId = 1;

        public Hotkeys()
        {
            CreateHandle(new CreateParams());
        }

        /// <summary>Registra Ctrl+Alt+tasto. Falso se un altro programma la usa già.</summary>
        public bool AddCtrlAlt(Keys key, Action action)
        {
            var id = _nextId++;
            if (!Native.RegisterHotKey(Handle, id, Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, (uint)key))
            {
                Log.Write($"Scorciatoia Ctrl+Alt+{key} non disponibile (già usata da un altro programma)");
                return false;
            }

            _actions[id] = action;
            return true;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && _actions.TryGetValue(m.WParam.ToInt32(), out var action))
            {
                action();
                return;
            }

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            foreach (var id in _actions.Keys)
            {
                Native.UnregisterHotKey(Handle, id);
            }

            _actions.Clear();
            DestroyHandle();
        }
    }
}
