using System;
using System.Collections.Generic;
using System.Globalization;

namespace JWFixMonitor
{
    /// <summary>
    /// Testi dell'interfaccia in italiano, inglese e spagnolo. La lingua segue quella
    /// dell'interfaccia di Windows; se non è tradotta si usa l'inglese.
    /// </summary>
    internal static class L
    {
        public static string Lang = LanguageFor(CultureInfo.CurrentUICulture);

        public static string LanguageFor(CultureInfo culture)
        {
            var two = culture?.TwoLetterISOLanguageName ?? "";
            return two == "it" || two == "es" ? two : "en";
        }

        public static IEnumerable<string> Keys => Table.Keys;

        /// <summary>Testo nella lingua corrente.</summary>
        public static string T(string key)
        {
            if (!Table.TryGetValue(key, out var t))
            {
                return key;
            }

            return Lang == "it" ? t[0] : Lang == "es" ? t[2] : t[1];
        }

        /// <summary>Testo con segnaposto {0}, {1}…</summary>
        public static string F(string key, params object[] args) => string.Format(T(key), args);

        // chiave → { italiano, inglese, spagnolo }
        private static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            // menu dell'icona
            ["Starting"] = new[] { "Avvio…", "Starting…", "Iniciando…" },
            ["Enabled"] = new[] { "Attivo", "Enabled", "Activado" },
            ["MonitorMenu"] = new[] { "Schermo della finestra media", "Media window screen", "Pantalla de la ventana multimedia" },
            ["TopMost"] = new[] { "Sempre in primo piano", "Always on top", "Siempre visible" },
            ["Notifications"] = new[] { "Mostra notifiche", "Show notifications", "Mostrar notificaciones" },
            ["StartWithWindows"] = new[] { "Avvia con Windows", "Start with Windows", "Iniciar con Windows" },
            ["PlaceNow"] = new[] { "Sistema di nuovo adesso", "Fix position now", "Recolocar ahora" },
            ["ReopenMedia"] = new[] { "Riapri finestra media", "Reopen media window", "Reabrir ventana multimedia" },
            ["WhatsOnTv"] = new[] { "Cosa va in TV… (clic sull'icona)", "What's on TV… (click the icon)", "Qué se ve en la TV… (clic en el icono)" },
            ["BackToJwMenu"] = new[] { "Torna a JW in TV (Ctrl+Alt+J)", "Back to JW on TV (Ctrl+Alt+J)", "Volver a JW en la TV (Ctrl+Alt+J)" },
            ["CheckUpdates"] = new[] { "Controlla aggiornamenti", "Check for updates", "Buscar actualizaciones" },
            ["CheckUpdatesNow"] = new[] { "Cerca aggiornamenti adesso", "Check for updates now", "Buscar actualizaciones ahora" },
            ["DownloadUpdate"] = new[] { "⬇ Scarica la versione {0}", "⬇ Download version {0}", "⬇ Descargar la versión {0}" },
            ["OpenLog"] = new[] { "Apri il registro (log)", "Open log", "Abrir registro (log)" },
            ["Exit"] = new[] { "Esci", "Exit", "Salir" },
            ["AutoSecondary"] = new[] { "Automatico (schermo secondario)", "Automatic (secondary screen)", "Automático (pantalla secundaria)" },
            ["ChosenNotConnected"] = new[] { "Schermo scelto non collegato ({0})", "Chosen screen not connected ({0})", "Pantalla elegida no conectada ({0})" },
            ["Screen"] = new[] { "Schermo", "Screen", "Pantalla" },
            ["Primary"] = new[] { "(principale)", "(primary)", "(principal)" },

            // notifiche
            ["MediaClosedOffer"] = new[]
            {
                "La finestra media di JW Library è stata chiusa. Clicca per riaprirla.",
                "The JW Library media window was closed. Click to reopen it.",
                "Se cerró la ventana multimedia de JW Library. Haz clic para volver a abrirla."
            },
            ["MediaAlreadyOpen"] = new[] { "La finestra media è già aperta.", "The media window is already open.", "La ventana multimedia ya está abierta." },
            ["ReopenFailed"] = new[]
            {
                "Non riesco a riaprirla ({0}): in JW Library vai su Home → Impostazioni → Riapri",
                "Couldn't reopen it ({0}): in JW Library go to Home → Settings → Reopen",
                "No se pudo volver a abrir ({0}): en JW Library ve a Inicio → Configuración → Volver a abrir"
            },
            ["SendFailed"] = new[] { "Non riesco a mandarla in TV: {0}", "Couldn't send it to the TV: {0}", "No se pudo enviar a la TV: {0}" },
            ["AlreadyJw"] = new[] { "In TV c'è già JW Library.", "JW Library is already on the TV.", "JW Library ya está en la TV." },
            ["SwappedClosed"] = new[]
            {
                "La finestra in TV è stata chiusa: torna JW Library.",
                "The window on the TV was closed: back to JW Library.",
                "Se cerró la ventana de la TV: vuelve JW Library."
            },
            ["MediaPlaced"] = new[] { "Finestra media sistemata su {0}", "Media window placed on {0}", "Ventana multimedia colocada en {0}" },
            ["UpdateAvailable"] = new[]
            {
                "È disponibile JWFixMonitor {0} (hai la {1}). Clicca per scaricarla.",
                "JWFixMonitor {0} is available (you have {1}). Click to download it.",
                "JWFixMonitor {0} está disponible (tienes la {1}). Haz clic para descargarla."
            },
            ["UpToDate"] = new[] { "JWFixMonitor è aggiornato (versione {0}).", "JWFixMonitor is up to date (version {0}).", "JWFixMonitor está actualizado (versión {0})." },
            ["UpdateCheckFailed"] = new[]
            {
                "Non riesco a controllare gli aggiornamenti: {0}",
                "Couldn't check for updates: {0}",
                "No se pudieron buscar actualizaciones: {0}"
            },
            ["LogOpenFailed"] = new[] { "Non riesco ad aprire il registro:\n{0}", "Couldn't open the log:\n{0}", "No se pudo abrir el registro:\n{0}" },
            ["AlreadyRunning"] = new[]
            {
                "JWFixMonitor è già in esecuzione.\nLo trovi nell'area di notifica, vicino all'orologio.",
                "JWFixMonitor is already running.\nYou'll find it in the notification area, next to the clock.",
                "JWFixMonitor ya se está ejecutando.\nLo encontrarás en el área de notificación, junto al reloj."
            },

