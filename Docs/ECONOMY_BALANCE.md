# Bilanciamento dell'economia

> Tutti i numeri sono in `Assets/Scripts/Core/Config/DefaultConfig.cs`. Le simulazioni usano **la stessa `GameSession` del gioco** (stesse formule),
> con profili di comportamento **ipotetici**. Non sono prove di retention, engagement o ricavi reali.

## Formule
- Costo del livello n+1 di una macchina: `BaseCost × CostGrowth^n` (`Formulas.LevelCost`); acquisto multiplo = serie geometrica; "Max" = ricerca esatta (verificata contro la forza bruta nei test).
- Produzione macchina: `BaseOutput × Livello × 2^(Livello/25) × Upgrade(macchina) × (1 + 0,15 × Revisioni) × (1 + Σ sinergie) × Upgrade globali`, poi × bonus temporanei (prodotto limitato a ×4).
- Tocco: `(1 + 2% × produzione_base/s) × moltiplicatore tocco`.
- Offline: `secondi (≤ limite) × produzione_base/s × efficienza (50% + upgrade, max 90%)`; limite 8 h (+4 h con upgrade, max 24 h).
- Ricompensa ordine: `max(minimo, secondi_di_produzione × produzione_base/s)`.

## Valore atteso (calcolato dalla configurazione, mostrato anche in UI e nei test)
| Meccanica | Opzione | Valore atteso | Rischio |
|---|---|---|---|
| Macchinario sperimentale | Prudente | 1,20× il costo | nullo |
| Macchinario sperimentale | Sperimentale | 1,349× il costo | perdita ≤ 70% del costo; probabilità di non guadagnare: 30% |
| Produzione fuori scala | Garantito | 90 s-produzione extra | nullo |
| Produzione fuori scala | Esperimento | 103,5 s-produzione extra | 40% di ottenere meno del garantito |
| Cassa | Cr | 128 s di produzione per cassa (solo parte "Cr") | nessuna perdita |

## Cosa hanno trovato le simulazioni (e cosa è stato cambiato)
1. **Prima configurazione: contenuto consumato in ~10 minuti.** Il primo livello di ogni macchina si ripagava in ~30–40 s e le ricompense degli ordini valevano 5–25 minuti di produzione: crescita esplosiva. Correzione: rapporto costo ×30 / produzione ×8 tra una macchina e la successiva (il rientro del primo livello cresce da 30 s a ~3,6 giorni), ricompense ordini ridotte a 30–420 s di produzione, soglie di fase e costi degli upgrade riscalati.
2. **Seconda iterazione: contenuto finito in ~3 giorni, poi plateau.** Cambiato il rapporto produzione/costo (da ×20 a ×8) e il tetto ai bonus temporanei (×8 → ×4).
3. **Politica del simulatore:** all'inizio il "giocatore" aspettava per giorni l'acquisto migliore senza comprare nulla di affordabile (falso blocco). Ora aspetta solo se l'acquisto ottimo è raggiungibile entro 15 minuti.

