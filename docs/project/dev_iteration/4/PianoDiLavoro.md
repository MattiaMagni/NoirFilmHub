# Piano di lavoro - Iterazione 4: Cinema territoriali, Programmazione avanzata, Ticketing e Pagamenti

## 1) Obiettivo iterazione
Evolvere la piattaforma da gestione "proiezioni semplici" a sistema multi-cinema/multi-sala con:
- programmazione utente avanzata (featured/in uscita/tutti, filtri, ricerca)
- scelta cinema preferito persistita (guest + utente autenticato)
- scheda film pubblica con show per giorno e tipologia sala
- acquisto posti con piantina reale, prevenzione race condition
- pagamento con Stripe + credito piattaforma (anche misto)
- emissione ticket elettronico PDF + barcode/QR + validazione in ingresso
- gestione amministrativa sale, show e ricariche credito

---

## 2) Stato di partenza (as-is)
- Backend: CRUD registi/film/cinema/proiezioni + JWT/RBAC + prenotazioni base.
- Frontend: pagine CRUD e `proiezioni-pubblico.html` con prenotazione numerica (senza posti reali).
- Limiti principali:
  - niente entita Sala/Posto
  - proiezione legata a Cinema (non a Sala)
  - nessun lock posti, nessun checkout reale
  - nessun wallet/ricarica credito
  - nessuna validazione biglietti

---

## 3) Evoluzione modello dati (to-be)

## 3.1 Entita nuove
- `Sala`
  - Id, CinemaId, NumeroProgressivo (univoco nel cinema), Tipologia (`ISENSE`, `XL`, `3D`, `2D`), Nome opzionale
  - Vincolo unique: `(CinemaId, NumeroProgressivo)`

- `Posto`
  - Id, SalaId, Settore, Fila, Numero, CoordX, CoordY, Attivo
  - Vincolo unique: `(SalaId, Fila, Numero)`

- `Show` (sostituisce la proiezione logica attuale)
  - Id, FilmId, SalaId, StartAtUtc, EndAtUtc, PrezzoBase, SupplementoSala, Stato
  - Vincolo unique: `(SalaId, StartAtUtc)`

- `SeatLock` (prenotazione temporanea posto)
  - Id, ShowId, PostoId, UtenteId, ExpiresAtUtc, CreatedAtUtc
  - Vincolo unique: `(ShowId, PostoId)`

- `OrdineAcquisto`
  - Id, UtenteId, ShowId, TotaleLordo, TotaleCreditoUsato, TotaleCarta, Stato, CreatedAtUtc

- `Biglietto`
  - Id, OrdineId, ShowId, PostoId, CodiceAcquisto, PrezzoFinale
  - Campo validazione: `Validato`, `ValidatoAtUtc`, `ValidatoDaUtenteId`, `CinemaValidazioneId`
  - Vincolo unique: `(ShowId, PostoId)`
  - Vincolo unique: `CodiceAcquisto`

- `TransazionePagamento`
  - Id, OrdineId, Metodo (`Stripe`, `CreditoPiattaforma`, `Misto`), Importo, Stato, ExternalId, CreatedAtUtc

- `RicaricaCredito`
  - Id, UtenteId, OperatoreId, CinemaId, Importo, CreatedAtUtc, Note

## 3.2 Entita da estendere
- `Film`
  - aggiungere: `DataUscita`, `DescrizioneLunga` (max 2000), `Cast` (tabella dedicata `FilmCastMember` o campo strutturato)
- `Cinema`
  - aggiungere: `Latitudine`, `Longitudine`
  - `CodiceLocale` (stringa, es. "0131220507688") - usato nella stampa del PDF ticket nella riga "Codice Locale". Campo obbligatorio per l'emissione del biglietto.
  - eventuale `Attivo`
- `Utente`
  - aggiungere: `CinemaPreferitoId`, `CreditoPiattaforma`

## 3.3 Regole forti di dominio
- No overlap show in stessa sala:
  - `newShow.StartAt >= previousShow.StartAt + previousShow.DurataFilm (+ buffer opzionale)`
- Posti acquistabili max 10 per ordine.
- Un posto puo essere:
  - libero
  - bloccato temporaneamente (`SeatLock` valido)
  - venduto (`Biglietto`)
