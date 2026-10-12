# Privacy e policy — checklist di verifica

> Questo documento **non è una consulenza legale** e **non afferma conformità**. Le policy cambiano: ogni voce va verificata sui testi **aggiornati** al momento della pubblicazione. L'uso di una valuta virtuale non rende automaticamente il gioco conforme. Nessuna approvazione degli store è stata richiesta od ottenuta.

## Cosa fa oggi il prototipo (fatti verificabili nel codice)
- Nessun backend, nessun account, nessun acquisto in-app, nessun SDK di analytics o pubblicità incluso.
- Nessun dato personale raccolto o inviato; il salvataggio resta in `Application.persistentDataPath`.
- Gli annunci sono SIMULATI (sviluppo) o DISATTIVATI (release senza SDK).
- Schermata di scelta annunci (personalizzati / non personalizzati / nessuno) con default prudente; nessun annuncio richiesto all'SDK prima della scelta (`ConsentGatedAdService`).
- Nessuna probabilità nascosta; probabilità visibili prima di ogni meccanica casuale; nessuna posta in valuta reale o acquistabile; nessuna conversione dei premi.

## Da verificare prima della pubblicazione
### Google Play
- [ ] Policy su **giochi d'azzardo simulati** e contenuti: le meccaniche (evento sperimentale con esiti probabilistici, cassa gratuita) non usano denaro reale né valute acquistabili — confermare che rientrino fuori dall'ambito "gambling" secondo il testo attuale.
- [ ] Policy su **annunci** e SDK di terze parti (formati rewarded, divieto di annunci ingannevoli, categorie bloccate).
- [ ] Modulo **Sicurezza dei dati / Data safety**: dichiarare i dati raccolti dall'SDK pubblicitario/analytics realmente integrati.
- [ ] **Informativa sulla privacy** pubblicata e linkata in app e in scheda.
- [ ] **Pubblico di destinazione e fascia d'età**: se l'app può attrarre minori valgono regole più severe (programmi per famiglie, annunci certificati). Decidere il target prima di scegliere SDK e configurazione.
- [ ] **Classificazione dei contenuti** (questionario IARC): rispondere con precisione sui meccanismi casuali.
- [ ] Requisiti di **test** per account nuovi (test chiuso con numero minimo di tester/giorni, se applicabile) e target API richiesto.
- [ ] Identificativo pubblicitario: dichiarazione e permesso.

### Apple App Store (futuro)
- [ ] Linee guida su meccaniche a probabilità (obbligo di mostrare le probabilità per oggetti casuali a pagamento: qui non ci sono acquisti, ma verificare il testo corrente) e su valuta virtuale.
- [ ] App Tracking Transparency se si usa l'IDFA; privacy nutrition labels.
- [ ] Configurazione iOS non fatta (id applicazione, ad unit, SKAdNetwork, ecc.).

### SDK e normativa
- [ ] Termini del network pubblicitario scelto (AdMob o altro) e versione del plugin compatibile.
- [ ] **Consenso:** implementare un flusso reale (es. Google UMP / IAB TCF) dove richiesto (SEE/UK/Svizzera ecc.) e le richieste per Stato USA applicabili. La schermata locale attuale non lo sostituisce.
- [ ] GDPR / altre normative: base giuridica, informativa, diritti dell'utente, conservazione.
- [ ] Filtri categoria annunci: escludere gambling/scommesse/casinò e simili nel pannello del network. Non garantiscono l'assenza assoluta di annunci indesiderati.
- [ ] Se si aggiunge analytics: minimizzazione dei dati, nessun identificatore diretto nei parametri (già applicato lato codice).

### Stile "per bambini", inviti e premi (0.4.0)
- [ ] **Pubblico**: lo stile cartoon può attrarre minori. Su Google Play un'app che si rivolge (anche) ai bambini deve rispettare il programma Famiglie: solo SDK pubblicitari certificati, niente annunci personalizzati per i minori, contenuti adatti. Su App Store: categoria Kids e relative regole se applicabile. Decidere il pubblico **prima** di integrare gli annunci.
- [ ] **Premi per la condivisione**: verificare le regole aggiornate degli store e delle piattaforme social sugli incentivi alla condivisione (alcune piattaforme vietano di premiare le condivisioni sui propri servizi). Il gioco premia l'uso del pannello di condivisione del sistema, non un social specifico.
- [ ] **Azzardo simulato**: il gioco non ha slot machine né ruote; la cassa gratuita mostra le probabilità e non si compra. Rispondere con precisione al questionario di classificazione.

## Esito
Nessuna voce è stata verificata o approvata. Non dichiarare conformità o approvazione finché non si è completata la revisione reale.
