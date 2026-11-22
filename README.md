# JWFixMonitor

Keeps the **JW Library** media window fixed and full screen on the TV (secondary
screen), even while you use other programs on the PC. It can also put another
window (a PDF, the second Zoom window…) on the TV in place of JW Library, and
bring JW Library back with one click.

Interface in **English, Italian and Spanish**, picked automatically from the
Windows display language (English for any other language).

[Italiano](#italiano) · [Español](#español)

## Features

- **Media window always on the TV**: finds the JW Library media window, takes it
  out of UWP full-screen mode and places it on the chosen screen, borderless,
  covering it exactly. Works with different scaling on PC and TV.
- **Remembers the TV** by monitor path, model and resolution, so the choice
  survives Windows renaming screens (useful with 3 screens: PC, TV, timer).
- **Reopen the media window**: if it gets closed, a notification offers to
  reopen it. JWFixMonitor drives JW Library (Home → Settings → Reopen), then goes
  back to the section you were in and resumes the video if it was playing.
- **Another window on the TV**: click the tray icon (or press **Ctrl+Alt+P**) to
  open a panel with live previews of your windows. Click one to send it to the
  TV; JW Library stays behind. The panel stays open:
  - a large live preview of what's on the TV works as a remote control
    (wheel: scroll, Ctrl+wheel: zoom, click: clicks on the TV, keys go to the TV);
  - **Back to JW** (or **Ctrl+Alt+J**) puts the window back where it was.
  - **Ctrl+Alt+T** sends the window you're using to the TV.
  - If the window on the TV is closed, JW Library comes back by itself.
- **TV off or unplugged**: releases the media window; puts it back when the TV returns.
- **Update notice**: at start and every 12 hours it asks GitHub for the latest
  release; if there is a newer one, a notification (and a menu item) opens its
  download page. Nothing is installed automatically. Can be turned off
  (*Check for updates*).
- Lightweight: one small exe, .NET Framework 4.8 (already in Windows 10/11).

## Usage

1. Close JwlMediaWin if it is running (the two programs would fight).
2. Start `JWFixMonitor.exe`. An icon appears next to the clock.
3. Open JW Library and show an image or a video.

Tray menu (right click): Enabled, Media window screen, Always on top, Show
notifications, **Start with Windows**, Fix position now, Reopen media window,
What's on TV…, Back to JW on TV, Check for updates, Check for updates now,
Open log, Exit.

**Start with Windows** adds JWFixMonitor to the current user's startup programs
(registry key `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`); no admin
rights needed. The path is refreshed at every start, so moving the exe is fine.

Settings and log: `%APPDATA%\JWFixMonitor\`.

### Notes on JW Library languages

Reopening the media window clicks JW Library's own buttons by name. Names are
verified for the Italian interface of JW Library 15.10; English and Spanish names
are included but not yet verified. If reopening fails, the notification tells you
the manual path.

## Build

```
dotnet build src/JWFixMonitor.csproj -c Release
dotnet test tests/JWFixMonitor.Tests.csproj
```

Or open `JWFixMonitor.sln` in Visual Studio 2022.

---

## Italiano

Tiene la finestra media di **JW Library** fissa sulla TV, a tutto schermo, anche
quando sul PC usi altri programmi. Può anche mandare in TV un'altra finestra (un
PDF, la seconda finestra di Zoom…) al posto di JW e tornare a JW con un clic.

- Clic sull'icona o **Ctrl+Alt+P**: pannello con le anteprime dal vivo delle
  finestre; un clic manda la finestra in TV. Il pannello resta aperto, con
  l'anteprima grande di ciò che è in TV usabile come telecomando (rotellina,
  Ctrl+rotellina per lo zoom, clic, tasti).
- **Ctrl+Alt+T** manda in TV la finestra in uso, **Ctrl+Alt+J** torna a JW.
- Se la finestra media si chiude, una notifica propone di riaprirla.
- La TV viene ricordata per modello e risoluzione (comodo con 3 schermi).
- **Avvia con Windows**: voce del menu, solo per l'utente corrente, senza
  permessi di amministratore.
- **Aggiornamenti**: all'avvio e ogni 12 ore controlla su GitHub se c'è una
  versione nuova; se sì, una notifica (e una voce del menu) apre la pagina per
  scaricarla. Si disattiva con *Controlla aggiornamenti*.

## Español

Mantiene la ventana multimedia de **JW Library** fija y a pantalla completa en
la TV, aunque uses otros programas en el PC. También puede enviar otra ventana
(un PDF, la segunda ventana de Zoom…) a la TV en lugar de JW y volver a JW con un
clic.

- Clic en el icono o **Ctrl+Alt+P**: panel con vistas previas en vivo de las
  ventanas; un clic envía la ventana a la TV. El panel queda abierto, con la
  vista previa grande de lo que está en la TV usable como control remoto (rueda,
  Ctrl+rueda para el zoom, clic, teclas).
- **Ctrl+Alt+T** envía a la TV la ventana en uso, **Ctrl+Alt+J** vuelve a JW.
- Si se cierra la ventana multimedia, una notificación ofrece volver a abrirla.
- La TV se recuerda por modelo y resolución (útil con 3 pantallas).
- **Iniciar con Windows**: opción del menú, solo para el usuario actual, sin
  permisos de administrador.
- **Actualizaciones**: al iniciar y cada 12 horas comprueba en GitHub si hay una
  versión nueva; si la hay, una notificación (y una opción del menú) abre la
  página para descargarla. Se desactiva con *Buscar actualizaciones*.

## License

MIT — see [LICENSE](LICENSE).
