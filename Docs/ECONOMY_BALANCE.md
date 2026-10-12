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
| Regolare (5x6min/giorno, con annunci, usa eventi) | 0.0 h / 0 min | 0.0 h / 1 min | 0.1 h / 3 min | 3.2 h / 7 min | 6.5 h / 16 min | 12.8 h / 24 min | 57.6 h / 78 min | 144.0 h / 180 min |
| Molto attivo (10x10min/giorno, con annunci, usa eventi) | 0.0 h / 0 min | 0.0 h / 1 min | 0.1 h / 3 min | 1.6 h / 10 min | 3.2 h / 21 min | 11.2 h / 71 min | 60.8 h / 280 min | 168.0 h / 700 min |
| Regolare SENZA annunci (5x6min, usa eventi) | 0.0 h / 0 min | 0.0 h / 1 min | 0.1 h / 5 min | 6.4 h / 12 min | 9.6 h / 19 min | 24.0 h / 30 min | 75.2 h / 96 min | 192.0 h / 240 min |
| Regolare, eventi prudenti/garantiti (5x6min, annunci) | 0.0 h / 0 min | 0.0 h / 1 min | 0.1 h / 3 min | 3.2 h / 7 min | 6.4 h / 13 min | 24.0 h / 30 min | 54.5 h / 76 min | 144.0 h / 180 min |
| Regolare che RIFIUTA gli eventi casuali (5x6min, annunci) | 0.0 h / 0 min | 0.0 h / 1 min | 0.1 h / 3 min | 3.2 h / 7 min | 6.4 h / 14 min | 24.0 h / 30 min | 54.5 h / 77 min | 144.0 h / 180 min |

## Produzione di base al secondo, saldo e fasi

| Profilo | Giorno 1 | Giorno 3 | Giorno 7 | Giorno 14 | Giorno 30 | Fase 2 raggiunta (h) | Fase 3 raggiunta (h) | Max ore senza acquisti | Annunci guardati | Eventi completati | Eventi sperimentali: speso -> restituito |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Occasionale (3x4min/giorno, con annunci, rifiuta eventi) | 20.26K | 1.13M | 20.28M | 50.04M | 202.83M | 5.3 | 58.7 | 18.6 | 179 | 0 | 0 -> 0 |
| Regolare (5x6min/giorno, con annunci, usa eventi) | 93.53K | 2.8M | 22.33M | 46.92M | 204.68M | 3.2 | 48.0 | 11.2 | 242 | 296 | 1.69T -> 2.32T |
| Molto attivo (10x10min/giorno, con annunci, usa eventi) | 219.83K | 2.96M | 17.33M | 43.4M | 188.2M | 1.6 | 36.8 | 12.8 | 246 | 597 | 2.95T -> 4.21T |
| Regolare SENZA annunci (5x6min, usa eventi) | 46.18K | 1.3M | 15.11M | 34.55M | 145.23M | 3.3 | 57.6 | 14.4 | 0 | 295 | 943.7B -> 1.46T |
| Regolare, eventi prudenti/garantiti (5x6min, annunci) | 99.44K | 3.04M | 24.4M | 46.95M | 205.53M | 3.2 | 48.0 | 14.4 | 242 | 297 | 848.92B -> 1.02T |
| Regolare che RIFIUTA gli eventi casuali (5x6min, annunci) | 98.79K | 3.03M | 24.4M | 46.95M | 205.49M | 3.2 | 48.0 | 14.4 | 242 | 0 | 0 -> 0 |

## Ora (dall'inizio) in cui ogni ordine viene riscosso - profilo Regolare con annunci

| Ordine | Tipo | Ore |
|---|---|---|
| t1 Primi pezzi | TapCount | 0.00 |
| t2 Un po' di aiuto | MachineLevel | 0.00 |
| t3 Produzione automatica | OutputPerSecond | 0.01 |
| a1 Ordine officina locale | EarnCurrency | 0.02 |
| a2 Secondo macchinario | MachineLevel | 0.04 |
| a3 Utensili migliori | BuyUpgrade | 0.03 |
| a4 Lotto di alberi | ProduceWithMachine | 0.05 |
| a5 Consegna express | EarnCurrency | 0.04 |
| a6 Terza linea | MachineLevel | 3.20 |
| b1 Commessa regionale | OutputPerSecond | 6.41 |
| b2 Esperimenti industriali | CompleteEvent | 3.28 |
| b3 Controllo numerico | MachineLevel | 6.41 |
| b4 Grande commessa | EarnCurrency | 12.83 |
| b5 Reparto presse | MachineLevel | 24.00 |
| b6 Automazione | MachineLevel | 27.20 |
| c1 Contratto industriale | OutputPerSecond | 78.46 |
| c2 Produzione in serie | ProduceWithMachine | 363.26 |
| c3 Fabbrica del futuro | MachineLevel | 360.01 |
| c4 Maestro dell'industria | EarnCurrency | non completato |

## Guadagni cumulati (valuta totale guadagnata) e saldo finale

| Profilo | Giorno 1 | Giorno 7 | Giorno 30 | Saldo finale |
|---|---|---|---|---|
| Occasionale (3x4min/giorno, con annunci, rifiuta eventi) | 64.33M | 2.93T | 226.5T | 67.05B |
| Regolare (5x6min/giorno, con annunci, usa eventi) | 871.79M | 4.04T | 231.07T | 592.88B |
| Molto attivo (10x10min/giorno, con annunci, usa eventi) | 3.47B | 2.95T | 185.77T | 2.55T |
| Regolare SENZA annunci (5x6min, usa eventi) | 319.07M | 1.63T | 90.24T | 187.04B |
| Regolare, eventi prudenti/garantiti (5x6min, annunci) | 681.2M | 4.22T | 229.67T | 2.78T |
| Regolare che RIFIUTA gli eventi casuali (5x6min, annunci) | 664.86M | 4.17T | 225.41T | 2.7T |