            // stato
            ["Paused"] = new[] { "In pausa", "Paused", "En pausa" },
            ["WaitingMedia"] = new[] { "In attesa della finestra media", "Waiting for the media window", "Esperando la ventana multimedia" },
            ["JwNotRunning"] = new[] { "JW Library non è avviato", "JW Library is not running", "JW Library no se está ejecutando" },
            ["OtherOnTv"] = new[] { "In TV c'è un'altra finestra", "Another window is on the TV", "Hay otra ventana en la TV" },
            ["Fixing"] = new[] { "Sistemo la finestra media…", "Fixing the media window…", "Ajustando la ventana multimedia…" },
            ["NoSecondary"] = new[] { "Nessuno schermo secondario collegato", "No secondary screen connected", "No hay pantalla secundaria conectada" },
            ["MediaOn"] = new[] { "Finestra media su {0}", "Media window on {0}", "Ventana multimedia en {0}" },
            ["TooMany"] = new[] { "Troppi spostamenti, riprovo tra poco", "Too many moves, retrying shortly", "Demasiados movimientos, reintentando en breve" },
            ["NotPrecise"] = new[] { "Posizione non precisa, riprovo", "Position not exact, retrying", "Posición no exacta, reintentando" },

            // pannello
            ["PanelTitle"] = new[] { "JWFixMonitor — cosa va in TV", "JWFixMonitor — what's on TV", "JWFixMonitor — qué se ve en la TV" },
            ["BackButton"] = new[] { "◀  Torna a JW   (Ctrl+Alt+J)", "◀  Back to JW   (Ctrl+Alt+J)", "◀  Volver a JW   (Ctrl+Alt+J)" },
            ["JwTile"] = new[] { "JW Library — video", "JW Library — video", "JW Library — video" },
            ["InTvJw"] = new[]
            {
                "In TV: JW Library. Clicca una finestra per mandarla in TV.",
                "On TV: JW Library. Click a window to send it to the TV.",
                "En la TV: JW Library. Haz clic en una ventana para enviarla a la TV."
            },
            ["InTv"] = new[] { "In TV: {0}", "On TV: {0}", "En la TV: {0}" },
            ["InTvBadge"] = new[] { "● IN TV  ", "● ON TV  ", "● EN TV  " },
            ["RemoteHint"] = new[]
            {
                "Rotellina: scorri  ·  Ctrl+rotellina: zoom  ·  Clic: clicca in TV  ·  Tasti: vanno alla TV",
                "Wheel: scroll  ·  Ctrl+wheel: zoom  ·  Click: clicks on the TV  ·  Keys: go to the TV",
                "Rueda: desplazar  ·  Ctrl+rueda: zoom  ·  Clic: hace clic en la TV  ·  Teclas: van a la TV"
            },
            ["Minimized"] = new[] { "ridotta a icona", "minimized", "minimizada" },
            ["NoPreview"] = new[] { "anteprima non disponibile", "preview not available", "vista previa no disponible" },

            // errori
            ["WindowGone"] = new[] { "la finestra non esiste più", "the window no longer exists", "la ventana ya no existe" },
            ["CannotSend"] = new[] { "questa finestra non si può mandare in TV", "this window can't be sent to the TV", "esta ventana no se puede enviar a la TV" },
            ["NoTv"] = new[] { "nessuno schermo TV collegato", "no TV screen connected", "no hay ninguna pantalla de TV conectada" },
            ["NoPlacement"] = new[] { "non riesco a leggere la posizione della finestra", "can't read the window position", "no se puede leer la posición de la ventana" },
            ["NoReopenButton"] = new[] { "non trovo il pulsante Riapri", "can't find the Reopen button", "no encuentro el botón Volver a abrir" },
            ["NoHomeClick"] = new[] { "non riesco a cliccare Home", "can't click Home", "no puedo hacer clic en Inicio" },
            ["NoSettingsButton"] = new[] { "non trovo il pulsante Impostazioni", "can't find the Settings button", "no encuentro el botón Configuración" },
        };
    }
}
