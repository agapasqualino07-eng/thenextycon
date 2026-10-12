# Analytics

## Stato
Esiste l'interfaccia `IAnalyticsService` e un sink di debug in memoria (`DebugAnalytics`, 500 eventi, visibile in *Altro → Strumenti sviluppo → log analytics* e nel log Unity). **Nessun SDK è collegato** e nessun dato lascia il dispositivo.
Le chiavi di parametro tipicamente personali (`email`, `user_id`, `device_id`, `advertising_id`, `idfa`, `gaid`…) vengono scartate da `AnalyticsParams.Of` e da `DebugAnalytics` (test: `Analytics_ForbiddenKeysAreDroppedFromParams`).

## Eventi emessi (tutti coperti da test salvo indicazione)
| Evento | Quando | Parametri |
|---|---|---|
| `game_session_started` | `GameSession.Start` | `session_index`, `days_since_install`, `phase` |
| `game_session_ended` | `GameSession.End` | `duration_s`, `phase` |
| `tutorial_completed` | ultimo ordine tutorial riscosso (una volta) | `seconds_played` |
| `machine_purchased` | livello macchina cambia | `machine_id`, `level` |
| `upgrade_purchased` | upgrade acquistato | `upgrade_id` |
| `order_completed` | ordine riscosso | `order_id` |
| `offline_reward_claimed` | riscossione offline | `seconds`, `with_bonus` |
| `risk_event_shown` | offerta sperimentale o surge comparsa | `type` |
| `risk_event_accepted` | scelta accettata | `type`, `choice` |
| `risk_event_declined` | rifiuto, o offerta surge scaduta | `type`, `reason`(solo scadenza) |
| `risk_event_completed` | risultato incassato / surge applicato | `type`, `choice`, `outcome` |
| `free_crate_opened` | cassa aperta | `entry_id` |
| `rewarded_offer_shown` | offerta annuncio visibile e attivabile | `placement` |
| `rewarded_ad_started` | richiesta mostrata all'SDK | `placement` |
| `rewarded_ad_completed` | ricompensa confermata | `placement` |
| `rewarded_ad_failed` | errore/chiusura anticipata/timeout | `placement`, `reason` |
| `bonus_granted` | bonus assegnato (una volta) | `type` |
Nota: `risk_event_shown` per il Surge è emesso alla comparsa dell'offerta, non alla visualizzazione della schermata.

## Metriche da ricavare (richiedono un backend di analytics non incluso)
Retention D1/D7/D30 (da `game_session_started` + `days_since_install`) · sessioni per utente · durata (`duration_s`) · completamento tutorial · % utenti che usano gli eventi (`risk_event_accepted`/utenti) · % eventi rifiutati · tasso di completamento ad (`completed`/`started`) · video per DAU · ricavo per DAU (dalla rete pubblicitaria) · saldo medio e velocità di progressione (eventi di acquisto, fase) · abbandono dopo esito negativo (sessione successiva a `risk_event_completed` con `outcome=fail`). Il salvataggio contiene già `Stats.RiskShown/RiskDeclined`.
**Non** si dichiara alcun miglioramento di retention in assenza di dati sufficienti.

## Piano di esperimento A/B (da eseguire, non ancora eseguibile in-app)
- **Versione A:** gioco base. **B:** con meccaniche di rischio/ricompensa. Oggi si ottiene A impostando `Experimental.UnlockPhase` e `Surge.UnlockPhase` a un valore oltre l'ultima fase (es. 99) via `game_config.json`; le casse restano (non sono casuali nel senso del rischio ma dipendono da premi di fase/ordini). **Non esiste assegnazione automatica dei gruppi né remote config:** servono due build/track di test (es. test chiusi distinti) o un'implementazione futura.
- **Ipotesi:** B aumenta la retention D7 rispetto ad A. **Metrica primaria:** retention D7. **Secondarie:** sessioni/utente, durata sessione, retention D1, completamento tutorial, video/DAU, abbandono post-esito negativo, rifiuto degli eventi.
- **Campione:** da stimare con un calcolo di potenza una volta nota la retention D7 di base (un test su poche decine di utenti non è conclusivo). **Criterio di successo:** differenza D7 con intervallo di confidenza che esclude 0 e senza peggioramento di D1/ricavi per DAU oltre una soglia da fissare prima. **Interruzione:** peggioramento significativo di D1 in B, reclami sui meccanismi casuali, crash/ANR, problemi di policy.
- **Effetti negativi possibili:** percezione di gioco d'azzardo, abbandono dopo esito negativo, rischio di policy/classificazione età, distrazione dal loop principale.
- Non trarre conclusioni forti da campioni piccoli o incompleti.
