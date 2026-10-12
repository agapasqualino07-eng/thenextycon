# Checklist di rilascio (manuale)

Legenda: `[ ]` da fare · nessuna voce è stata eseguita in questo repository.

## 1. Primo avvio in Unity (blocca tutto il resto)
- [ ] Aprire il progetto con Unity 6 LTS (6000.0.x); lasciare importare i pacchetti.
- [ ] **Correggere gli errori di compilazione** di `Assets/Scripts/Unity` e `Assets/Editor` (mai compilati in Unity). Annotarli in CHANGELOG.
- [ ] Eseguire **Officina → Apply Project Settings** e sostituire company name e application id segnaposto (`com.example.officinatycoon`).
- [ ] Test Runner → EditMode → Run All: tutti verdi (attesi 177).
- [ ] Play in Editor: avvio nella scena `Main`, tutorial, acquisto, offline simulato (dev menu), tutti gli eventi.

## 2. Verifiche su dispositivo reale (non automatizzabili qui)
- [ ] Avvio, frame rate stabile, consumo/temperatura, memoria.
- [ ] Cambio schede, scorrimento liste, nessun testo tagliato su più rapporti (16:9, 18:9, 20:9, 4:3 tablet), notch/safe area, rotazione bloccata.
- [ ] Pausa/ripresa e background: premere Home per minuti/ore, tornare → popup offline corretto; chiusura forzata e riapertura → progressi mantenuti.
- [ ] Salvataggio: uccisione del processo durante acquisto/evento; spazio disco pieno; salvataggio corrotto (sostituire il file) → ripristino da `.bak`.
- [ ] Orologio: spostare avanti/indietro l'ora di sistema → nessun guadagno indebito/ripetuto.
- [ ] Tasto Indietro Android: **non gestito** dal codice (comportamento di default del sistema): decidere se serve.
- [ ] Nessun pulsante principale inattivo: percorrere ogni schermata e ogni pulsante.
- [ ] Audio: on/off, non invasivo; (musica assente).
- [ ] Modalità aereo/niente rete: il gioco funziona; ad non disponibile.

## 3. Annunci (solo dopo integrazione reale)
- [ ] Plugin compatibile importato, App ID, ad unit di produzione, define `OFFICINA_ADMOB`.
- [ ] Test con ID di test: completamento, chiusura anticipata, errore, nessuna rete, richiamate tardive.
- [ ] Consenso reale (UMP) per le regioni richieste; gestione preferenze.
- [ ] Esclusione categorie gambling; verifica a campione degli annunci mostrati.
- [ ] Verificare che nella build di release **non compaia** l'annuncio simulato.

## 4. Bilanciamento e dati
- [ ] Rieseguire il simulatore dopo ogni modifica ai numeri.
- [ ] Decidere cosa fare per chi gioca molto (vedi ECONOMY_BALANCE) e per la fine del contenuto (specializzazioni/prestigio).
- [ ] Collegare un sistema di analytics reale **solo** se serve e dopo aver aggiornato privacy e Data safety.
- [ ] Definire ipotesi, campione e criteri dell'esperimento A/B (vedi ANALYTICS) prima di raccogliere dati.

## 5. Store
- [ ] Policy aggiornate (vedi PRIVACY_AND_POLICY_CHECKLIST): contenuto, annunci, data safety, età, classificazione, informativa.
- [ ] Icona, schermata di avvio, screenshot, descrizione, informativa privacy URL.
- [ ] Firma: keystore di release **conservato fuori dal repository** (`*.keystore` è in `.gitignore`); Play App Signing.
- [ ] Build AAB release IL2CPP ARM64; test su più dispositivi/API; test chiuso → produzione.
- [ ] iOS: configurazione dedicata (non iniziata).

## 6. Dopo il rilascio
- [ ] Monitorare crash/ANR, retention D1/D7/D30, completamento tutorial, tassi di completamento ad, reclami sui meccanismi casuali.