- Broadcast multi-sala: e ammesso che piu sale della stessa tipologia (es. due sale ISENSE) facciano partire lo stesso show in contemporanea. In questo caso `scheda-film.html` e `my-cinemas.html` mostreranno bottoni con lo stesso orario nella stessa tipologia, ma ciascuno con `idSala` diverso. Non deduplicare per orario: raggruppare per tipologia e listare tutti gli orari (anche duplicati) con il rispettivo `idSala` come parametro URL.
- Vincolo unique Show esplicitato: `(SalaId, StartAtUtc)` e equivalente a `(CinemaId, SalaId, Data, OraInizio)` dato che `SalaId` identifica gia univocamente la sala nel cinema. Implementare il vincolo a livello DB come indice unique su `(SalaId, StartAtUtc)`.

---

## 4) Strategia anti-race-condition (best practice)
Implementare lock ottimistico lato API + enforcement DB:

1. Utente seleziona primo posto su `acquista.html`:
   - API crea lock temporaneo `SeatLock` (TTL 8-10 min)
2. Se altro utente prova lo stesso posto:
   - fallisce su vincolo unique `(ShowId, PostoId)` oppure riceve "posto gia bloccato"
3. Conferma pagamento:
   - transazione DB atomica:
     - verifica lock ancora valido e ownership lock
     - crea `Biglietto` per ciascun posto
     - rimuove lock
4. Scadenza lock:
   - job periodico (`IHostedService`) pulisce lock scaduti

Risultato: nessuna doppia vendita posto anche con richieste concorrenti.

---

## 5) API backend da introdurre/aggiornare

## 5.1 Programmazione pubblica
- `GET /programmazione/films`
  - filtri: `categoria`, `search`, `tab=featured|coming|all`, `cinemaId`
- `GET /programmazione/films/{filmId}`
  - dettaglio film + stato disponibilita nel cinema selezionato
- `GET /shows`
  - per film/cinema/giorno, output raggruppato per tipologia sala

## 5.2 Cinema preferito
- `GET /cinemas/nearby?lat=&lng=`
- `GET /auth/me/cinema-preferito`
- `PUT /auth/me/cinema-preferito`
- Guest: gestione via localStorage frontend

## 5.3 Sale e piantine (admin/power)
- CRUD `GET/POST/PUT/DELETE /sale`
- CRUD `GET/POST/PUT/DELETE /sale/{id}/posti`
- endpoint bulk import/replace piantina

## 5.4 Show (admin/power)
- nuova gestione show legata a sala
- validazioni overlap + unique
- endpoint calendario per cinema/sala/giorno

## 5.5 Checkout/Ticketing
- `POST /checkout/locks` (crea/aggiorna lock posti)
- `DELETE /checkout/locks/{id}` (rilascio manuale)
- `POST /checkout/conferma` (finalizza ordine e genera biglietti)
- `GET /tickets/{codiceAcquisto}` (dettaglio ticket)
- `GET /tickets/validate/{codiceAcquisto}`
  - Endpoint pubblico in lettura (non esegue la validazione, mostra solo il dettaglio del biglietto). Usato come URL codificato nel QR code stampato sul PDF, nel formato: `https://{host}/tickets/validate/{codiceAcquisto}`
  - Quando un PowerUser/Admin apre questo URL da smartphone/tablet gia autenticato, la pagina `validazione-biglietti.html` si carica con il codice pre-compilato e l'addetto puo confermare la validazione con un tap.
  - Se l'utente non e autenticato, eseguire redirect a login con callback verso lo stesso URL.
- `POST /tickets/{codiceAcquisto}/validate`
  - Richiede ruolo PowerUser o Admin.
  - Verifica che `Biglietto.Show.Sala.CinemaId` corrisponda al `CinemaId` dell'operatore loggato (impedisce validazione cross-cinema).
  - Imposta `Validato=true`, `ValidatoAtUtc=now()`, `ValidatoDaUtenteId`, `CinemaValidazioneId`.
  - Restituisce errore 409 se il biglietto e gia stato validato (impedisce doppia vidimazione).

## 5.6 Pagamenti/Credito
- Stripe PaymentIntent endpoints
- endpoint wallet:
  - `GET /wallet/me`
  - `POST /wallet/topup` (solo PowerUser/Admin)
  - ledger transazioni e audit operatore

---

## 6) Frontend: pagine utente

## 6.1 `programmazione.html` (ref uci film)
- tabs: `In evidenza`, `In uscita`, `Tutti i film`
- ricerca titolo
- filtro categorie
- card unica per film (non piu per singola proiezione)
- badge "Disponibile nel tuo cinema" / "Non disponibile"

