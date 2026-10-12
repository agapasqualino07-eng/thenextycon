using System.Collections.Generic;

namespace OfficinaTycoon.UnityLayer
{
    /// <summary>
    /// Key-based text lookup so strings can be localised. Only Italian is provided (default language);
    /// adding a language = registering another table with Loc.Register("en", table).
    /// Missing keys fall back to the default language and then to the key itself (visible in QA).
    /// </summary>
    public static class Loc
    {
        const string Default = "it";
        static readonly Dictionary<string, Dictionary<string, string>> Tables = new Dictionary<string, Dictionary<string, string>>();
        public static string Language = Default;

        static Loc() { Register(Default, ItalianTable()); }

        public static void Register(string lang, Dictionary<string, string> table) { Tables[lang] = table; }

        public static string T(string key)
        {
            if (Tables.TryGetValue(Language, out var t) && t.TryGetValue(key, out var v)) return v;
            if (Tables.TryGetValue(Default, out var d) && d.TryGetValue(key, out var dv)) return dv;
            return key;
        }

        public static string T(string key, params object[] args)
        {
            try { return string.Format(T(key), args); } catch (System.FormatException) { return key; }
        }

        static Dictionary<string, string> ItalianTable() => new Dictionary<string, string>
        {
            ["app.title"] = "Officina Tycoon",
            ["app.version"] = "Versione prototipo {0}",
            ["top.ops"] = "+{0}/s",
            ["top.boost"] = "Bonus {0} per {1}",
            ["top.phase"] = "Fase: {0}",
            ["tap.button"] = "Lavora un pezzo",
            ["tap.value"] = "+{0} Cr",

            ["nav.factory"] = "Macchine",
            ["nav.upgrades"] = "Upgrade",
            ["nav.orders"] = "Ordini",
            ["nav.events"] = "Eventi",
            ["nav.settings"] = "Altro",

            ["machine.level"] = "Liv. {0}",
            ["machine.output"] = "+{0}/s",
            ["machine.buy"] = "Acquista {0}",
            ["machine.buy_x"] = "x{0}",
            ["machine.max"] = "Max",
            ["machine.maxed"] = "Livello massimo",
            ["machine.locked"] = "Bloccato",
            ["machine.req_machine"] = "Sblocco: {0} al livello {1}",
            ["machine.req_phase"] = "Sblocco: fase \"{0}\"",
            ["machine.produces"] = "Produce: {0}",

            ["upg.header"] = "Upgrade permanenti",
            ["upg.owned"] = "Acquistato",
            ["upg.locked_machine"] = "Richiede {0} al livello {1}",
            ["upg.locked_phase"] = "Richiede la fase \"{0}\"",
            ["upg.buy"] = "Acquista",
            ["overhaul.header"] = "Revisione macchinari (ricambi: {0})",
            ["overhaul.row"] = "{0}: revisione {1}/{2}",
            ["overhaul.buy"] = "Revisiona\n{0} ricambi",
            ["overhaul.max"] = "Massimo",
            ["overhaul.hint"] = "Ogni revisione dà +{0} alla produzione della macchina. I ricambi si ottengono da ordini e casse.",

            ["orders.header"] = "Ordini industriali",
            ["orders.empty"] = "Nessun ordine al momento. Continua a produrre!",
            ["orders.progress"] = "{0} / {1}",
            ["orders.reward"] = "Ricompensa: {0}",
            ["orders.reward_crate"] = " + {0} cassa",
            ["orders.reward_parts"] = " + {0} ricambi",
            ["orders.claim"] = "Ritira",
            ["orders.deadline"] = "Tempo: {0}",
            ["orders.in_progress"] = "In corso",

            ["events.header"] = "Eventi industriali",
            ["events.free_note"] = "Tutti gli eventi usano solo valuta di gioco guadagnata. Puoi sempre rifiutare, senza penali.",
            ["exp.title"] = "Macchinario sperimentale",
            ["exp.locked"] = "Si sblocca nella fase \"{0}\".",
            ["exp.cooldown"] = "Prossimo prototipo tra {0}",
            ["exp.offer_intro"] = "Un fornitore propone un prototipo. Scegli come collaudarlo:",
            ["exp.prudent"] = "Collaudo prudente",
            ["exp.prudent_info"] = "Costo {0} - esito certo: ricevi {1} - durata {2}",
            ["exp.experimental"] = "Test sperimentale",
            ["exp.experimental_info"] = "Costo {0} - durata {1}. Possibili esiti:",
            ["exp.outcome_row"] = "{0}  {1}: {2}",
            ["exp.outcome_boost"] = " + bonus {0} per {1}",
            ["exp.ev"] = "Valore atteso: circa {0} del costo (media su molti tentativi, non una garanzia).",
            ["exp.max_loss"] = "Perdita massima: {0} del costo.",
            ["exp.accept"] = "Avvia ({0})",
            ["exp.confirm"] = "Conferma: paga {0}",
            ["exp.decline"] = "Rifiuta (nessuna penale)",
            ["exp.not_enough"] = "Saldo insufficiente",
            ["exp.in_progress"] = "Collaudo in corso... {0}",
            ["exp.ready"] = "Risultato: {0}",
            ["exp.ready_payout"] = "Ricavo: {0}",
            ["exp.collect"] = "Ritira il risultato",
            ["exp.prudent_result"] = "Collaudo completato",

            ["surge.title"] = "Produzione fuori scala",
            ["surge.locked"] = "Si sblocca nella fase \"{0}\".",
            ["surge.cooldown"] = "Prossima richiesta tra {0}",
            ["surge.intro"] = "Arriva un picco di richieste! Offerta valida ancora {0}. Gratis, senza penali.",
            ["surge.guaranteed"] = "Bonus garantito",
            ["surge.guaranteed_info"] = "Produzione {0} per {1}",
            ["surge.experiment"] = "Esperimento",
            ["surge.experiment_info"] = "Un esito tra:",
            ["surge.outcome_row"] = "{0}  {1}: produzione {2} per {3}",
            ["surge.ev"] = "Valore medio: {0} secondi extra di produzione (garantito: {1}).",
            ["surge.dismiss"] = "Ignora",
            ["surge.active"] = "Bonus attivo: {0} per {1}",

            ["crate.title"] = "Cassa gratuita di ricambi",
            ["crate.count"] = "Casse disponibili: {0} / {1}",
            ["crate.how"] = "Le casse si guadagnano con ordini e traguardi. Non si possono acquistare.",
            ["crate.odds"] = "Probabilità (somma 100%):",
            ["crate.row"] = "{0}  {1}",
            ["crate.open"] = "Apri una cassa",
            ["crate.none"] = "Nessuna cassa disponibile",
            ["crate.result_currency"] = "Hai ricevuto {0}",
            ["crate.result_boost"] = "Acceleratore {0} per {1}",
            ["crate.result_parts"] = "Hai ricevuto {0} ricambi",
            ["crate.currency_label"] = "{0} (circa {1} s di produzione)",
            ["crate.boost_label"] = "{0}: {1} per {2}",
            ["crate.parts_label"] = "{0}: {1} ricambi",

            ["offline.title"] = "Bentornato!",
            ["offline.away"] = "Sei stato via {0}",
            ["offline.cap"] = "Guadagno massimo raggiunto ({0} di limite).",
            ["offline.base"] = "Guadagni della fabbrica: {0}",
            ["offline.claim"] = "Riscuoti {0}",
            ["offline.claim_bonus"] = "Riscuoti {0} (con bonus)",
            ["offline.ad"] = "Guarda un video: +{0} extra",
            ["offline.ad_note"] = "Facoltativo. Il bonus viene assegnato solo a video completato.",
            ["offline.ad_unavailable"] = "Video non disponibile ora. Puoi comunque riscuotere.",
            ["offline.ad_loading"] = "Video in corso...",
            ["offline.ad_failed"] = "Video non completato: nessun bonus. Puoi riscuotere normalmente.",
            ["offline.ad_done"] = "Bonus ottenuto!",
            ["offline.ad_cooldown"] = "Prossimo video disponibile tra poco.",
            ["offline.ad_limit"] = "Limite giornaliero di video raggiunto.",
            ["offline.ad_disabled"] = "Video disattivati nelle impostazioni.",
            ["offline.anomaly"] = "Orologio del dispositivo incoerente: nessun guadagno offline calcolato.",

            ["boost.ad"] = "Acceleratore {0} per {1} (video)",
            ["boost.ad_note"] = "Facoltativo.",
            ["boost.ad_active"] = "Acceleratore da video attivo",

            ["settings.header"] = "Impostazioni",
            ["settings.sfx"] = "Effetti sonori",
            ["settings.music"] = "Musica (nessun brano incluso nel prototipo)",
            ["settings.on"] = "Attivo",
            ["settings.off"] = "Disattivo",
            ["settings.privacy"] = "Privacy e annunci",
            ["settings.reset"] = "Azzera i progressi",
            ["settings.reset_confirm"] = "Sicuro? Tocca di nuovo per cancellare TUTTI i progressi",
            ["settings.reset_done"] = "Progressi azzerati",
            ["settings.dev"] = "Strumenti sviluppo (solo build di sviluppo)",
            ["settings.mock_note"] = "Gli annunci in questa build sono SIMULATI (nessuna rete, nessun ricavo).",

            ["privacy.title"] = "Privacy e annunci",
            ["privacy.body"] = "Officina Tycoon è gratuito. Puoi guardare video facoltativi per ottenere bonus: il gioco resta completo anche senza. Non raccogliamo dati personali nel gioco; la scelta sotto riguarda il tipo di annunci. In questa build gli annunci sono simulati.",
            ["privacy.personalized"] = "Accetto annunci personalizzati",
            ["privacy.basic"] = "Solo annunci non personalizzati",
            ["privacy.none"] = "Nessun annuncio",
            ["privacy.later"] = "Puoi cambiare scelta in Altro > Privacy e annunci.",

            ["toast.order_done"] = "Ordine completato!",
            ["toast.order_expired"] = "Ordine scaduto: riparte da zero, senza penali.",
            ["toast.crate_earned"] = "Hai guadagnato una cassa!",
            ["toast.phase"] = "Nuova fase: {0}",
            ["toast.not_enough"] = "Crediti insufficienti",
            ["toast.upgrade"] = "Upgrade acquistato",
            ["toast.machine"] = "Macchinario potenziato",
            ["toast.overhaul"] = "Revisione completata",
            ["toast.boost_ad"] = "Acceleratore attivo!",
            ["toast.surge"] = "Bonus di produzione attivato!",
            ["toast.saved"] = "Salvato",
            ["toast.load_corrupt"] = "Salvataggio danneggiato: avviata una nuova partita (il vecchio file è stato conservato).",
            ["toast.load_recovered"] = "Salvataggio ripristinato dalla copia di sicurezza.",
            ["toast.load_newer"] = "Salvataggio creato da una versione più recente: avviata una nuova partita (il file è stato conservato).",

            ["settings.backup"] = "Backup dei progressi",
            ["settings.backup_hint"] = "I progressi sono salvati solo su questo dispositivo. Copia il codice di backup e conservalo (es. in Note) per ripristinarlo o spostarlo su un altro dispositivo.",
            ["settings.export"] = "Copia codice di backup",
            ["settings.copied"] = "Codice di backup copiato",
            ["settings.copy_failed"] = "Copia non riuscita",
            ["settings.import"] = "Ripristina da codice",
            ["settings.import_placeholder"] = "Incolla qui un codice di backup",
            ["settings.import_confirm"] = "Il ripristino sostituisce i progressi attuali (che restano come copia di sicurezza). Tocca di nuovo per confermare.",
            ["settings.import_confirm_btn"] = "Conferma ripristino",
            ["settings.import_ok"] = "Progressi ripristinati",
            ["settings.import_invalid"] = "Codice non valido o danneggiato: nessuna modifica.",
            ["settings.import_too_new"] = "Codice creato da una versione più recente: aggiorna il gioco.",
            ["settings.privacy_web"] = "Questa versione non raccoglie dati personali, non usa cookie di tracciamento, analytics o pubblicità. I progressi restano nella memoria del browser del dispositivo e non vengono inviati a nessuno.",
            ["settings.no_ads"] = "versione senza pubblicità",
            ["install.title"] = "Installa su iPhone",
            ["install.body"] = "Apri questa pagina in Safari, tocca il pulsante Condividi e scegli \"Aggiungi alla schermata Home\". Il gioco si aprirà a schermo intero e funzionerà anche senza connessione.",

            ["common.close"] = "Chiudi",
            ["common.ok"] = "OK",
            ["common.free"] = "Gratis",

            ["ad.mock.title"] = "Annuncio di prova (simulato)",
            ["ad.mock.body"] = "Nel gioco reale qui compare un video. Attendi {0} per ottenere la ricompensa.",
            ["ad.mock.close"] = "Chiudi senza ricompensa",
        };
    }
}
