# Architettura

## Principio
La **logica di gioco è codice C# puro** (`Assets/Scripts/Core`, asmdef con `noEngineReferences: true`). Unity è solo uno strato di presentazione.
Vantaggi: la logica è testabile senza Editor (`dotnet test`), la simulazione usa le formule reali e gli errori economici non dipendono dalla UI.

```
          ┌──────────────────────────── Assets/Scripts/Unity (UnityEngine, uGUI) ───────────────────────────┐
          │ GameBootstrap ──► MainUI (Panels, Events) · AudioService · UnityMockAdService · AdMob (#if)       │
          └──────────────────────────────────────────┬────────────────────────────────────────────────────────┘
                                                     │ legge stato, invia intenti
          ┌──────────────────────────────────────────▼────────────────────────────────────────────────────────┐
          │ Core:  GameSession (composition root, nessun singleton)                                            │
          │  Wallet · ProductionSystem · ProgressionSystem · OrderSystem                                       │
          │  ExperimentalMachineEvent · ProductionSurgeEvent · FreePartsCrate                                  │
          │  OfflineRewardSystem · RewardedAdController · MonetizationService · ConsentGatedAdService           │
          │  SaveManager/SaveMigrator · IAnalyticsService · GameConfig/ConfigValidator · GameSignals           │
          └────────────────────────────────────────────────────────────────────────────────────────────────────┘
```
Le dipendenze vanno solo verso il basso (Unity → Core). Nel Core i sistemi si parlano tramite costruttori espliciti e l'hub di eventi `GameSignals`.

## Responsabilità
| Modulo | Responsabilità |
|---|---|
| `Wallet` | **Unica** fonte del saldo. `TrySpend`/`Add` sincroni e atomici, saldo mai negativo, tetto `MaxBalance`, log delle transazioni (ring buffer da 200). |
| `Formulas` | Costo livello `BaseCost·Growth^n`, costo cumulativo (serie geometrica), massimo acquistabile. Condivise da gioco, UI, test, simulatore. |
| `ProductionSystem` | Un solo `Tick(dt)` per tutte le macchine (niente `Update` per macchina). Livelli, upgrade, revisioni (ricambi), sinergie, bonus temporanei con tetto sul prodotto (`MaxTempMultiplier`). |
| `ProgressionSystem` | Fase corrente (monotona) dai guadagni totali; premi di fase pagati una volta (ledger). |
| `OrderSystem` | Ordini configurabili guidati da `GameSignals`; scadenze senza penali; riscossione una sola volta. |
| `ExperimentalMachineEvent`, `ProductionSurgeEvent`, `FreePartsCrate` | Meccaniche di rischio/ricompensa; RNG iniettabile; stato persistente. |
| `OfflineRewardSystem` | Calcolo assenza con limite, orologio monotono (`LastValidTicks` non torna mai indietro), offerta persistente. |
| `RewardedAdController` | Una sola riproduzione alla volta; ricompensa solo su `OnRewardEarned` del tentativo corrente; risultato consegnato **una volta**; timeout, cooldown, limite giornaliero. |
| `MonetizationService` | Collega gli annunci ai due premi facoltativi (bonus offline, acceleratore). |
| `ClaimLedger` | Insieme persistito di chiavi ("order:a1", "offline:7"…) per rendere idempotenti tutti i premi. |
| `SaveManager` | Busta `{v, sum, payload}` con FNV-1a 64; ordine di lettura main → .tmp → .bak; file corrotti spostati (non cancellati). |
| `JsonMapper`/`MiniJson` | JSON minimale senza dipendenze, identico in Unity e nei test; interi a 64 bit preservati (i tick di `DateTime` superano 2^53). |
| `GameSession` | Costruisce e collega tutto da un `GameConfig`, ciclo di vita (Start/Update/Pause/Resume/End), autosave, wiring analytics. Rifiuta config non valide. |

## Flussi chiave
- **Acquisto:** UI → `ProductionSystem.TryBuy(id, n)` → `Wallet.TrySpend` (atomico) → livello → `GameSignals.MachineLevelChanged` → analytics/ordini/audio. Un secondo tap senza fondi fallisce nel `Wallet`; la UI non ricalcola nulla.
- **Evento sperimentale:** `Advance(dt)` crea l'offerta con prezzi fissati; `TryAccept(scelta, costoMostrato)` valida il costo mostrato contro quello della logica, paga **una volta**, tira l'esito con l'RNG e passa subito a `InProgress` (un secondo tap trova lo stato cambiato); `TryCollect` paga una volta tramite ledger.
- **Offline + annuncio:** `CheckOffline` → offerta persistente (base + bonus dichiarato) → `RequestOfflineBonus` → controller → `OnRewardEarned` → `GrantBonus` (ledger) → `ClaimOffline` paga base (+ bonus se concesso). La base non dipende mai dall'annuncio.
- **Riavvio:** `GameSession.Capture/Apply`; nulla di "in volo" (annunci) sopravvive; eventi, offerte, RNG e bonus concessi sì.

## Determinismo e test
`IRandom` iniettabile (`XorShiftRandom` persistibile, `FixedRandom` per i test), `IClock` (`ManualClock`). L'RNG di gioco è salvato nel file: ricaricare un salvataggio **riproduce gli stessi tiri** invece di consentire un nuovo tentativo. È robustezza contro errori accidentali, non protezione da manomissione.

## Come aggiungere contenuti
- Nuova macchina/upgrade/ordine/fase: solo dati in `DefaultConfig.cs` (o JSON). Eseguire `dotnet test` (il validatore e i test controllano riferimenti e probabilità) e il simulatore.
- Nuovo campo di salvataggio con default: nessuna migrazione. Rinomina/rimozione/reinterpretazione: nuovo step in `SaveMigrator`, incremento di `SaveData.CurrentVersion` e test.
- Nuova lingua: `Loc.Register("en", tabella)`.
- Nuovo sink analytics: implementare `IAnalyticsService`.

## Scelte tecniche e rinunce
- UI costruita **da codice** (uGUI): evita prefab/scene scritti a mano, ma significa che non c'è un layout editabile visualmente e che il tutto è **non verificato in Unity**.
- `UnityEngine.UI.Text` (font `LegacyRuntime.ttf`) invece di TextMeshPro per non dipendere da asset da importare; per un prodotto finale conviene migrare a TMP.
- Niente `Dictionary` nello schema di salvataggio (solo `List`) per mantenere il mapper banale.
- Reflection per i dati: richiede `Assets/link.xml` con IL2CPP (incluso).
- Valuta `double` con tetto: sufficiente per ~1e24; il cambio a una rappresentazione "big number" sarebbe necessario solo con un endgame molto più lungo.
