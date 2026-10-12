# Monetizzazione

## Stato: **NON operativa**
Esiste l'architettura completa e un mock; **nessuna rete pubblicitaria reale è integrata o verificata**. Nessun ricavo è possibile con questa build.

## Principi
- Solo video **rewarded facoltativi**; nessun banner/interstitial.
- La ricompensa base non dipende mai dalla disponibilità dell'annuncio.
- Il bonus è **dichiarato prima**: "+X Cr extra" (X = 50% della ricompensa offline, `Offline.AdBonusFraction`).
- Nessun premio che renda il gioco ingiocabile senza annunci: dal simulatore, il giocatore senza annunci procede ~30% più lentamente (produzione al giorno 30: 145 M/s contro 205 M/s; vedi [ECONOMY_BALANCE](ECONOMY_BALANCE.md)). È un'ipotesi di simulazione.

## Punti di offerta
| Placement | Premio | Limiti (config `Ads`) |
|---|---|---|
| `offline_bonus` | + `AdBonusFraction` × ricompensa offline | 1 per offerta |
| `production_boost` | ×2 produzione per 300 s | non richiedibile se resta più di 30 s di acceleratore da video |
| `daily_bonus` | regalo giornaliero raddoppiato | 1 per regalo |
Globali: min 120 s tra due annunci, max 8 al giorno, timeout 120 s. Tutto modificabile in `AdsConfig`.

## Flusso e robustezza (testati con mock)
1. Calcolo e persistenza dell'offerta offline. 2. Si mostra la ricompensa base. 3. L'annuncio è facoltativo. 4. Conferma = `OnRewardEarned` del tentativo **corrente**. 5. `GrantBonus` una sola volta (ledger), poi `ClaimOffline`.

Gestiti: annuncio non disponibile · errore di rete (segnalato dall'SDK) · chiusura anticipata (nessun bonus) · richiamate ripetute o tardive (ignorate) · timeout · doppio tap (`Busy`) · riavvio durante il flusso (nessun tentativo sopravvive: nessun bonus, base riscuotibile) · riscossione della base mentre l'annuncio è aperto (il bonus non viene assegnato a un'offerta già chiusa) · eccezione sincrona dell'SDK (trattata come fallimento) · limiti di frequenza e giornalieri (persistiti).

## Video interi senza trucchi
I video si guardano per intero perché il premio arriva **solo** a video completato e viene dichiarato prima (es. "Raddoppia con un video"). Non si usano: pulsanti di chiusura nascosti o finti, annunci non richiesti, timer ingannevoli, premi tolti a chi non guarda. Oltre che scorretti, sono vietati dalle policy di AdMob, Google Play e App Store e portano a sospensione dell'account o rimozione dell'app.

## Pubblicità nella versione web
La web app oggi **non ha pubblicità**. Le strade possibili: (1) app nativa (Unity, AdMob) — richiede Mac/Xcode per iOS; (2) annunci per giochi web di Google (Ad Placement API / H5 Games Ads, tramite AdSense), soggetti ad approvazione dell'account e del sito: andrà aggiunta un'implementazione di `IRewardedAdService` che usa quella API e aggiornata la CSP con le origini del fornitore. Nessuna delle due è implementata.

## Integrare AdMob (da fare, **non verificato**)
1. Importare il Google Mobile Ads Unity plugin compatibile con la versione di Unity (verificare la versione e le dipendenze Android/iOS al momento, es. External Dependency Manager).
2. Inserire l'**App ID** AdMob in Assets → Google Mobile Ads → Settings.
3. Aggiungere il define di scripting `OFFICINA_ADMOB` (abilita `AdMobRewardedAdService` in `AdServices.cs`; scritto contro l'API v8+ **senza averlo compilato**).
4. Compilare `AdUnitIds.AndroidProduction/IosProduction` con gli ad unit **del proprietario** (oggi vuoti). Le build di sviluppo usano gli ID di test pubblici di Google, che non generano ricavi.
5. Configurare nel pannello AdMob l'esclusione delle categorie di annunci gambling/scommesse e altre categorie sensibili. I filtri non garantiscono l'assenza assoluta di annunci indesiderati.
6. Provare su dispositivo con ID di test; verificare callback di ricompensa/chiusura/errore, timeout e rete assente.
7. Solo allora disattivare `Debug.isDebugBuild` come condizione per il mock e testare la build di release.

## Consenso e privacy
Implementato: schermata iniziale con tre scelte (annunci personalizzati / non personalizzati / nessun annuncio), salvata nel file; `ConsentGatedAdService` impedisce di chiedere un annuncio all'SDK prima della scelta o dopo "Nessun annuncio"; gestibile da *Altro → Privacy e annunci*.
**Non implementato:** il flusso di consenso reale (es. Google **UMP**/IAB TCF) e la gestione per Paese/età. Una schermata locale **non** garantisce conformità normativa. Vedi [PRIVACY_AND_POLICY_CHECKLIST](PRIVACY_AND_POLICY_CHECKLIST.md).

## Ricavi
Il target di ~1.000 €/mese è un obiettivo di business, non una proprietà del codice. Dipende da utenti attivi, retention, impression per DAU e eCPM, tutti ignoti finché non esistono dati reali. Non si fanno stime di ricavo in questo documento.
