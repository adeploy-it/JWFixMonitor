using System;
using System.Threading;
using System.Windows.Automation;

namespace JWFixMonitor
{
    /// <summary>
    /// Riapre la finestra media di JW Library facendo quello che farebbe l'utente:
    /// Home → Impostazioni → "Riapri" → Chiudi, poi torna alla sezione di prima (dove
    /// il player resta aperto) e, se un video andava, lo fa ripartire. Usa UI
    /// Automation, quindi dipende dall'interfaccia (e dalla lingua italiana) di
    /// JW Library 15.x. Sincrono (5–20 s): da chiamare fuori dal thread dell'interfaccia.
    /// </summary>
    internal static class JwlReopener
    {
        // nomi dell'interfaccia di JW Library 15.10.9 (italiano)
        private const string MainTitle = "JW Library";
        private const string HomeId = "navigation_home";
        private const string NavigationPrefix = "navigation_"; // _home, _bible, _library, _meetings, _personal_study
        private const string SettingsName = "Impostazioni";
        private const string SettingsClass = "AppBarButton";
        private const string ReopenName = "Riapri";
        private const string SecondScreenToggleName = "Riproduci video sullo schermo secondario";
        private const string CloseId = "CloseButton";
        private const string PlayButtonId = "TogglePlaybackButton";
        private const string PauseName = "Pausa";     // nome del pulsante mentre il video va
        private const string PlayName = "Riproduci";
        private const int NavColumnWidth = 48;        // larghezza delle icone del menu laterale chiuso

        private static readonly TimeSpan MediaReadyTimeout = TimeSpan.FromSeconds(10);

        /// <summary>La finestra principale di JW Library è aperta?</summary>
        public static bool IsMainWindowOpen() => FindMainFrame() != IntPtr.Zero;

        /// <param name="mediaReady">vero quando JWFixMonitor ha agganciato la nuova finestra media</param>
        public static bool TryReopen(Func<bool> mediaReady, out string error)
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
            AutomationElement root = null;
            var settingsOpen = false;

