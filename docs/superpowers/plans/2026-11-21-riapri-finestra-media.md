# Riapri finestra media — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Quando la finestra media di JW Library si chiude, JWFixMonitor propone con una notifica di riaprirla e lo fa via UI Automation; stessa azione dal menu.

**Architecture:** Nuova classe `JwlReopener` (UI Automation + Win32) che esegue il percorso Home → Impostazioni → Riapri → Chiudi → Indietro. `MediaWindowService` segnala la chiusura con un evento; `TrayApp` mostra il balloon, reagisce al clic e al menu, ed esegue `JwlReopener` su un thread a parte.

**Tech Stack:** C# / .NET Framework 4.8, WinForms `NotifyIcon`, `System.Windows.Automation` (già referenziato).

**Spec:** `docs/superpowers/specs/2026-11-21-riapri-finestra-media-design.md`

## Global Constraints

- Nomi pulsanti JW Library 15.10.9 in italiano, raccolti in costanti: `Home`, `Impostazioni` (classe `AppBarButton`), `Riapri`, `Riproduci video sullo schermo secondario` (ToggleSwitch), AutomationId `CloseButton`, `Back`, `navigation_home`.
- Nessuna dipendenza nuova.
- Testi utente in italiano semplice, come nel resto dell'app.
- La UI Automation non gira sul thread UI della tray.

## Review Focus

- JW Library già sulla Home con Impostazioni aperte → non deve fallire (Riapri trovato direttamente).
- JW Library minimizzata → va ripristinata prima di leggere l'albero, poi riminimizzata.
- "Riapri" assente ma interruttore spento → accendere l'interruttore.
- Clic ripetuti sulla notifica / menu mentre la riapertura è in corso → una sola esecuzione.
- JW Library non avviata → messaggio, nessuna eccezione.

Questi casi toccano UI di terze parti: si verificano a mano (Task 1 Step 3), non con test automatici (il progetto test è net8 e non include UIA).

---

### Task 1: `JwlReopener`

**Files:**
- Create: `src/JwlReopener.cs`
- Modify: `src/Native.cs` (aggiungere `SetCursorPos`, `mouse_event`, `IsIconic` se manca, `SW_MINIMIZE = 6`)

**Interfaces:**
- Produces: `internal static class JwlReopener { public static bool TryReopen(out string error); }` — sincrono, 5–15 s, da chiamare fuori dal thread UI. `error` = testo per log/notifica quando ritorna false.

- [ ] **Step 1: Implementa `TryReopen`** seguendo la spec, passi 1–7:
  - trova il frame principale: `ApplicationFrameWindow` visibile, titolo `JW Library`, non topmost (EnumWindows); se assente → `error = "JW Library non è avviato"`;
  - ricorda `GetForegroundWindow()` e `IsIconic(frame)`; `ShowWindow(SW_RESTORE)` + `Native.ForceForeground(frame)`; attesa ~1,5 s;
  - helper `Find(root, cond, timeoutMs)` che riprova ogni 250 ms fino a timeout (default 4000);
  - prima cerca `Riapri`: se c'è salta direttamente al click (caso Impostazioni già aperte);
  - altrimenti Home: elemento AutomationId `navigation_home` → clic fisico al centro del suo `BoundingRectangle` (SetCursorPos + mouse_event down/up, poi riporta il cursore dov'era); poi `AppBarButton` `Impostazioni` → Invoke;
  - `Riapri` → Invoke; se assente e ToggleSwitch spento → Toggle; se nessuno dei due → `error = "Non trovo il pulsante Riapri"`;
  - `CloseButton` → Invoke, poi `Back` → Invoke (ignora se manca);
  - se era minimizzata `ShowWindow(SW_MINIMIZE)`; `ForceForeground(fgPrima)`;
  - ogni eccezione → `error = ex.Message`, false. `Log.Write` a ogni passo saliente.

- [ ] **Step 2: Build** — `dotnet build src/JWFixMonitor.csproj -c Release` → 0 errori, 0 avvisi.

- [ ] **Step 3: Verifica manuale** (finestra media chiusa in JW Library) con un piccolo comando temporaneo nel menu o chiamata da Task 2: la finestra media ricompare sulla TV, JW torna alla pagina e allo stato (minimizzata o no) di prima, fuoco restituito. Ripeti con Impostazioni già aperte e con JW non minimizzata.

- [ ] **Step 4: Commit** — `git add src/JwlReopener.cs src/Native.cs && git commit -m "feat: riapertura finestra media via UI Automation"`

### Task 2: Notifica e menu

**Files:**
- Modify: `src/MediaWindowService.cs:136-141` (blocco "La finestra media è stata chiusa")
- Modify: `src/TrayApp.cs` (menu, balloon, esecuzione)

**Interfaces:**
- Consumes: `JwlReopener.TryReopen(out string error)`
- Produces: `public event Action MediaWindowClosed;` su `MediaWindowService`, alzato nel blocco di chiusura (solo se `s.Enabled`).

- [ ] **Step 1: Evento** — in `MediaWindowService` aggiungi `MediaWindowClosed` e invocalo dopo `ForgetTracked()`.

- [ ] **Step 2: Tray** in `TrayApp`:
  - campo `bool _offerReopen` e `int _reopening` (Interlocked, evita esecuzioni doppie);
  - su `MediaWindowClosed` (via `_ui.Post`): `_offerReopen = true`; `ShowBalloonTip(10000, "JWFixMonitor", "La finestra media di JW Library è stata chiusa. Clicca per riaprirla.", ToolTipIcon.Info)` — sempre, anche con "Mostra notifiche" spento (è una domanda, non un avviso: scelta del builder documentata qui);
  - `_icon.BalloonTipClicked`: se `_offerReopen` → `Reopen()`; `BalloonTipClosed` → `_offerReopen = false`; ogni altro balloon mostrato azzera `_offerReopen`;
  - voce menu `"Riapri finestra media"` dopo "Sistema di nuovo adesso" → `Reopen()`;
  - `Reopen()`: se `_reopening` già 1 → return; su `ThreadPool`/thread STA (UIA client non richiede STA, ma il thread deve essere fuori dal UI) chiama `TryReopen`; poi `_service.PlaceNow()`; se false → balloon `"Non riesco a riaprirla: in JW Library vai su Home → Impostazioni → Riapri"` + `Log.Write`.

- [ ] **Step 3: Build + test** — `dotnet build src/JWFixMonitor.csproj -c Release` (0/0); `DOTNET_ROLL_FORWARD=Major dotnet test tests/JWFixMonitor.Tests.csproj` → tutti superati.

- [ ] **Step 4: Verifica manuale end-to-end** — con JWFixMonitor avviato, chiudi la finestra media in JW Library → compare il balloon → clic → la finestra torna sulla TV a tutto schermo senza bordo; log con i passi. Prova anche la voce di menu e il doppio clic rapido (una sola esecuzione).

- [ ] **Step 5: Commit** — `git add src/MediaWindowService.cs src/TrayApp.cs && git commit -m "feat: notifica per riaprire la finestra media chiusa"`
