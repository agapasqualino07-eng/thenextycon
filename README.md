# Officina Tycoon

Gioco mobile idle/tycoon 2D (Unity + C#) ambientato nella produzione industriale: si parte da una piccola officina, si
comprano e potenziano macchinari, si completano ordini e si reinveste. Offline-first, nessun backend, nessun acquisto in-app.

> **Stato onesto (leggere prima di tutto).** La **logica di gioco** (economia, produzione, ordini, eventi di rischio/ricompensa,
> cassa gratuita, guadagni offline, salvataggi, flusso rewarded ad, analytics) è completa, **testata con 177 test
> automatici che passano** e simulata. Il **livello Unity** (UI, audio, bootstrap, scena, strumenti Editor, adattatore AdMob)
> è scritto ma **non è mai stato aperto né compilato in Unity**: durante lo sviluppo non era disponibile l'Unity Editor.
> Per questo il progetto **NON è dichiarato pronto per un test interno**. Cosa manca è elencato in
> [Stato dei criteri di accettazione](#stato-dei-criteri-di-accettazione) e in [Docs/TEST_REPORT.md](Docs/TEST_REPORT.md).

## Requisiti
- **Unity 6 LTS (6000.0.x)**. `ProjectSettings/ProjectVersion.txt` indica `6000.0.23f1`: se hai un'altra 6000.0.x installata, Unity Hub proporrà di aprirla con quella (è normale). Non è stata testata nessuna versione.
- Pacchetti (vedi `Packages/manifest.json`): `com.unity.ugui` 2.0.0, `com.unity.test-framework` 1.4.5 e moduli built-in (audio, ui, imgui, jsonserialize, androidjni). Nessun SDK pubblicitario o di analytics è incluso.
- Per Android: modulo Android Build Support (SDK/NDK/JDK) installato via Unity Hub.
- Per i test fuori da Unity: .NET SDK 8 (`dotnet`).

## Come aprire il progetto
1. Unity Hub → *Add project from disk* → cartella del repository.
2. Alla prima apertura: menu **Officina → Apply Project Settings** (imposta portrait, IL2CPP, ARM64, id applicazione *segnaposto*).
3. Apri `Assets/Scenes/Main.unity` e premi Play. Se la scena non si carica, **Officina → Create Main Scene** la ricrea; in ogni caso `GameBootstrap` si crea da solo in qualunque scena (`RuntimeInitializeOnLoadMethod`).

## Come eseguire i test e gli strumenti
| Cosa | Comando / azione | Note |
|---|---|---|
| Test logica (177) | `dotnet test Tools/Tests.Dotnet` | Compila gli **stessi** sorgenti di `Assets/Scripts/Core` e gli **stessi** test di `Assets/Tests/EditMode`. Risultato verificato. |
| Test in Unity | Window → General → Test Runner → EditMode → Run All | **Non eseguito** (nessun Editor disponibile). |
| Simulatore economia | `dotnet run --project Tools/Simulator -c Release -- 30` | Usa `GameSession` reale. Output in [Docs/ECONOMY_BALANCE.md](Docs/ECONOMY_BALANCE.md). |
| Controllo compilazione parziale Unity | `dotnet build Tools/UnityCompileCheck` | Vedi limiti sotto. |

**Limiti del controllo parziale:** usa le API core di `UnityEngine` dal pacchetto NuGet comunitario `UnityEngine.Modules` 2021.3.33 (non Unity 6) e **stub scritti a mano** per `UnityEngine.UI`. Un risultato verde è solo un controllo di sintassi/segnature, **non** sostituisce l'apertura in Unity. `Assets/Editor/*` e l'adattatore AdMob (`#if OFFICINA_ADMOB`) non rientrano neanche in questo controllo.

## Come modificare costi, ricompense e probabilità
Tutti i numeri sono in `Assets/Scripts/Core/Config/DefaultConfig.cs` (struttura in `GameConfig.cs`). Niente è scritto negli script di gioco.
- Rapido: modifica `DefaultConfig.cs` e rilancia test/simulatore.
- Senza ricompilare: **Officina → Export Default Config JSON** crea `Assets/Resources/game_config.json`; modificalo e verrà usato al posto dei default. Il file è **validato all'avvio** (somma probabilità = 100, riferimenti, cicli, overflow…); se non è valido viene ignorato con un errore in console. Cancellalo per tornare ai default. **Officina → Validate Config** controlla a mano.
- La validazione (`ConfigValidator`) e i test impediscono tabelle di probabilità che non sommano a 100%.

## Annunci rewarded: configurazione e modalità mock
- **Mock (default in Editor e Development Build):** `UnityMockAdService` mostra un finto annuncio da 3 s; il bonus è dato solo a conteggio completato, "Chiudi" prima = nessun bonus. In *Altro → Strumenti sviluppo* si può forzare: completo / errore / chiusura anticipata / non disponibile / nessuna rete / non risponde (timeout).
- **Build di release senza SDK reale:** gli annunci sono **disattivati** (nessun finto annuncio agli utenti finali).
- **AdMob reale:** non integrato e non verificato. Vedi [Docs/MONETIZATION.md](Docs/MONETIZATION.md) per i passi, gli identificativi da configurare (`AdUnitIds.cs`: gli ID di produzione sono **vuoti di proposito**; quelli di test sono gli ID pubblici ufficiali Google) e il consenso (UMP **non** implementato).

## Struttura
```
Assets/Scripts/Core/        Logica pura C# (asmdef senza riferimenti a UnityEngine)
  Config/ Economy/ Production/ Progression/ RiskReward/ Monetization/ Analytics/ SaveSystem/ Util/  + GameSession.cs
Assets/Scripts/Unity/       Bootstrap, UI uGUI da codice, audio procedurale, servizi annunci, localizzazione
Assets/Editor/              Menu Officina (impostazioni progetto, scena, export config, build Android)
Assets/Tests/EditMode/      Test NUnit
Tools/Tests.Dotnet/         Esegue Core + test con `dotnet test`
Tools/Simulator/            Simulatore di bilanciamento
Tools/UnityCompileCheck/    Controllo di compilazione parziale (vedi limiti)
Docs/                       Documentazione
```
Documenti: [ARCHITECTURE](Docs/ARCHITECTURE.md) · [GAME_DESIGN](Docs/GAME_DESIGN.md) · [ECONOMY_BALANCE](Docs/ECONOMY_BALANCE.md) · [MONETIZATION](Docs/MONETIZATION.md) · [ANALYTICS](Docs/ANALYTICS.md) · [PRIVACY_AND_POLICY_CHECKLIST](Docs/PRIVACY_AND_POLICY_CHECKLIST.md) · [TEST_REPORT](Docs/TEST_REPORT.md) · [RELEASE_CHECKLIST](Docs/RELEASE_CHECKLIST.md) · [ASSETS_TO_REPLACE](Docs/ASSETS_TO_REPLACE.md) · [CHANGELOG](CHANGELOG.md)

## Cosa è completo / simulato / incompleto
| Area | Stato |
|---|---|
| Economia centralizzata (`Wallet`), formule di costo, produzione con un solo tick, upgrade, revisioni, sinergie | **Completo e testato** (logica) |
| Salvataggi versionati, checksum, backup, recupero, migrazioni, RNG persistito | **Completo e testato** (logica) |
| Guadagni offline con limite e protezione orologio | **Completo e testato** (logica) |
| Ordini (7 tipi) e tutorial | **Completo e testato** (logica) |
| Macchinario sperimentale, Produzione fuori scala, Cassa gratuita | **Completo e testato** (logica) |
| Flusso rewarded (una volta, timeout, richiamate ripetute, riavvio, limiti) | **Completo e testato** con mock; rete reale **non integrata** |
| Analytics (interfaccia + sink di debug) | **Completo**; nessun SDK collegato |
| Simulazioni di bilanciamento | **Eseguite** (ipotesi, non dati reali) |
| UI mobile, audio, bootstrap, scena | **Scritti, mai compilati/eseguiti in Unity** |
| Arte e audio | **Solo segnaposto procedurali** ([ASSETS_TO_REPLACE](Docs/ASSETS_TO_REPLACE.md)); la direzione artistica **non** è completata |
| AdMob / consenso UMP | **Non implementati** (adattatore AdMob scritto ma non verificato) |
| Build Android / iOS | **Mai eseguite** |
| Localizzazione | Solo italiano (sistema a chiavi pronto per altre lingue) |
| Specializzazioni (fase avanzata), prestigio, rewarded per evento | **Non implementati** |

## Stato dei criteri di accettazione
| # | Criterio | Stato |
|---|---|---|
| 1 | Si avvia nella scena prevista | **Non verificato** (nessun Editor) |
| 2 | Raccolta valuta | Logica testata; UI non verificata |
| 3 | Acquisto/potenziamento macchinari | Logica testata; UI non verificata |
| 4 | Produzione corretta | Testata |
| 5 | Salvataggio | Testato (logica); file reale su dispositivo non verificato |
| 6 | Offline coerente e limitato | Testato |
| 7 | Ordini funzionanti | Testato (logica) |
| 8 | Sperimentale mostra costi/probabilità prima | Anteprima testata; schermata non verificata |
| 9 | Cassa senza acquisti | Testato |
| 10 | Produzione fuori scala | Testato (logica) |
| 11 | Rewarded robusto o mock identificato | Mock testato; rete reale assente |
| 12 | Ricompensa base indipendente dall'annuncio | Testato |
| 13 | Niente duplicazione ricompense | Testato per le sequenze previste (limite: salvataggio locale non è inviolabile) |
| 14 | Test automatici passano | **177/177 con dotnet**; Unity Test Runner non eseguito |
| 15 | Build verificata | **No** |
| 16 | Configurazioni e dipendenze documentate | Sì |
| 17 | Checklist di pubblicazione | Sì |
| 18 | Nessun errore bloccante nascosto | Elenco nel [TEST_REPORT](Docs/TEST_REPORT.md); il rischio principale è il codice Unity non compilato |

**Conclusione:** prototipo **non** ancora pronto per un test interno finché non vengono aperti il progetto in Unity, corretti gli eventuali errori di compilazione, eseguiti i test nel Test Runner e fatta una build su un dispositivo reale.