## 6.2 Modale selezione cinema
- elenco cinema ordinato per distanza (geolocalizzazione browser + fallback)
- persistenza:
  - guest -> localStorage
  - logged -> backend profile + sync frontend
- cinema selezionato sempre visibile in header pagina

## 6.3 `scheda-film.html`
- hero film con: copertina, titolo, durata, data rilascio, genere, descrizione lunga, regista, cast
- pulsante "Vai agli show"
- sezione date orizzontale (oggi + prossimi giorni con frecce)
- sotto: cinema selezionato + indirizzo
- elenco tipologie sala con bottoni orari
- click orario:
  - autenticato -> `acquista.html?...`
  - anonimo -> `login.html` + callback redirect
  - Il redirect verso login deve includere un parametro callback: `login.html?callback=/scheda-film.html%3FidFilm%3D{id}%26idShow%3D{id}%26...` Al successo del login, il frontend esegue `window.location.href` verso il callback decodificato. Implementare questa logica in modo centralizzato (es. funzione `requireAuth(destinationUrl)`).

## 6.4 `my-cinemas.html`
- lista cinema a card (nome/citta/indirizzo/tipologie sala)
- dettaglio `?IdCinema=` con timeline giorni orizzontale
- per ogni film del giorno: card con descrizione breve + showtimes raggruppati per tipologia sala
- La pagina gestisce due modalita in base alla presenza del parametro URL:
  - **Senza parametro** (`/my-cinemas.html`): mostra la lista di tutti i cinema gestiti come card (nome, citta, indirizzo, tipologie sale).
  - **Con parametro** (`/my-cinemas.html?IdCinema={id}`): mostra direttamente la timeline giorni + programmazione per quel cinema. Gestire entrambi i casi nello stesso file HTML rilevando il parametro al caricamento pagina.
- Il click su un bottone orario da parte di un utente non autenticato esegue il redirect a: `login.html?callback=/acquista.html%3FidCinema%3D{id}%26idFilm%3D{id}%26idSala%3D{id}%26idShow%3D{id}`

## 6.5 `acquista.html`
- riepilogo show (film, sala, data/ora, cinema)
- piantina posti interattiva per sala
- stato posti: libero/occupato/bloccato/selezionato
- massimo 10 posti
- bottone "Continua" -> `pagamento.html`

## 6.6 `pagamento.html` + esito
- opzioni:
  - solo carta (Stripe)
  - solo credito piattaforma
  - misto credito + carta
- dopo successo:
  - pagina esito
  - email con riepilogo + PDF allegato (1 pagina per biglietto)

---

## 7) Frontend: pagine admin/power

- `sale.html`: CRUD sale + editor piantina posti
- `proiezioni.html` (revamp): gestione show per cinema/sala/giorno
- `ricariche-credito.html`: accessibile solo a PowerUser e Admin.
  - L'operatore cerca l'utente da ricaricare per **email oppure id utente** (campo di ricerca con lookup live che restituisce nome, cognome, email e credito attuale prima di procedere).
  - Inserisce l'importo da ricaricare e conferma.
  - Il sistema registra: importo, timestamp, `UtenteId` del beneficiario, `OperatoreId` (utente loggato), `CinemaId` del cinema dell'operatore (recuperato dal profilo dell'operatore loggato).
  - La stessa pagina mostra la lista delle ricariche effettuate con filtro per data e per utente (audit trail operatore).
- `validazione-biglietti.html`:
  - input manuale codice
  - scanner barcode
  - scanner QR con URL validazione
  - vincolo cinema operatore per evitare utilizzo ticket su sede errata

---

