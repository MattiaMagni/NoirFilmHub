# Iterazione 4 - Guida di funzionamento

Questa guida descrive come funziona l'iterazione 4 del progetto, cioe la transizione da una gestione semplice delle proiezioni a una piattaforma completa per:
- catalogo film pubblico avanzato
- gestione multi-cinema e multi-sala
- acquisto posti con mappa sala
- pagamento (carta, credito piattaforma, misto)
- emissione e validazione biglietti elettronici

Riferimento piano ufficiale: `docs/project/dev_iteration/4/PianoDiLavoro.md`.

## 1) Cosa cambia rispetto alle iterazioni precedenti

- Prima: una proiezione era legata a `Cinema + Film + Data + Ora`.
- Ora: uno show e legato a `Film + Sala + StartAt`, quindi il cinema si ricava dalla sala.
- Prima: prenotazione a numero posti generico.
- Ora: selezione posti reali su piantina, con lock temporaneo anti-concorrenza.
- Prima: nessun vero pagamento.
- Ora: checkout con Stripe, credito piattaforma e pagamento misto.
- Prima: nessun ticket digitale validabile.
- Ora: PDF con barcode/QR, endpoint di validazione e vincoli anti cross-cinema.

## 2) Modello dati funzionale (visione pratica)

Entita cardine introdotte/estese:

- `Sala`: ogni cinema puo avere piu sale, anche della stessa tipologia (`ISENSE`, `XL`, `3D`, `2D`).
- `Posto`: rappresenta la piantina della sala (fila, numero, settore, coordinate).
- `Show`: evento schedulato in una sala e a un orario specifico.
- `SeatLock`: lock temporaneo del posto durante il checkout (TTL 8-10 minuti).
- `OrdineAcquisto` + `Biglietto`: risultato finale del pagamento.
- `TransazionePagamento`: traccia i pagamenti carta/credito/misto.
- `RicaricaCredito`: audit completo delle ricariche eseguite da PowerUser/Admin.

Campi chiave:

- `Cinema.CodiceLocale`: obbligatorio per stampa ticket (campo "Codice Locale").
- `Utente.CinemaPreferitoId`: cinema selezionato in modo persistente per esperienza utente coerente.
- `Utente.CreditoPiattaforma`: saldo spendibile in checkout.

Vincoli importanti:

- Unique show: `(SalaId, StartAtUtc)`.
- No overlap in stessa sala: uno show non puo iniziare prima della fine del precedente.
- Unique lock posto/show: `(ShowId, PostoId)`.
- Unique biglietto posto/show: impedisce doppia vendita.

## 3) Programmazione pubblica: come naviga l'utente

## 3.1 `programmazione.html`

La pagina mostra una card per film (non una card per ogni show) con:
- tab `In evidenza` (film con piu show nei prossimi 7 giorni)
- tab `In uscita` (film che partono entro 2 settimane)
- tab `Tutti i film`
- ricerca per titolo
- filtro per categoria
- indicatore "disponibile/non disponibile nel cinema selezionato"

## 3.2 Selezione cinema preferito

La scelta cinema avviene via modale:
- elenco cinema ordinato per distanza (se geolocalizzazione disponibile)
- utente anonimo: persistenza in localStorage
- utente autenticato: persistenza backend nel profilo, con sincronizzazione frontend/backend

## 3.3 `scheda-film.html?idFilm={id}`

Contiene:
- dati film completi (durata, data uscita, genere, descrizione lunga, regista, cast)
- sezione "Vai agli show" con timeline date orizzontale
- lista show per tipologia sala con bottoni orario

Nota broadcast multi-sala:
- se due sale della stessa tipologia hanno lo stesso orario, i bottoni restano entrambi visibili
- non si deduplica per orario
- ogni bottone porta il suo `idSala` specifico

## 3.4 `my-cinemas.html`

Due modalita nello stesso file:
- `/my-cinemas.html`: lista di tutti i cinema
- `/my-cinemas.html?IdCinema={id}`: dettaglio con timeline giorni e programmazione del cinema scelto

Click su showtime:
- autenticato: va a `acquista.html?idCinema={id}&idFilm={id}&idSala={id}&idShow={id}`
- anonimo: redirect login con callback URL encoded