            try
            {
                Log.Write("Riapertura finestra media: inizio");
                if (wasMinimized)
                {
                    Native.ShowWindow(frame, Native.SW_RESTORE); // non tocco una finestra massimizzata
                }

                Native.ForceForeground(frame);
                Thread.Sleep(1500); // JW sospesa: le serve un attimo per esporre l'interfaccia

                root = AutomationElement.FromHandle(frame);

                // dove era l'utente: andare su Home mette in pausa il video, ma il
                // player resta aperto nella sua sezione e lì lo si ritrova
                var section = SelectedSection(root);
                var player = Find(root, ById(PlayButtonId), 300);
                var wasPlaying = player != null && player.Current.Name == PauseName;
                Log.Write($"Riapertura finestra media: sezione {section ?? "?"}, video in corso: {(wasPlaying ? "sì" : "no")}");

                // Impostazioni già aperte? Allora "Riapri" è già lì
                var reopen = Find(root, ByName(ReopenName), 500);
                settingsOpen = reopen != null;
                if (reopen == null)
                {
                    if (!OpenSettings(frame, root, out error))
                    {
                        return false;
                    }

                    settingsOpen = true;
                    reopen = Find(root, ByName(ReopenName), 3000);
                }

                if (reopen != null)
                {
                    Invoke(reopen);
                    Log.Write("Riapertura finestra media: premuto Riapri");
                }
                else if (!TurnOnSecondScreen(root))
                {
                    error = "Non trovo il pulsante Riapri";
                    return false;
                }

                // aspetto che JWFixMonitor abbia sistemato la nuova finestra: la
                // conversione sposta il fuoco e non va disturbata
                var until = DateTime.UtcNow + MediaReadyTimeout;
                while (!mediaReady() && DateTime.UtcNow < until)
                {
                    Thread.Sleep(250);
                }

                Native.ForceForeground(frame);
                CloseSettings(root);
                settingsOpen = false;

                ReturnTo(frame, root, section, wasPlaying);
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
                if (settingsOpen && root != null)
                {
                    try
                    {
                        CloseSettings(root); // non lascio JW fermo sulle Impostazioni
                    }
                    catch (Exception)
                    {
                        // JW sta cambiando: pazienza
                    }
                }

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

        private static bool OpenSettings(IntPtr frame, AutomationElement root, out string error)
        {
            error = null;

            // la selezione via UI Automation non cambia pagina se si è in una
            // sotto-pagina di Home: serve un clic vero sulla casetta
            if (!ClickNavigation(frame, root, HomeId))
            {
                error = "Non riesco a cliccare Home";
                return false;
            }

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

        /// <summary>"Riapri" manca: se l'interruttore dello schermo secondario è spento lo accendo.</summary>
        private static bool TurnOnSecondScreen(AutomationElement root)
        {
            var toggle = Find(root, ByName(SecondScreenToggleName), 1000);
            if (toggle == null || !toggle.TryGetCurrentPattern(TogglePattern.Pattern, out var p))
            {
                return false;
            }

            var tp = (TogglePattern)p;
            if (tp.Current.ToggleState == ToggleState.On)
            {
                return false;
            }

            tp.Toggle();
            Log.Write("Riapertura finestra media: acceso lo schermo secondario");
            return true;
        }

        private static void CloseSettings(AutomationElement root)
        {
            var close = Find(root, ById(CloseId), 2000);
            if (close != null)
            {
                Invoke(close);
                Thread.Sleep(500);
            }
        }

        /// <summary>AutomationId della sezione selezionata nel menu laterale (es. navigation_library), o null.</summary>
        private static string SelectedSection(AutomationElement root)
        {
            var walker = TreeWalker.ControlViewWalker;
            foreach (AutomationElement group in root.FindAll(TreeScope.Descendants,
                         new PropertyCondition(AutomationElement.ClassNameProperty, "NamedContainerAutomationPeer")))
            {
                var id = group.Current.AutomationId;
                if (id == null || !id.StartsWith(NavigationPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var item = walker.GetParent(group);
                if (item != null && item.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var p) &&
                    ((SelectionItemPattern)p).Current.IsSelected)
                {
                    return id;
                }
            }

            return null;
        }

        /// <summary>Torna alla sezione di prima e, se il video andava, lo fa ripartire.</summary>
        private static void ReturnTo(IntPtr frame, AutomationElement root, string section, bool wasPlaying)
        {
            if (section == null || section == HomeId || !ClickNavigation(frame, root, section))
            {
                return;
            }

            Log.Write("Riapertura finestra media: tornato alla sezione " + section);
            if (!wasPlaying)
            {
                return;
            }

            var player = Find(root, new AndCondition(ById(PlayButtonId), ByName(PlayName)), 3000);
            if (player != null)
            {
                Invoke(player);
                Log.Write("Riapertura finestra media: video ripartito");
            }
        }

        /// <summary>
        /// Clic vero sull'icona di una voce del menu laterale. Clicco nella colonna delle
        /// icone: il centro della voce intera cadrebbe sulla pagina (es. sul riquadro
        /// della scrittura del giorno). Non clicco se sotto il punto non c'è JW Library.
        /// </summary>
        private static bool ClickNavigation(IntPtr frame, AutomationElement root, string navigationId)
        {
            var group = Find(root, ById(navigationId), 3000);
            var item = group == null ? null : TreeWalker.ControlViewWalker.GetParent(group);
            if (item == null || item.Current.IsOffscreen)
            {
                return false;
            }

            var r = item.Current.BoundingRectangle;
            if (r.IsEmpty || r.Width <= 0 || r.Height <= 0)
            {
                return false;
            }

            var x = (int)(r.Left + Math.Min(r.Width, NavColumnWidth) / 2);
            var y = (int)(r.Top + r.Height / 2);
            var under = Native.WindowFromPoint(new Native.POINT { X = x, Y = y });
            if (under == IntPtr.Zero || Native.GetAncestor(under, Native.GA_ROOT) != frame)
            {
                Log.Write("Riapertura finestra media: JW Library è coperta, non clicco");
                return false;
            }

            Native.ClickAt(x, y);
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
