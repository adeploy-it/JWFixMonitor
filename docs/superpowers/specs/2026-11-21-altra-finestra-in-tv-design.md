# Un'altra finestra in TV al posto di JW — design breve

## Perché
Durante l'adunanza a volte in TV serve un PDF o la seconda finestra di Zoom al
posto del video di JW Library. La finestra media di JW è in primo piano fisso e
JWFixMonitor la rimette a posto: un'altra finestra trascinata in TV finisce
sotto. Serve un modo veloce per sostituirla e poi tornare a JW.

## Cosa (approvato dall'utente: pannello + scorciatoie, strada A "spostare")
- **Pannello**: clic sinistro sull'icona (o menu "Cosa va in TV…"). Elenco
  delle finestre aperte (regola Alt+Tab, senza JWFixMonitor, JW media e shell),
  "Manda in TV", "Torna a JW", "Aggiorna". Doppio clic / Invio = manda in TV.
- **Scorciatoie**: Ctrl+Alt+T manda in TV la finestra in primo piano;
  Ctrl+Alt+J torna a JW. Se già usate da altri programmi: riga nel log.
- **Manda in TV**: salva posizione e stato (WINDOWPLACEMENT), la sposta sullo
  schermo TV scelto, la ingrandisce e la mette in primo piano fisso. La
  finestra media di JW resta agganciata ma esce dal primo piano e non viene
  rimessa davanti (stato "coperta").
- **Torna a JW**: rimette la finestra dov'era (stessa posizione, stato, primo
  piano originale) e JW torna davanti in TV.
- Finestra in TV chiusa o nascosta (es. fine di Zoom): dopo 1 s torna JW da solo.
- Una sola finestra alla volta: mandarne un'altra rimette a posto la prima.
- Uscendo da JWFixMonitor si torna a JW.

## Approcci considerati
- **A. Spostare la finestra (scelto)**: qualità piena, giusto per Zoom.
- B. Copia dal vivo (DWM thumbnail): niente spostamenti ma qualità legata
  alla dimensione sul PC; eventuale aggiunta futura per i PDF.

## Lasciato al builder
Aspetto del pannello, posizione (in basso a destra sullo schermo principale).
