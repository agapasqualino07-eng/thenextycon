# Sicurezza

## Modello di minaccia
Gioco single-player offline. Nessun backend, nessun account, nessun pagamento, nessun dato personale, nessuna chiamata di rete oltre ai file del gioco.
Asset da proteggere: integrità e disponibilità dei progressi del giocatore; il dispositivo/browser del giocatore (niente esecuzione di codice estraneo); la catena di build/pubblicazione.
**Non** è un obiettivo impedire al proprietario del dispositivo di modificare il proprio salvataggio: è un gioco locale, il checksum serve contro corruzione accidentale, non come protezione anti-cheat.

## Revisione eseguita (12/10/2026)
Revisione mirata dell'intero branch (logica C#, front-end Unity, web app, service worker, workflow). **Nessuna vulnerabilità con sfruttabilità concreta trovata.** Verificato in particolare:
- **Deserializzazione**: `JsonMapper` crea solo i tipi dichiarati dai campi delle classi di salvataggio/config; nessun nome di tipo letto dai dati (test `JsonMapper_OnlyInstantiatesDeclaredFieldTypes`).
- **Import del codice di backup** (input non fidato): stessi controlli del caricamento — dimensione massima (1 MB), parser con profondità e dimensione limitate, checksum, versione, migrazione, sanificazione dei valori all'applicazione; solo un codice valido sostituisce i progressi, che restano come copia `.bak` (test in `SecurityAndStorageTests`).
- **Percorsi file** (Unity): solo nomi costanti, nessun nome derivato dall'utente.
- **XSS** (Blazor): nessun `MarkupString`/HTML grezzo; testi sempre codificati; gli stili dinamici sono solo numeri formattati.
- **JS**: nessun `eval`, `innerHTML`, script dinamici; `localStorage` con chiavi fisse.
- **CSP** (meta tag, perché GitHub Pages non permette intestazioni): `script-src 'self' 'wasm-unsafe-eval'`, nessuno script inline, nessuna origine esterna, `object-src 'none'`, `base-uri 'self'`, `form-action 'none'`. I test nel browser falliscono se compaiono violazioni CSP, errori in console o richieste verso altre origini.
- **Service worker**: mette in cache solo file dello stesso sito elencati nel manifest di build con hash di integrità; risponde solo a richieste GET dello stesso sito.
- **Strumenti di sviluppo** (soldi gratis, orologio, annunci simulati): **non compilati** nella build pubblica (`OFFICINA_DEV`), nemmeno con `?dev=1` (test e2e dedicato).
- **Segreti**: nessuno nel repository; gli ID pubblicitari di produzione sono vuoti di proposito; `*.keystore`/`*.jks` in `.gitignore`.
- **CI**: token in sola lettura, permessi Pages solo nel job di deploy e solo da `main`, `pull_request` (non `pull_request_target`), `persist-credentials: false`, dipendenze npm bloccate da `package-lock.json` con hash di integrità.

## Aggiunte 0.4.0
- Il parametro `?ref=` dell'URL è input non fidato: accettato solo se è un codice di 6 caratteri dell'alfabeto previsto, mai inserito come HTML, rimosso dalla barra degli indirizzi dopo la lettura (test e2e con un tentativo di iniezione HTML).
- La demo pubblica ha un salvataggio separato (`officina-demo.`) e il service worker del gioco non risponde mai per `/demo/`: i trucchi della demo non possono toccare i progressi veri.
- Il premio di condivisione è limitato a una volta al giorno con orologio "solo in avanti" e ledger anti-duplicazione.

## Rinforzi applicati durante la revisione
- Limite di dimensione nel parser JSON e nella decodifica dei salvataggi.
- Conversione numerica saturante (niente overflow con valori enormi in un salvataggio manipolato).
- `ConfigValidator` robusto con id duplicati; `GameSession` rifiuta configurazioni non valide.

## Rischi residui e raccomandazioni
- `style-src 'unsafe-inline'` è necessario per gli stili dinamici (larghezza barre); senza punti di iniezione HTML non è sfruttabile.
- Le GitHub Actions sono fissate a tag di versione (`@v4`), non a SHA: per la massima garanzia fissarle a SHA di commit verificati.
- `localStorage` può essere cancellato dal sistema/utente: per questo esiste il backup manuale.
- Quando si integreranno pubblicità o analytics reali servirà una nuova revisione (privacy, CSP con le origini dei fornitori, consenso).
