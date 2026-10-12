# Game design

## Visione
Idle/tycoon 2D mobile-first: una piccola officina diventa uno stabilimento. Il giocatore lavora pezzi (tocco), compra macchinari, li potenzia,
completa ordini, apre casse guadagnate col gioco e può accettare eventi industriali facoltativi. Valuta: **Crediti (Cr)**, esclusivamente di gioco
(simbolo volutamente non monetario).

## Loop
1. Tocca il banco (valore = base + 2% della produzione/s, ×moltiplicatori) e/o lascia produrre.
2. Compra o potenzia un macchinario → produzione/s sale (feedback: ingranaggi più veloci, suono, impulso).
3. Completa un ordine → ricompensa (Cr, ricambi, casse).
4. Sblocca macchine/upgrade/fasi, reinvesti.
5. Torna dopo un'assenza e riscuoti i guadagni offline (limite 8 h, 50% della produzione di base).

### Primo minuto (tutorial = 3 ordini)
`t1` tocca 10 volte (premio 15 Cr: esattamente il Trapano) → `t2` compra il Trapano → `t3` raggiungi 1 Cr/s. Le meccaniche avanzate (eventi, casse, revisioni) compaiono dopo.

## Progressione (tutto in `DefaultConfig.cs`)
| Fase | Ingresso (Cr totali guadagnati) | Contenuti |
|---|---|---|
| 1 Piccola officina | 0 | Trapano, Tornio, Fresatrice; upgrade base; ordini `t*`/`a*` |
| 2 Officina in crescita | 2 M (+1 cassa) | Centro CNC, Pressa, Braccio robotico; Macchinario sperimentale e Produzione fuori scala; ordini `b*` |
| 3 Stabilimento industriale | 30 G (+2 casse) | Linea di assemblaggio, Fabbrica intelligente; **sinergie** (+1%/livello della macchina sorgente); ordini `c*` |

Macchine: costo base 15 → 3,28·10¹¹, crescita 1,15→1,22 per livello, produzione per livello ×8 per macchina. Ogni 25 livelli la produzione della macchina raddoppia.
Prerequisiti: ogni macchina richiede il livello 5 (10 per le ultime due) della precedente.

## Ordini
Tipi: guadagna Cr, tocchi, livello macchina, acquista upgrade, produzione/s, completa evento, produci con una macchina.
Ricompensa = `max(minimo, secondi_di_produzione × produzione_base/s)` → scala col progresso. Max 4 ordini attivi. Le scadenze (solo ordini "express") **azzerano e ripartono senza penali**. Ogni fase ha almeno 2 ordini che non richiedono gli eventi di rischio e **nessun ordine è bloccato dietro un evento**.

## Modulo Industrial Risk & Reward
Vincoli rispettati (verificati dai test dove indicato): nessuna puntata in denaro reale, nessuna valuta acquistabile, nessuna conversione in beni reali, nessuna cassa acquistabile (test: `Crate_HasNoPurchaseApi`), niente slot/roulette, niente animazioni di quasi-vittoria, probabilità mostrate = probabilità usate (stessa tabella per tiro e UI), nessun acquisto o annuncio richiesto per continuare, rifiuto sempre gratuito.

### A — Macchinario sperimentale (`ExperimentalMachineEvent`)
Offerta dalla fase 2, primo invito dopo 120 s, poi cooldown 900 s (300 s dopo un rifiuto). Prezzi = secondi di produzione di base (minimo 50 Cr) **fissati alla comparsa dell'offerta**.
| Opzione | Costo | Durata | Esito |
|---|---|---|---|
| Collaudo prudente | 60 s di produzione | 20 s | certo: 1,20× il costo (EV 1,20) |
| Test sperimentale | 120 s di produzione | 45 s | 30% difettoso 0,30× · 38% funzionante 1,25× · 24% efficiente 2,00× · 8% eccezionale 3,80× + bonus ×1,5 per 120 s (EV 1,349; perdita massima 70% del costo) |
La schermata mostra costo, ogni esito con probabilità e ricavo, durata, valore atteso e perdita massima **prima** della conferma; l'avvio richiede un secondo tocco di conferma. Un esito negativo non blocca nulla e non propone annunci.

### B — Cassa gratuita di ricambi (`FreePartsCrate`)
Si ottiene solo da ordini e ingresso di fase (max 5 in magazzino; l'eccedenza diventa Cr invece di perdersi). Premi: Cr piccoli 40% (120 s di produzione) · Cr grandi 20% (400 s) · acceleratore ×2 per 120 s 20% · 3 ricambi 17% · "lega rara" 12 ricambi 3%. Limite: i Cr di una cassa non superano 1800 s di produzione. **Ricambi** = risorsa secondaria con funzione concreta: *revisione* di un macchinario (+15% produzione, fino a 5 volte, costo 5·2ⁿ ricambi). Le "lega rara" sono semplicemente una quantità grande di ricambi (nessuna terza risorsa).

### C — Produzione fuori scala (`ProductionSurgeEvent`)
Fase 2, offerta valida 300 s (se ignorata scade, nessuna penale), cooldown 1500 s. Gratuita. *Garantito*: ×2 per 90 s (valore 90). *Esperimento*: 40% ×1,5/60 s · 35% ×2/90 s · 20% ×3/90 s · 5% ×5/120 s (valore atteso 103,5). Il prodotto di tutti i bonus è limitato a ×4.

### D — Offline + rewarded
Vedi [MONETIZATION](MONETIZATION.md).

## Coinvolgimento e grafica (0.3.0, versione web)
- **Fabbrica 2D animata**: ogni macchinario ha un'illustrazione propria (trapano che fora, tornio e fresa che girano, CNC con spia e utensile, pressa che stampa, braccio robotico che salda, linea con nastro e carrelli, fabbrica intelligente con finestre e drone). Le animazioni accelerano con il livello; le macchine acquistabili pulsano; quelle bloccate sono sagome con lucchetto. Un nastro trasporta i prodotti delle macchine possedute; lo sfondo cambia a ogni fase.
- **Feedback**: scintille e monete al tocco (martello sul banco), monete che volano verso il saldo alla riscossione, saldo che "scorre", coriandoli per ordini e nuove fasi, banner ai traguardi di livello (ogni 25: produzione x2), apertura animata della cassa (durata fissa; rarità mostrata = rarità reale; nessun effetto "quasi vinto").
- **Frenesia**: 30 tocchi rapidi (la carica cala di 1,5/s) attivano produzione e tocchi x2 per 12 s. Premia il gioco attivo senza penalizzare chi non tocca.
- **Regalo giornaliero**: calendario di 7 giorni (Cr, ricambi, casse; casse al giorno 4 e 7). Avanza a ogni giorno di gioco e **non si azzera** se salti giorni (niente pressione). Facoltativo: raddoppio con video premiato.
- **Obiettivo in vista**: l'ordine più vicino al completamento è sempre mostrato sotto la fabbrica e si riscuote con un tocco.
- **Accessibilità**: impostazione "Effetti visivi: ridotti" e rispetto dell'opzione di sistema "riduci movimento".

## Non implementato (dichiarato)
- **Specializzazioni** della fase avanzata (previste dal brief): oggi la fase avanzata ha solo macchine costose e sinergie.
- **Prestigio/reset**: l'economia continua a crescere ma il contenuto finisce dopo ~30 giorni (vedi [ECONOMY_BALANCE](ECONOMY_BALANCE.md)).
- Rewarded "ricompensa supplementare per un evento".
- Materiali secondari oltre ai ricambi.
