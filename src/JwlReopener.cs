using System;
using System.Threading;
using System.Windows.Automation;

namespace JWFixMonitor
{
    /// <summary>
    /// Riapre la finestra media di JW Library facendo quello che farebbe l'utente:
    /// Home → Impostazioni → "Riapri" → Chiudi. JW resta su Home: andare su Home mette
    /// in pausa un video in corso e "Indietro" non è affidabile. Usa UI Automation, quindi
    /// dipende dall'interfaccia (e dalla lingua italiana) di JW Library 15.x.
    /// Sincrono (5–15 s): da chiamare fuori dal thread dell'interfaccia.
    /// </summary>
    internal static class JwlReopener
    {
        // nomi dell'interfaccia di JW Library 15.10.9 (italiano)
        private const string MainTitle = "JW Library";
        private const string HomeId = "navigation_home";
        private const string SettingsName = "Impostazioni";
        private const string SettingsClass = "AppBarButton";
        private const string ReopenName = "Riapri";
        private const string SecondScreenToggleName = "Riproduci video sullo schermo secondario";
        private const string CloseId = "CloseButton";

        public static bool TryReopen(out string error)
        {
            error = null;
            var frame = FindMainFrame();
            if (frame == IntPtr.Zero)
            {
                error = "JW Library non è avviato";
                return false;
            }

            var previous = Native.GetForegroundWindow();
            var wasMinimized = Native.IsIconic(frame);

            try
            {
                Log.Write("Riapertura finestra media: inizio");
                Native.ShowWindow(frame, Native.SW_RESTORE);
                Native.ForceForeground(frame);
                Thread.Sleep(1500); // JW sospesa: le serve un attimo per esporre l'interfaccia

                var root = AutomationElement.FromHandle(frame);

                // Impostazioni già aperte? Allora "Riapri" è già lì
                var reopen = Find(root, ByName(ReopenName), 500);
                if (reopen == null && !OpenSettings(root, out error))
                {
                    return false;
                }

                reopen = reopen ?? Find(root, ByName(ReopenName), 3000);
                if (reopen != null)
                {
                    Invoke(reopen);
                    Log.Write("Riapertura finestra media: premuto Riapri");
                }
                else
                {
                    var toggle = Find(root, ByName(SecondScreenToggleName), 1000);
                    object pattern = null;
                    var tp = toggle != null && toggle.TryGetCurrentPattern(TogglePattern.Pattern, out pattern)
                        ? (TogglePattern)pattern
                        : null;
                    if (tp == null || tp.Current.ToggleState == ToggleState.On)
                    {
                        error = "Non trovo il pulsante Riapri";
                        return false;
                    }

                    tp.Toggle();
                    Log.Write("Riapertura finestra media: acceso lo schermo secondario");
                }

                Thread.Sleep(800);
                var close = Find(root, ById(CloseId), 2000);
                if (close != null)
                {
                    Invoke(close);
                    Thread.Sleep(500);
                }

                Log.Write("Riapertura finestra media: fatto");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Log.Write("Riapertura finestra media non riuscita: " + ex);
                return false;
            }
            finally
            {
                if (wasMinimized)
                {
                    Native.ShowWindow(frame, Native.SW_MINIMIZE);
                }

                if (previous != IntPtr.Zero && previous != frame && Native.IsWindow(previous))
                {
                    Native.ForceForeground(previous);
                }
            }
        }

        private static bool OpenSettings(AutomationElement root, out string error)
        {
            error = null;

            // la selezione via UI Automation non cambia pagina se si è in una
            // sotto-pagina di Home: serve un clic vero sulla casetta
            var home = Find(root, ById(HomeId), 3000);
            if (home == null)
            {
                error = "Non trovo il pulsante Home";
                return false;
            }

            var r = home.Current.BoundingRectangle;
            Native.ClickAt((int)(r.Left + r.Width / 2), (int)(r.Top + r.Height / 2));

            var settings = Find(root, new AndCondition(ByName(SettingsName),
                new PropertyCondition(AutomationElement.ClassNameProperty, SettingsClass)), 4000);
            if (settings == null)
            {
                error = "Non trovo il pulsante Impostazioni";
                return false;
            }

            Invoke(settings);
            Log.Write("Riapertura finestra media: aperte le Impostazioni");
            return true;
        }

        /// <summary>Finestra principale: cornice UWP "JW Library" non in primo piano fisso.</summary>
        private static IntPtr FindMainFrame()
        {
            foreach (var h in Native.TopLevelWindows())
            {
                if (Native.IsWindowVisible(h) && !Native.IsTopMost(h) &&
                    Native.ClassOf(h) == "ApplicationFrameWindow" && Native.TitleOf(h) == MainTitle)
                {
                    return h;
                }
            }

            return IntPtr.Zero;
        }

        private static Condition ByName(string name) =>
            new PropertyCondition(AutomationElement.NameProperty, name);

        private static Condition ById(string id) =>
            new PropertyCondition(AutomationElement.AutomationIdProperty, id);

        /// <summary>Cerca un elemento riprovando finché compare o scade il tempo.</summary>
        private static AutomationElement Find(AutomationElement root, Condition condition, int timeoutMs)
        {
            var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (true)
            {
                var el = root.FindFirst(TreeScope.Descendants, condition);
                if (el != null || DateTime.UtcNow >= until)
                {
                    return el;
                }

                Thread.Sleep(250);
            }
        }

        private static void Invoke(AutomationElement el) =>
            ((InvokePattern)el.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    }
}
