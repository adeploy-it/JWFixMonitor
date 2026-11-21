# Ricordare la TV per modello — design breve

## Perché
In sala ci sono 3 schermi in modalità "Estendi": PC (principale), video (TV,
risoluzione più bassa) e timer. La scelta "Schermo della finestra media" è
salvata col nome Windows (`\\.\DISPLAYn`), che può cambiare dopo un cavo
staccato o un riavvio: allora si torna all'automatico, che può mandare la
finestra media sul timer.

## Cosa
1. Scegliendo uno schermo dal menu si salvano anche: percorso del monitor
   (DisplayConfig `monitorDevicePath`, legato a connettore + monitor), modello
   (codice EDID, es. `SAM0F9E`) e risoluzione.
2. Ritrovo lo schermo scelto, in ordine:
   1. stesso percorso monitor;
   2. stesso modello, se un solo schermo collegato ha quel modello
      (TV e timer potrebbero essere uguali);
   3. stesso nome Windows (`DISPLAYn`);
   4. stessa risoluzione, se un solo schermo non principale ce l'ha;
   5. altrimenti automatico, come oggi.
3. Schermo scelto trovato → la finestra media va lì sempre, anche se JW la
   apre su un altro secondario (già così).
4. Menu: "Schermo 3 — SAMSUNG 1280×720" (nome dal monitor, se disponibile).
5. Impostazioni vecchie (solo `TargetDevice`) continuano a funzionare.

## Approcci considerati
- **Identità salvata + ricerca a cascata (scelto).**
- "TV = risoluzione più bassa": il timer può essere più basso. Scartato.
- Numero di schermo: si rompe come `DISPLAYn`. Scartato.

## Lasciato al builder
API Win32 (QueryDisplayConfig + DisplayConfigGetDeviceInfo), nomi dei campi
in settings.ini.

Approvata dall'utente il 21/11/2026 ("ok approvo").