## 8) PDF ticket + validazione
Contenuto ticket (una pagina PDF per ciascun biglietto dell'ordine):
- Titolo film
- Data e ora show (formato: `GG/MM/AAAA - HH:mm`)
- Sala: nome sala, Settore, Fila, Numero posto
- Tipo Evento: `CINEMA`
- Organizzatore: ragione sociale della piattaforma
- Nome Locale: nome del cinema
- Codice Locale: `Cinema.CodiceLocale`
- Indirizzo Locale: indirizzo del cinema
- Descrizione biglietto (es. "Biglietto Intero")
- Breakdown prezzo: PrezzoBase, SupplementoSala, PrezzoTotale
- Barcode (code128) del `CodiceAcquisto`
- Testo del `CodiceAcquisto`
- QR code che codifica l'URL: `https://{host}/tickets/validate/{codiceAcquisto}`

Validazione:
- marca ticket come `Validato=true` con timestamp e operatore
- impedire doppia validazione
- impedire validazione su cinema non coerente con show

---

## 9) Sicurezza, audit e compliance
- RBAC:
  - Utente: acquisto e storico proprio
  - PowerUser/Admin: ricariche + validazione ticket
- Audit trail obbligatorio:
  - ricariche credito
  - validazioni ticket
  - operazioni show/sale
- Idempotenza sui callback pagamento Stripe
- sanitizzazione input e rate limit endpoint sensibili
- integrazione TMDB con token conservato solo lato backend (mai esposto al frontend)

---

## 10) Piano di esecuzione (WBS)

### Sprint 1 - Fondazioni dominio
1. Migrazioni: Sala/Posto/Show + estensioni Film/Cinema/Utente
2. Servizi dominio scheduling (no overlap)
3. Endpoint admin sale/show base

### Sprint 2 - Programmazione pubblica
4. API programmazione aggregata + featured/coming/all
5. Refactor `programmazione.html` + modale cinema + persistenza preferenza
6. Implementazione `my-cinemas.html`

### Sprint 3 - Scheda film e funnel acquisto
7. `scheda-film.html` con timeline date + show grouped
8. `acquista.html` con seatmap e lock posti backend

### Sprint 4 - Pagamento e ticket
9. `pagamento.html` con Stripe + wallet + pagamento misto
10. Creazione ordine, emissione biglietti, email+PDF

### Sprint 5 - Operativita cinema
11. `ricariche-credito.html` (PowerUser/Admin)
12. `validazione-biglietti.html` con scanner e vincoli cinema

### Sprint 6 - Hardening
13. Test concorrenza lock posti
14. Test integrazione pagamenti
15. Test E2E principali flussi utente/admin
16. Aggiornamento documentazione (`status.md`, `changelog.md`)

### Sprint 7 - Integrazione TMDB
17. Estensione modello dati film con campi metadata esterni (descrizione lunga, trailer, cast/crew, TMDB id)
18. Implementazione client TMDB autenticato con Bearer token da variabili ambiente
19. Endpoint admin/power per sync manuale singolo film e batch
20. Job notturno di sincronizzazione metadata (manuale + notturna)
21. Aggiornamento pagine utente/admin per visualizzare metadati arricchiti
22. Test e monitoraggio errori sync (fallback lingua, film non matchati, rate limiting)

---

## 11) Criteri di accettazione
1. `programmazione.html` mostra 1 card per film con tab featured/coming/all, ricerca e filtro categoria.
2. Cinema preferito persistito correttamente (guest/localStorage, user/backend sincronizzato).
3. `scheda-film.html` e `my-cinemas.html` mostrano show per giorno e tipologia sala.
4. Nessuna doppia vendita posti in test concorrente.
5. Acquisto completabile con carta, credito o combinato.
6. Invio email post-acquisto con PDF ticket e codici barcode/QR.
7. Validazione ticket disponibile solo a PowerUser/Admin con tracciamento operatore.
8. Gestione sale/show/ricariche completa in area amministrativa.
9. Integrazione TMDB attiva: ogni film puo essere arricchito con trailer, overview, cast e regia tramite sync manuale e job notturno.

---

## 12) Deliverable Iterazione 4
- Documento piano iterazione (`docs/project/dev_iteration/4/PianoDiLavoro.md`)
- Migrazioni DB Iterazione 4
- Nuovi endpoint backend (sale/show/programmazione/checkout/pagamento/ticket/wallet)
- Nuove pagine frontend utente e admin
- Test unit/integration/E2E aggiornati
- Aggiornamento `docs/project/status.md` e `docs/project/changelog.md`
- Configurazione TMDB (`TMDB_API_READ_TOKEN`) e guida operativa integrazione

## 13) Note implementative per AI agent

Questa sezione riassume i vincoli e comportamenti impliciti che l'AI agent deve rispettare durante l'implementazione per evitare ambiguita.

1. **Parametri URL pagine pubbliche**
   - `scheda-film.html?idFilm={id}`
   - `acquista.html?idCinema={id}&idFilm={id}&idSala={id}&idShow={id}`
   - `my-cinemas.html` (lista) e `my-cinemas.html?IdCinema={id}` (dettaglio)
   - `validazione-biglietti.html?codice={codiceAcquisto}` (pre-compilazione da QR)

2. **Callback login**: ogni redirect verso `login.html` deve includere `?callback=<url_encoded_destination>`. Il frontend post-login legge il parametro ed esegue il redirect. Implementare in modo centralizzato con una funzione `requireAuth(destinationUrl)`.

3. **Broadcast multi-sala**: non deduplicare bottoni orario per tipologia. Ogni bottone porta `idSala` diverso anche se l'orario e identico.

4. **SeatLock TTL**: il lock scade dopo 8-10 minuti dal primo posto selezionato. Il frontend mostra un conto alla rovescia visibile. Alla scadenza i posti vengono rilasciati lato backend (job periodico) e il frontend notifica l'utente reindirizzandolo alla `scheda-film.html`.

5. **Pagamento misto**: il frontend calcola in tempo reale la quota coperta dal credito e la quota residua da pagare con carta. L'API `/checkout/conferma` riceve entrambi gli importi e gestisce le due transazioni in modo atomico.

6. **PDF generazione**: un PDF per ordine, con una pagina per ogni biglietto. Ogni pagina contiene tutti i campi della sezione 8. La libreria usata deve supportare barcode code128 e QR code inline.

7. **Coerenza cinema operatore**: la pagina `validazione-biglietti.html` mostra sempre il cinema associato al profilo dell'operatore loggato e impedisce la validazione di biglietti di show in altri cinema.

8. **Ricerca utente in ricariche-credito**: il lookup accetta sia email che id numerico e restituisce nome, cognome, email e credito attuale prima di procedere alla ricarica.

9. **TMDB API key/token**: usare il token di lettura v3/v4 (API Read Access Token) come `Authorization: Bearer <token>` nelle chiamate backend verso `https://api.themoviedb.org/3`. Non usare il token nel browser e non salvarlo in file client.

## 14) Integrazione The Movie Database (TMDB)

Obiettivo: arricchire automaticamente i film locali con metadati ufficiali (trailer, descrizione, cast, regia e immagini) mantenendo il catalogo coerente e aggiornato.

### 14.1 Configurazione e autenticazione
- Variabili ambiente richieste:
  - `TMDB_API_READ_TOKEN` (obbligatoria)
  - `TMDB_BASE_URL` (default `https://api.themoviedb.org/3`)
  - `TMDB_LANGUAGE` (default `it-IT`)
  - `TMDB_FALLBACK_LANGUAGE` (default `en-US`)
  - `TMDB_REGION` (default `IT`)
  - `TMDB_SYNC_ENABLED` (default `true`)
  - `TMDB_SYNC_HOUR` (default `03`)
- Le richieste usano header `Authorization: Bearer <TMDB_API_READ_TOKEN>`.
- Il token resta esclusivamente lato backend.

### 14.2 Strategia di sincronizzazione
- **Manuale**: endpoint admin/power per sincronizzare un film o un batch su richiesta.
- **Notturna**: job schedulato giornaliero che aggiorna film incompleti o stantii.
- Policy matching iniziale: titolo + anno (con fallback solo titolo) e marcatura `NeedsReview` in caso ambiguo.

### 14.3 Endpoint TMDB da usare
- `GET /search/movie` per ricerca movie id TMDB.
- `GET /movie/{movie_id}?append_to_response=videos,credits,images,release_dates` per dettaglio completo in una chiamata.

### 14.4 Dati da persistere
- Su `Film`: `TmdbMovieId`, `TitoloOriginale`, `DescrizioneLunga`, `Tagline`, `DataUscita`, `BackdropPath`, `TrailerUrl`, `TrailerYouTubeKey`, `VotoMedio`, `NumeroVoti`, `Popolarita`, `UltimaSyncTmdbUtc`, `SyncStato`.
- Tabelle correlate:
  - `FilmCastMember` (nome attore, personaggio, ordine)
  - `FilmCrewMember` (department/job, inclusa regia)
  - `FilmVideo` (tipo video, source, chiave, ufficiale)

### 14.5 Regole di qualita dati
- Trailer preferito: YouTube + tipo Trailer + official=true + lingua primaria, con fallback lingua.
- Regista locale aggiornato da `credits.crew` con `job=Director` (creazione regista se assente).
- Nessuna sovrascrittura cieca di dati manuali in caso di conflitto; registrare audit e stato sync.
