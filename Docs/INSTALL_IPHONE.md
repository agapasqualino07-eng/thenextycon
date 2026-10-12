# Installare Officina Tycoon su iPhone

Il gioco per iPhone è una **web app installabile (PWA)**: si apre in Safari e si aggiunge alla schermata Home.
Dopo la prima apertura funziona **anche senza connessione**, a schermo intero, con la sua icona. Non serve l'App Store, né un Mac, né un account sviluppatore Apple.

## 1. Pubblicare il sito (una volta sola)
La repository è **privata**: GitHub Pages su repository private richiede un piano GitHub a pagamento (Pro/Team). Scegli una strada:

| Opzione | Passi | Note |
|---|---|---|
| **A. Repository pubblica + GitHub Pages** (gratis) | 1) Settings → General → *Change visibility* → Public. 2) Settings → Pages → *Source*: **GitHub Actions**. 3) Unisci il branch in `main` (o lancia il workflow *Web app* su `main`). | Indirizzo: `https://agapasqualino07-eng.github.io/thenextycon/`. Il codice diventa visibile a tutti. |
| **B. Repository privata + GitHub Pro** | Come A ma senza cambiare la visibilità. | Il **sito** è comunque pubblico (il codice no). |
| **C. Repository privata, hosting statico esterno** | Actions → ultimo run *Web app* → scarica l'artefatto **officina-tycoon-web** → caricalo su un hosting statico HTTPS (es. trascinando la cartella su un servizio di "drag & drop" per siti statici). | Serve HTTPS (obbligatorio per l'installazione e il funzionamento offline). |

Il workflow `.github/workflows/web.yml` a ogni push compila, esegue **252 test** e **22 test nel browser**, e solo su `main` pubblica su Pages.

## 2. Installare sull'iPhone
1. Apri l'indirizzo del sito con **Safari** (non con altri browser in-app).
2. Tocca **Condividi** (quadrato con freccia) → **Aggiungi alla schermata Home** → **Aggiungi**.
3. Apri il gioco dall'icona "Officina". Al primo avvio scarica ~2 MB, poi resta disponibile offline.

## 3. Salvataggi e backup
- I progressi restano **solo su quel dispositivo** (memoria del browser dell'app). Cancellare l'app dalla Home o i dati del sito di Safari **cancella i progressi**.
- Per sicurezza: *Altro → Backup dei progressi → Copia codice di backup*, incollalo in Note. Per ripristinare o spostarti su un altro dispositivo: incolla il codice e tocca *Ripristina* due volte.
- Il codice è controllato (checksum, versione, limiti): un codice alterato o danneggiato viene rifiutato senza toccare i progressi.

## 4. Aggiornamenti
Dopo una nuova pubblicazione, l'app installata scarica la nuova versione in background: chiudila e riaprila (a volte serve farlo due volte).

## Limiti noti della versione web
- **Niente pubblicità** e quindi niente ricavi: nella versione web non c'è una rete pubblicitaria (i video bonus esistono solo come simulazione nella build di sviluppo).
- Niente notifiche push, niente vibrazione su iPhone.
- I test nel browser usano **Chromium** con dimensioni e tocco da iPhone, non il vero Safari/WebKit: la prova finale va fatta su un iPhone reale (vedi RELEASE_CHECKLIST).

## E un'app nativa da App Store?
Il progetto Unity incluso può diventare un'app iOS nativa, ma richiede: un **Mac con Xcode**, **Unity 6** con modulo iOS, un **Apple ID** (gratuito: installazione sul proprio iPhone valida 7 giorni) o l'**Apple Developer Program** (99 $/anno: TestFlight e App Store). Il livello Unity **non è mai stato compilato**; vedi README.
