# Changelog

## 0.4.0 — Stile cartoon, premi e inviti
### Aggiunto
- **Stile cartoon**: colori vivaci, contorni spessi, caratteri arrotondati, pulsanti 3D "gommosi", moneta d'oro, cieli diversi per fase, contorni sui disegni dei macchinari.
- **Bullo**, la mascotte robot: saluta, commenta acquisti e Frenesia, dà consigli (toccalo per il prossimo).
- **Scheda Premi**: regalo giornaliero, video premiati, inviti.
- **Guarda un video per…** (solo con rete pubblicitaria o nella demo): cassa gratis (3 al giorno), ricompensa ordine x2, oltre ad acceleratore, regalo x2 e bonus offline.
- **Invita un amico**: codice personale di 6 caratteri e link `?ref=CODICE`; chi entra dal link riceve un regalo di benvenuto (una volta per dispositivo); chi condivide riceve una cassa (una volta al giorno). Inserimento manuale del codice di un amico.
- **Demo pubblica** in `/demo/` con video simulati e strumenti di sviluppo, salvataggio separato, etichetta DEMO.
- 20 test unitari (252) e 6 test nel browser (22), incluso un test di compatibilità con i salvataggi della versione precedente.
### Modificato
- Annunci: massimo 12 al giorno, almeno 60 s tra uno e l'altro.
### Non fatto (deliberatamente)
- Nessuna slot machine o ruota: meccaniche di azzardo simulato, a rischio policy e legali, specie con uno stile che attrae bambini.
- Nessun premio "quando l'amico inizia a giocare": serve un server per verificarlo.

## 0.3.0 — Grafica e coinvolgimento
### Aggiunto
- Fabbrica 2D animata con illustrazioni di tutti gli 8 macchinari (animazioni più veloci al crescere del livello), nastro trasportatore con i prodotti, sfondo che cambia per fase; illustrazioni anche nelle liste e nella festa di nuova fase.
- Effetti: scintille e monete al tocco, banco con martello, monete che volano al saldo, saldo che scorre, coriandoli, banner ai traguardi di livello, apertura animata della cassa, bagliore durante la Frenesia. Impostazione "Effetti visivi: ridotti" e rispetto di "riduci movimento".
- Meccaniche (logica condivisa, testata): **Frenesia** (tocchi rapidi → x2 per 12 s, anche sui tocchi) e **Regalo giornaliero** (7 giorni, nessun azzeramento, raddoppio facoltativo con video `daily_bonus`).
- Obiettivo sempre visibile con riscossione in un tocco; popup di nuova fase.
- 15 test unitari (232 totali) e 6 test nel browser (16 totali), incluso un controllo di fluidità (nessun blocco lungo del thread principale).
### Modificato
- Il valore del tocco beneficia dei bonus temporanei.

## 0.2.0 — Web app per iPhone (non ancora pubblicata)
### Aggiunto
- `Web/OfficinaTycoon.Web`: web app installabile (PWA, Blazor WebAssembly) che usa la stessa logica di `Assets/Scripts/Core`; interfaccia mobile completa (macchine, upgrade e revisioni, ordini, eventi, cassa, impostazioni), popup offline, istruzioni "Aggiungi alla schermata Home", funzionamento offline (service worker), salvataggio in `localStorage`, **backup/ripristino** con codice validato, Content-Security-Policy, nessuna pubblicità né raccolta dati.
- `KeyValueSaveStorage`, `SaveManager.Encode/TryDecode` (import sicuro), limiti di dimensione nel parser JSON, conversioni numeriche saturanti.
- 40 nuovi test unitari (sicurezza/storage, testi e formattazione) e 10 test end-to-end in browser (Playwright, viewport iPhone).
- CI GitHub Actions: test + build + e2e a ogni push, pubblicazione su GitHub Pages da `main`.
- Documenti `Docs/INSTALL_IPHONE.md`, `Docs/SECURITY.md`.
### Corretto
- Formattazione numeri: `999.999.999` mostrava "1000M" (ora "999M", senza arrotondare per eccesso il saldo).
- Testi con apostrofi al posto delle lettere accentate.

## 0.1.0-prototype (non rilasciato)
Prima versione sviluppata da un repository vuoto.

### Aggiunto
- Logica di gioco pura C# (`Assets/Scripts/Core`): `Wallet`, formule di costo, `ProductionSystem` (upgrade, revisioni, sinergie, bonus temporanei), progressione a 3 fasi, `OrderSystem` (7 tipi, tutorial, scadenze senza penali), `ExperimentalMachineEvent`, `ProductionSurgeEvent`, `FreePartsCrate`, `OfflineRewardSystem`, `RewardedAdController` / `MonetizationService` / `ConsentGatedAdService`, analytics astratto con sink di debug, `SaveManager` (versionato, checksum, backup, migrazioni), `GameSession`.
- Configurazione centralizzata con validazione (`ConfigValidator`) e override JSON.
- 177 test NUnit eseguibili con `dotnet test Tools/Tests.Dotnet` e in Unity (EditMode).
- Simulatore di bilanciamento (`Tools/Simulator`).
- Livello Unity (non compilato in Unity): bootstrap, UI uGUI da codice, audio procedurale, servizi annunci mock/AdMob (non verificato), strumenti Editor, scena `Main`.
- Documentazione completa in `Docs/`.

### Correzioni emerse durante lo sviluppo (dai test/simulatore)
- Bilanciamento iniziale errato (tutti i contenuti in ~10 minuti): ridisegnati i rapporti costo/produzione e le ricompense degli ordini. Vedi `Docs/ECONOMY_BALANCE.md`.
- `ConfigValidator` andava in eccezione con id macchina duplicati: ora li segnala.
- Un'offerta offline già "pagata" con l'annuncio veniva depositata in silenzio a ogni riavvio: ora resta riscuotibile dopo assenze brevi.
- `GameSession` rifiuta di avviarsi con una configurazione non valida (messaggio leggibile).
