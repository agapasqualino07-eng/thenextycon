# Report dei test

Ambiente: container Linux, .NET SDK 8.0.131 (apt), NUnit 3.14, Microsoft.NET.Test.Sdk 17.11.1, C# LangVersion 9 (come Unity 6). **Unity Editor non disponibile.**

## Risultato eseguito (aggiornato alla 0.4.1: 252 unitari, 27 nel browser)
In 0.4.1: 5 test di tocco (tocchi col dito contati una sola volta, multi-touch con due dita via protocollo DevTools, fumetti che non bloccano i pulsanti, niente selezione/menu di pressione lunga e niente zoom da doppio tocco, mascotte che non copre le macchine). Hanno trovato un difetto introdotto dalla prima correzione (doppio conteggio pressione + click su touch), corretto prima del rilascio.

### Risultati 0.4.0
In 0.4.0: `SocialAndRewardsTests` (codice invito, link non validi o con tentativi di iniezione, proprio codice, una volta per dispositivo, condivisione una volta al giorno con orologio all'indietro, cassa via video con limite giornaliero e richiamate ripetute, ordine x2 via video anche se riscosso durante il video, **compatibilità con salvataggi 0.2/0.3**) e 6 test nel browser (mascotte, link di invito, codici malformati, condivisione, cassa via video, ordine x2).

### Risultati 0.3.0
In 0.3.0 si aggiungono `EngagementTests` (Frenesia, regalo giornaliero: orologio, riavvii, bonus video una sola volta, nessun azzeramento) e 6 test nel browser: scena animata, Frenesia, regalo giornaliero, traguardo di livello e nuova fase, effetti ridotti, fluidità (task più lungo del thread principale < 250 ms su Chromium desktop: indicativo, non una misura su iPhone).

### Risultati 0.2.0
```
$ dotnet test Tools/Tests.Dotnet -c Release
Passed!  - Failed: 0, Passed: 217, Skipped: 0, Total: 217, Duration: ~1 s

$ node --test --test-concurrency=1 Web/e2e/game.e2e.cjs     (Chromium 141, viewport 390x844, touch, UA iPhone)
ok 1 - release: boots on an iPhone viewport with no errors, CSP violations or third-party requests
ok 2 - release: first minute — tap, claim tutorial, buy the first machine, production starts
ok 3 - release: progress survives a reload (saved in the browser)
ok 4 - release: every tab renders, nothing overflows horizontally, no button text is cut
ok 5 - release: no simulated ads and no dev tools in the public build, even with ?dev=1
ok 6 - release: backup export -> reset -> import restores progress; invalid code is rejected
ok 7 - release: installable PWA — manifest, icons and offline start via service worker
ok 8 - dev: offline earnings popup, simulated rewarded ad grants the declared bonus once
ok 9 - dev: experimental machine shows odds before paying, needs confirmation, pays once
ok 10 - dev: surge and free crate work and show their odds
# tests 10 · pass 10 · fail 0
```
I test end-to-end girano sui file **pubblicati**, serviti sotto il sottopercorso `/thenextycon/` come su GitHub Pages.
Aggiunti in 0.2.0: `SecurityAndStorageTests` (import non fidato, codici manomessi/troppo grandi/di versione futura, valori estremi, parser con input casuale, mapper che non istanzia tipi arbitrari, storage chiave-valore con backup) e `TextAndFormatTests` (formattazione numeri/tempi, testi con segnaposto, chiavi di testo usate dal codice tutte presenti).
Difetti trovati in 0.2.0 dai test e corretti: arrotondamento "1000M"; accenti come apostrofi; il popup "Aggiungi alla schermata Home" (comportamento voluto) copriva i comandi: ora è verificato e chiuso dai test.
I test sono in `Assets/Tests/EditMode` e si compilano **senza modifiche** sia nel progetto .NET sia nell'Unity Test Framework (asmdef Editor-only con `nunit.framework.dll`). La corsa in Unity **non è stata eseguita**.

## Copertura rispetto alla specifica §13
| Area | Test (file) | Esito |
|---|---|---|
| **Economia** — saldo insufficiente, acquisto valido, doppio tap, costo corretto, produzione, nessun saldo negativo, nessun overflow | `EconomyTests` | Passano |
| **Salvataggi** — salva/carica, dati corrotti (spazzatura, troncati, manomessi), recupero da backup, file .tmp completo/parziale, salvataggio di versione futura, migrazione (con passi sintetici), migrazione mancante/che lancia, valori non validi | `SaveTests` | Passano |
| **Offline** — formula, limite, upgrade, orologio all'indietro, soglia minima, nessuna produzione, riscossione una volta, offerta persistente al riavvio, ripresa da background, avanzamento timer eventi | `SaveTests` | Passano |
| **Duplicazione ricompense** — ordine una volta anche dopo ripristino, ledger limitato, RNG persistito | `SaveTests` | Passano |
| **Eventi casuali** — probabilità valide e somma 100, validatore, esito deterministico (soglie), frequenze empiriche (200k tiri ±0,5 punti), costo una volta, saldo insufficiente, costo mostrato non fidato, rifiuto senza penali, risultato una volta, limite perdita/payout, nessun blocco dopo esito negativo, riavvio durante l'evento (stesso esito, nessun secondo addebito), stati non validi | `RiskRewardTests` | Passano |
| **Produzione fuori scala** — bonus garantito, esiti deterministici, scelta una volta, scadenza e cooldown, ignora gratis, riavvio, tetto bonus | `RiskRewardTests` | Passano |
| **Cassa** — nessun acquisto (nessuna API), solo da gioco, premio di fase una volta anche dopo riavvio, probabilità visibili, esiti deterministici, apertura una volta, overflow→Cr, tetto Cr, nessun valore esterno | `RiskRewardTests` | Passano |
| **Rewarded ads** — completato, non disponibile, errore (SDK/rete/eccezione), chiusura anticipata, richiamata ripetuta, solo-apertura = nessun premio, timeout (+ ricompensa tardiva ignorata), doppio tap, riscossione durante l'annuncio, riavvio durante il flusso, riavvio dopo completamento, cooldown, limite giornaliero (+ riavvio), disattivati, acceleratore, base sempre disponibile, gioco senza annunci, consenso | `RewardedAdTests` | Passano |
| **Progressione/ordini/config/analytics/sessione** — tutorial, ricompense, scadenze, tipi di ordine, fasi, config round-trip JSON e override parziale, validatore (id duplicati, cicli, riferimenti, overflow), sessione rifiuta config invalida, eventi analytics e assenza di chiavi personali | `ProgressionAndConfigTests` | Passano |
| **Prestazioni (solo logica)** — 1 h a 60 FPS (216k tick) e avvio sessione | `ProgressionAndConfigTests` | Passano (tripwire di regressione, **non** un benchmark su dispositivo) |

## NON eseguito / non coperto
- Unity Test Runner (EditMode) e qualunque PlayMode test.
- **Compilazione del livello Unity in Unity** (UI, bootstrap, audio, Editor, AdMob). `Tools/UnityCompileCheck` dà 0 errori ma con API core 2021.3 da NuGet e **stub scritti a mano** per uGUI: controllo parziale.
- Prestazioni reali, avvio su dispositivo, cambio schermata, pausa/background reali, risoluzioni diverse, ricerca di riferimenti a oggetti distrutti, pulsanti inattivi. Sono **verifiche manuali** (RELEASE_CHECKLIST §2).
- Build Android/iOS; SDK pubblicitario; flusso di consenso reale.
- Nessun test automatico sulla UI **Unity** (la UI web è coperta dai test e2e).
- I test e2e usano Chromium, **non WebKit/Safari reale**: provare su un iPhone fisico prima di diffondere il link.

## Difetti trovati dai test/simulatore durante lo sviluppo (tutti corretti)
1. Economia iniziale sbilanciata (tutto sbloccato in ~10 min) — trovato dal simulatore; ridisegnata (vedi ECONOMY_BALANCE).
2. `ConfigValidator` lanciava eccezione con id macchina duplicati — corretto, test aggiunto.
3. Offerta offline con bonus già ottenuto depositata silenziosamente al riavvio — corretto, test aggiunto.
4. `ConfigValidator` troppo severo sul costo al livello massimo — soglia sostituita con controllo di finitezza (<1e90).
5. Diversi errori nei test stessi (helper di riavvio che risalvava l'orologio, aspettative errate): corretti; non erano difetti del gioco.

## Problemi noti ancora aperti
- Livello Unity non compilato/eseguito in Unity (rischio principale; potrebbe contenere errori di compilazione o di layout).
- Scena `Main.unity` scritta a mano (se non si carica: Officina → Create Main Scene; il bootstrap si crea comunque da solo).
- Adattatore AdMob e strumenti Editor non verificati.
- Consenso reale (UMP) non implementato.
- Giocare molto non accelera la progressione; il contenuto termina dopo ~30 giorni (ECONOMY_BALANCE).
- Tasto Indietro Android non gestito; solo lingua italiana; arte/audio segnaposto; specializzazioni non implementate.