## 4) Autenticazione e callback centralizzata

Ogni pagina che richiede login usa una logica centralizzata tipo:
- `requireAuth(destinationUrl)`

Comportamento:
1. Se non autenticato, redirect a `login.html?callback=<url_encoded_destination>`.
2. Dopo login, il frontend legge `callback` e fa `window.location.href` verso l'URL originale.

Questo evita perdita di contesto durante il funnel di acquisto.

## 5) Flusso acquisto posti e anti-race-condition

## 5.1 `acquista.html`

Mostra:
- riepilogo show (film, cinema, sala, data/ora)
- mappa posti con stati visivi: libero, bloccato, occupato, selezionato
- max 10 posti acquistabili per ordine

## 5.2 Lock posti (`SeatLock`)

Flusso consigliato:
1. Primo posto selezionato -> creazione lock lato backend.
2. TTL lock 8-10 minuti con countdown visibile lato frontend.
3. Se lock scade -> job backend rilascia i posti, frontend notifica e reindirizza a `scheda-film.html`.

Risultato:
- niente doppia vendita dello stesso posto anche con utenti concorrenti.

## 6) Pagamento (`pagamento.html`)

Modalita supportate:
- solo carta (Stripe)
- solo credito piattaforma
- misto (parte credito + parte carta)

Regola di coerenza:
- il frontend calcola in tempo reale le due quote
- `POST /checkout/conferma` riceve entrambi gli importi
- backend finalizza in modo atomico ordine, transazioni e biglietti

## 7) Emissione biglietti PDF e invio email

Dopo pagamento positivo:
- creazione ordine e biglietti
- invio email con riepilogo acquisto
- allegato PDF (un file per ordine, una pagina per ogni biglietto)

Contenuti per pagina:
- titolo film
- data e ora (`GG/MM/AAAA - HH:mm`)
- sala, settore, fila, posto
- tipo evento (`CINEMA`)
- organizzatore
- nome/codice/indirizzo locale
- descrizione biglietto
- breakdown prezzo
- barcode Code128 del codice acquisto
- testo codice acquisto
- QR con URL `https://{host}/tickets/validate/{codiceAcquisto}`

## 8) Validazione biglietti al cinema

Endpoint principali:
- `GET /tickets/validate/{codiceAcquisto}`: sola lettura, usato anche dal QR.
- `POST /tickets/{codiceAcquisto}/validate`: validazione effettiva (PowerUser/Admin).

Pagina operativa:
- `validazione-biglietti.html` (smartphone/tablet addetto)
- supporta inserimento manuale, barcode scanner e flusso QR

Controlli obbligatori:
- vietata validazione cross-cinema (cinema operatore deve coincidere con cinema show)
- vietata doppia validazione (risposta 409 se gia validato)
- salvataggio audit: validato da chi, quando, in quale cinema

## 9) Ricarica credito piattaforma

Pagina: `ricariche-credito.html` (solo PowerUser/Admin).

Flusso:
1. Ricerca utente per email o id (lookup live).
2. Visualizzazione identita e credito attuale.
3. Inserimento importo e conferma.
4. Registrazione audit con: importo, timestamp, beneficiario, operatore, cinema operatore.
5. Storico ricariche filtrabile per data e utente.

## 10) Checklist funzionale rapida

Per considerare l'iterazione completa:

- [ ] Programmazione con tab featured/in uscita/tutti + filtri e ricerca.
- [ ] Cinema preferito coerente tra localStorage e backend.
- [ ] `scheda-film.html` con timeline e show raggruppati per tipologia.
- [ ] `my-cinemas.html` con doppia modalita (lista/dettaglio).
- [ ] Lock posti funzionante con scadenza e rilascio.
- [ ] Pagamento carta/credito/misto funzionante.
- [ ] PDF ticket completo + email inviata.
- [ ] Validazione ticket con controllo cinema operatore e anti doppia vidimazione.
- [ ] Audit trail completo per ricariche e validazioni.

---

Questa guida e pensata per sviluppo e test manuale dell'iterazione 4. Per la pianificazione dettagliata sprint/WBS usare sempre il documento ufficiale in `docs/project/dev_iteration/4/PianoDiLavoro.md`.
