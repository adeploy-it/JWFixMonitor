# Riapri finestra media — design breve

## Perché
Se la finestra media di JW Library si chiude (per errore o dopo un video), per
farla ricomparire sulla TV bisogna andare in JW Library → Home → Impostazioni →
"Riapri". Durante l'adunanza è scomodo. JWFixMonitor deve proporlo da solo.

## Cosa
1. Quando il servizio vede che la finestra media agganciata non esiste più
   (`La finestra media è stata chiusa`), mostra una notifica:
   "La finestra media di JW Library è stata chiusa. Clicca per riaprirla."
2. Clic sulla notifica → riapertura automatica.
3. Stessa azione dal menu dell'icona: **Riapri finestra media**.
4. Se fallisce: riga nel log + notifica "Non riesco a riaprirla: in JW Library
   vai su Home → Impostazioni → Riapri".

## Come si riapre (UI Automation, provato a mano il 21/11/2026, JW Library 15.10.9)
1. Ricorda la finestra in primo piano.
2. JW Library (ApplicationFrameWindow del processo JW): se minimizzata la
   ripristina, la porta in primo piano (UWP sospesa non espone l'albero UIA).
3. Va su Home: icona casetta nel menu laterale (ListItem `Home`, gruppo
   `navigation_home`). La selezione UIA non basta se si è in una sotto-pagina di
   Home: serve un clic vero al centro dell'elemento.
4. Pulsante `Impostazioni` (classe `AppBarButton`) → Invoke.
5. Pulsante `Riapri` (compare sotto "La finestra dello schermo secondario è
   stata chiusa") → Invoke. Se manca ma l'interruttore "Riproduci video sullo
   schermo secondario" è spento, lo accende.
6. `CloseButton` → Invoke. JW resta su Home: andare su Home mette in pausa un video in corso; il ritorno alla pagina di prima è stato tolto su richiesta (Indietro non affidabile).
7. Se JW era minimizzata la riminimizza; ridà il fuoco alla finestra di prima.
8. Il servizio aggancia la nuova finestra al giro successivo (già funziona).

## Approcci considerati
- **UI Automation sui pulsanti di JW (scelto)**: usa la stessa strada
  dell'utente, nessuna dipendenza nuova; si rompe se JW cambia interfaccia o
  lingua (nomi italiani) → fallback con messaggio.
- Scorciatoie da tastiera / protocollo `jwlibrary:`: non esiste un comando
  noto per riaprire lo schermo secondario.
- Riaprire da soli senza chiedere: scartato, la chiusura può essere voluta.

## Non obiettivi
- Mandare in TV PDF/Zoom (discussione separata).
- Supporto lingue diverse dall'italiano (nomi pulsanti cablati, raccolti in
  costanti per poterli estendere).

## Lasciato al builder
Tempi di attesa tra i passi, numero di tentativi, thread su cui gira (non il
thread UI della tray).