## Osservazioni sull'esito finale (da leggere con cautela)
- **Eventi accessori:** la produzione al giorno 30 è ~uguale con eventi (204,7 M/s) e rifiutandoli (205,5 M/s): non rendono irrilevante la produzione normale né sono obbligatori.
- **Annunci:** acceleratore + bonus offline portano ~+41% di produzione al giorno 30 rispetto a nessun annuncio (205 M/s contro 145 M/s). Il gioco resta pienamente progressivo senza annunci.
- **Poca differenza tra frequenze di gioco:** il giocatore "occasionale" (3×4 min/giorno) arriva a 203 M/s e il "molto attivo" (10×10 min) a 188 M/s: **giocare di più non accelera la progressione** (il "molto attivo" è addirittura sotto per effetti della politica di acquisto). Causa: oltre ~3 accessi al giorno la produzione è dominata dall'accumulo offline (limite 8 h) e dai costi. **Rischio di design:** chi gioca molto non è premiato. Da valutare con dati reali; possibili leve: bonus di produzione "turno attivo", valore del tocco, ordini a tempo. Non modificato per non inseguire un'ipotesi.
- **Nessun blocco della progressione:** il massimo intervallo senza acquisti è 11–19 ore (cioè tra una sessione e l'altra), non giorni.
- **Contenuto:** dopo ~30 giorni ogni macchina è a livello 30–130 e il ritmo rallenta (produzione ×1,1 al giorno): senza prestigio/nuovi livelli il gioco avrà un fine contenuto. Non implementato.
- **Inflazione:** i premi sono proporzionali alla produzione (non assoluti), le casse e le ricompense degli eventi hanno tetti (`MaxCurrencyOpsSeconds`, `MaxRewardOpsSeconds`), il saldo ha un tetto (1e24). I saldi finali non accumulati indicano che la valuta viene spesa.
- **Limiti del modello:** un solo schema di acquisto (rapporto costo/beneficio), nessuna variabilità tra giocatori, nessun abbandono, nessuna conoscenza reale delle abitudini d'uso.

## Frenesia e regalo giornaliero (0.3.0)
La simulazione include la Frenesia (i profili toccano 1,5–3 volte al secondo durante le sessioni): accelera i primi giorni dei giocatori attivi (giorno 1: 132K/s contro 94K/s del profilo regolare) ma non cambia la progressione a lungo termine (giorno 30: ~205M/s in entrambi i casi). Il regalo giornaliero **non** è simulato: vale tra 2 e 10 minuti di produzione o una piccola quantità di casse/ricambi al giorno, trascurabile rispetto ai guadagni offline.

## Come ripetere
```
dotnet run --project Tools/Simulator -c Release -- 30
SIM_TRACE=1 dotnet run --project Tools/Simulator -c Release -- 3     # traccia della prima sessione su stderr
```

## Risultati della simulazione (30 giorni)

> Generato da `dotnet run --project Tools/Simulator -- 30` con `GameSession` reale (stesse formule del gioco).
> I profili sono ipotesi di comportamento, **non** dati di retention o ricavi reali.

## Valore atteso delle meccaniche casuali

| Meccanica | Opzione | Valore atteso |
|---|---|---|
| Macchinario sperimentale | Collaudo prudente | 1.200x il costo (esito certo) |
| Macchinario sperimentale | Test sperimentale | 1.349x il costo (perdita massima 70% del costo) |
| Produzione fuori scala | Bonus garantito | 90.0 secondi-produzione extra |
| Produzione fuori scala | Esperimento | 103.5 secondi-produzione extra |
| Cassa gratuita | Fondi (valore atteso valuta) | 128.0 secondi di produzione per cassa |

## Tempo al primo acquisto di ogni macchinario (ore reali dall'inizio / minuti attivi di gioco)

| Profilo | Trapano a colonna | Tornio | Fresatrice | Centro CNC | Pressa idraulica | Braccio robotico | Linea di assemblaggio | Fabbrica intelligente |
|---|---|---|---|---|---|---|---|---|
| Occasionale (3x4min/giorno, con annunci, rifiuta eventi) | 0.0 h / 0 min | 0.0 h / 1 min | 0.1 h / 3 min | 5.4 h / 5 min | 10.7 h / 10 min | 29.3 h / 16 min | 72.0 h / 36 min | 144.0 h / 72 min |
| Regolare (5x6min/giorno, con annunci, usa eventi) | 0.0 h / 0 min | 0.0 h / 1 min | 0.0 h / 3 min | 3.2 h / 6 min | 6.4 h / 14 min | 24.0 h / 30 min | 54.4 h / 72 min | 120.0 h / 150 min |
| Molto attivo (10x10min/giorno, con annunci, usa eventi) | 0.0 h / 0 min | 0.0 h / 1 min | 0.0 h / 2 min | 1.6 h / 10 min | 3.2 h / 20 min | 9.6 h / 60 min | 48.0 h / 201 min | 168.0 h / 700 min |
| Regolare SENZA annunci (5x6min, usa eventi) | 0.0 h / 0 min | 0.0 h / 1 min | 0.1 h / 4 min | 6.4 h / 12 min | 9.6 h / 19 min | 24.0 h / 30 min | 75.2 h / 96 min | 216.0 h / 270 min |
| Regolare, eventi prudenti/garantiti (5x6min, annunci) | 0.0 h / 0 min | 0.0 h / 1 min | 0.0 h / 3 min | 3.2 h / 6 min | 6.4 h / 13 min | 12.8 h / 24 min | 54.4 h / 72 min | 144.0 h / 180 min |
| Regolare che RIFIUTA gli eventi casuali (5x6min, annunci) | 0.0 h / 0 min | 0.0 h / 1 min | 0.0 h / 3 min | 3.2 h / 6 min | 6.4 h / 13 min | 12.8 h / 24 min | 57.6 h / 78 min | 144.0 h / 180 min |

## Produzione di base al secondo, saldo e fasi

| Profilo | Giorno 1 | Giorno 3 | Giorno 7 | Giorno 14 | Giorno 30 | Fase 2 raggiunta (h) | Fase 3 raggiunta (h) | Max ore senza acquisti | Annunci guardati | Eventi completati | Eventi sperimentali: speso -> restituito |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Occasionale (3x4min/giorno, con annunci, rifiuta eventi) | 20.26K | 1.13M | 20.28M | 49.91M | 202.79M | 5.3 | 58.7 | 18.6 | 179 | 0 | 0 -> 0 |
| Regolare (5x6min/giorno, con annunci, usa eventi) | 132.55K | 3.29M | 24.74M | 46.99M | 204.62M | 3.2 | 48.0 | 11.2 | 242 | 296 | 1.7T -> 2.42T |
| Molto attivo (10x10min/giorno, con annunci, usa eventi) | 243.3K | 3.61M | 20.06M | 47.38M | 182.46M | 1.6 | 35.2 | 12.8 | 246 | 597 | 2.77T -> 4T |
| Regolare SENZA annunci (5x6min, usa eventi) | 46.51K | 1.23M | 14.45M | 32.44M | 142.15M | 3.3 | 57.6 | 14.4 | 0 | 296 | 872.33B -> 1.26T |
| Regolare, eventi prudenti/garantiti (5x6min, annunci) | 108.33K | 3.03M | 24.08M | 46.57M | 203.92M | 3.2 | 48.0 | 14.4 | 242 | 297 | 842.66B -> 1.01T |
| Regolare che RIFIUTA gli eventi casuali (5x6min, annunci) | 88.3K | 2.8M | 24.38M | 46.89M | 206.2M | 3.2 | 48.0 | 14.4 | 242 | 0 | 0 -> 0 |

## Ora (dall'inizio) in cui ogni ordine viene riscosso - profilo Regolare con annunci

| Ordine | Tipo | Ore |
|---|---|---|
| t1 Primi pezzi | TapCount | 0.00 |
| t2 Un po' di aiuto | MachineLevel | 0.01 |
| t3 Produzione automatica | OutputPerSecond | 0.01 |
| a1 Ordine officina locale | EarnCurrency | 0.02 |
| a2 Secondo macchinario | MachineLevel | 0.03 |
| a3 Utensili migliori | BuyUpgrade | 0.03 |
| a4 Lotto di alberi | ProduceWithMachine | 0.04 |
| a5 Consegna express | EarnCurrency | 0.03 |
| a6 Terza linea | MachineLevel | 0.09 |
| b1 Commessa regionale | OutputPerSecond | 6.40 |
| b2 Esperimenti industriali | CompleteEvent | 3.28 |
| b3 Controllo numerico | MachineLevel | 6.41 |
| b4 Grande commessa | EarnCurrency | 12.80 |
| b5 Reparto presse | MachineLevel | 12.80 |
| b6 Automazione | MachineLevel | 24.01 |
| c1 Contratto industriale | OutputPerSecond | 75.24 |
| c2 Produzione in serie | ProduceWithMachine | 336.06 |
| c3 Fabbrica del futuro | MachineLevel | 360.00 |
| c4 Maestro dell'industria | EarnCurrency | non completato |

## Guadagni cumulati (valuta totale guadagnata) e saldo finale

| Profilo | Giorno 1 | Giorno 7 | Giorno 30 | Saldo finale |
|---|---|---|---|---|
| Occasionale (3x4min/giorno, con annunci, rifiuta eventi) | 64.86M | 2.93T | 226.81T | 897.48B |
| Regolare (5x6min/giorno, con annunci, usa eventi) | 958.82M | 4.7T | 234.21T | 956.73B |
| Molto attivo (10x10min/giorno, con annunci, usa eventi) | 4.85B | 4.22T | 182.83T | 2.9T |
| Regolare SENZA annunci (5x6min, usa eventi) | 331.21M | 1.54T | 83.8T | 385.35B |
| Regolare, eventi prudenti/garantiti (5x6min, annunci) | 892.11M | 4.18T | 230.01T | 379.79B |
| Regolare che RIFIUTA gli eventi casuali (5x6min, annunci) | 839.84M | 4.17T | 236.58T | 2.83T |

