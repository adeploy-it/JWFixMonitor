# JWFixMonitor

Tiene la finestra media di **JW Library** fissa sulla TV (schermo secondario),
a tutto schermo, anche quando sul PC usi altri programmi.

Riscrittura da zero ispirata a JwlMediaWin.

## Uso

1. Chiudi JwlMediaWin, se è aperto (i due programmi si darebbero fastidio).
2. Avvia `JWFixMonitor.exe`. Compare un'icona vicino all'orologio.
3. Apri JW Library e mostra un'immagine o un video: la finestra media viene
   sistemata da sola sulla TV.

Menu dell'icona (tasto destro):

- **Attivo** — spegnilo per mettere in pausa. La finestra torna cliccabile.
- **Schermo della finestra media** — automatico oppure uno schermo preciso.
- **Sempre in primo piano** — la finestra media resta sopra le altre.
- **Avvia con Windows**
- **Sistema di nuovo adesso** — riposiziona subito.
- **Apri il registro (log)** — utile se qualcosa non va.

## Cosa fa meglio

- **Scala diversa tra PC e TV** (es. 100% e 200%): niente più finestra a metà.
  Lavora in pixel reali e corregge la finestra se JW Library si ridimensiona da solo.
- **Bordi precisi**: legge i bordi invisibili della finestra da Windows invece di
  usare valori fissi.
- **Niente controlli a raffica**: reagisce agli eventi di Windows (finestra che
  appare, si sposta, schermi che cambiano). Un controllo leggero ogni 3 secondi
  fa da rete di sicurezza.
- **TV spenta o scollegata**: sblocca la finestra e la toglie dal primo piano,
  così non resti bloccato. Quando la TV torna, la rimette al suo posto.
- **Ritorno dalla sospensione**: riposiziona da solo.
- **Ti ridà il fuoco**: dopo la conversione torna alla finestra che stavi usando.
- **Leggero**: un solo file da 60 KB, .NET Framework 4.8 (già presente in Windows 10/11).

## Compilare

```
dotnet build src/JWFixMonitor.csproj -c Release
dotnet test tests/JWFixMonitor.Tests.csproj
```

Oppure apri `JWFixMonitor.sln` in Visual Studio 2022.

Impostazioni e registro: `%APPDATA%\JWFixMonitor\`.

## Come funziona

1. Cerca una finestra `ApplicationFrameWindow` in primo piano che contiene la
   `CoreWindow` del processo `JWLibrary` (o `JWLibrary.Forms.UWP`).
2. Se è ancora in modalità schermo intero UWP, le dà il fuoco e invia
   **Win+Shift+Invio**. Aspetta 10 secondi prima di riprovare, per non farla
   tornare a schermo intero.
3. La porta sullo schermo scelto con `SetWindowPos`, calcolando i bordi
   invisibili con DWM. Ripete se serve, poi la disabilita: niente clic,
   niente pulsanti, niente ridimensionamenti per sbaglio.
