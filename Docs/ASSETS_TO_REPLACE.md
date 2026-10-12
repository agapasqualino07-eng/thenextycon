# Asset segnaposto da sostituire

La direzione artistica **non** è completata: l'interfaccia usa forme procedurali e colori della palette industriale (arancione sicurezza su grigio-blu scuro).

| Cosa | Oggi | Da produrre |
|---|---|---|
| Icone macchinari (8) | ingranaggi procedurali con numero di denti diverso (`SpriteFactory.Gear`) | illustrazioni 2D riconoscibili (trapano, tornio, fresatrice, CNC, pressa, braccio robotico, linea, fabbrica) con stati bloccato/attivo |
| Fabbrica (schermata principale) | fila di ingranaggi rotanti | scena 2D della fabbrica con linee produttive animate |
| Pulsanti/pannelli | rettangoli arrotondati tinteggiati (`SpriteFactory.RoundedRect`) | set UI coerente (9-slice), icone per navigazione, valuta, ricambi, casse |
| Valuta | testo "Cr" | icona valuta |
| Casse/ricambi/eventi | testo | illustrazioni (nessun aspetto da casinò) |
| Font | font built-in `LegacyRuntime.ttf` | font scelto + eventuale migrazione a TextMeshPro |
| Effetti sonori | toni sinusoidali procedurali (`AudioService`) | SFX brevi e non invasivi: tocco, acquisto, upgrade, ordine, sblocco, ricompensa, evento, errore |
| Musica | assente (il comando esiste ma non c'è nessun brano) | brano/i loop con volume basso |
| Icona app e schermata di avvio | predefinite Unity | icona adattiva Android, splash |
| Screenshot/feature graphic per gli store | assenti | da creare |
