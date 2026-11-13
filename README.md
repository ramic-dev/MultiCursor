# MultiCursor

Piccolo strumento per Windows che aggiunge cursori del mouse supplementari: seguono il cursore vero a distanza fissa e replicano click e rotella in tutti i punti, quasi nello stesso istante.

Un solo file sorgente C#, nessuna dipendenza, nessuna installazione. Si compila con il compilatore già incluso in Windows.

## Come funziona

Windows ha un solo cursore di sistema. I cursori aggiuntivi sono quindi finestrelle sempre in primo piano e trasparenti ai click che disegnano una freccia. Quando premi un tasto del mouse, MultiCursor trattiene l'evento reale e lo riemette in rapida sequenza su ogni punto (il tuo incluso), poi riporta il cursore dov'era.

## Uso

1. Avvia `MultiCursor.exe`: compare un'icona nell'area di notifica.
2. Porta il mouse su ogni punto in cui vuoi un cursore e premi `Ctrl+Shift+F1`.
3. Porta il mouse nel suo punto di partenza e premi `Ctrl+Shift+F2`: i cursori si colorano, seguono il mouse e replicano i tuoi click.
4. Premi di nuovo `Ctrl+Shift+F2` per mettere in pausa.

| Scorciatoia predefinita | Comando | Effetto |
|---|---|---|
| `Ctrl+Shift+F1` | Add | Aggiunge un cursore dove si trova il mouse (mette in pausa) |
| `Ctrl+Shift+F7` | Remove | Elimina il cursore più vicino al mouse |
| `Ctrl+Shift+F3` | Clear | Rimuove tutti i cursori |
| `Ctrl+Shift+F2` | Toggle | Attiva / mette in pausa |
| `Ctrl+Shift+F5` | Save | Salva la combinazione corrente |
| `Ctrl+Shift+F6` | Load | Ricrea la combinazione salvata attorno al mouse e la attiva |
| `Ctrl+Shift+F4` | Quit | Chiude il programma |

Lo stesso elenco, con le scorciatoie effettivamente configurate, si apre dalla voce **Aiuto** del menu dell'icona nell'area di notifica (o con un doppio click sull'icona).

## Impostazioni

Al primo avvio viene creato `MultiCursor.ini` accanto all'eseguibile. Dopo una modifica va riavviato il programma.

| Chiave | Significato |
|---|---|
| `Add`, `Remove`, `Clear`, `Toggle`, `Save`, `Load`, `Quit` | Scorciatoie: `Ctrl`, `Alt`, `Shift`, `Win` più un tasto (`A`, `F1`, `D1`, `NumPad1`, `Space`, ...) |
| `Replicate` | Cosa replicare: `Left`, `Right`, `Middle`, `X1`, `X2`, `Wheel` |
| `HoldMs` | Durata in millisecondi della pressione su ogni punto (0 = istantanea) |
| `GapMs` | Pausa in millisecondi tra un punto e l'altro (0 = nessuna) |
| `Color` | Colore dei cursori quando attivi (nome colore .NET: `OrangeRed`, `Lime`, `Cyan`, ...) |

La combinazione salvata sta in `MultiCursor.layout`, sempre accanto all'eseguibile. C'è un solo slot.

## Compilazione

Serve solo Windows con .NET Framework 4.x, già presente in Windows 10 e 11.

```bat
build.cmd
```

Il comando produce `MultiCursor.exe` nella stessa cartella. L'icona `MultiCursor.ico` si può rigenerare con `tools\make-icon.ps1`.

## Limiti

- **Trascinamento:** da attivo, ogni pressione diventa un click istantaneo, quindi non si può trascinare. Metti in pausa per farlo.
- **Giochi:** alcuni giochi non rilevano click a durata zero: prova `HoldMs=20`. I giochi a schermo intero esclusivo non mostrano i cursori aggiuntivi.
- **Rotella:** tra un punto e l'altro c'è una pausa minima di 1 ms, altrimenti Windows fonde gli scatti in uno solo. Se alcuni scatti si fondono comunque, aumenta `GapMs`.
- **Finestre amministratore:** per agire su programmi avviati come amministratore, anche MultiCursor va avviato come amministratore.
- **Monitor con scala diversa:** il programma è stato provato su un solo monitor.

## Avvertenze

- **Antivirus:** l'eseguibile non è firmato, intercetta il mouse e genera click sintetici. Alcuni antivirus o SmartScreen possono segnalarlo. Il sorgente è un solo file: leggilo e compilalo da te.
- **Anti-cheat e condizioni d'uso:** nei giochi online l'input sintetico può essere bloccato o violare le regole del gioco. L'uso è sotto la tua responsabilità.

## Licenza

[MIT](LICENSE)
