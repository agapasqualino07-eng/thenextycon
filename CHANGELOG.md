# Changelog

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
