# Tutorial progetto FilmAPI + FilmFrontend

Questa guida spiega in modo pratico e dettagliato come funziona il progetto, con focus su:
- architettura generale
- flusso delle chiamate HTTP
- organizzazione backend/frontend
- CRUD e gestione errori
- avvio locale e troubleshooting

## 1) Panoramica del progetto

Il repository contiene due applicazioni distinte ma integrate:

1. `FilmAPI` (backend)
   - ASP.NET Core Minimal API
   - Entity Framework Core + MariaDB
   - espone endpoint REST per `registi`, `films`, `cinemas`, `proiezioni`

2. `FilmFrontend` (frontend)
   - ASP.NET Core Minimal API usata come static server
   - pagine HTML/CSS/JS in `wwwroot`
   - usa `fetch` per chiamare gli endpoint del backend

Il frontend non accede direttamente al database: comunica solo via HTTP con `FilmAPI`.

## 2) Struttura repository (sezioni principali)

- `Program.cs` (root): bootstrap backend
- `Data/FilmDbContext.cs`: DbContext EF Core e relazioni
- `Model/`: entita (`Regista`, `Film`, `Cinema`, `Proiezione`)
- `DTOs/`: modelli input/output endpoint
- `Endpoints/`: mapping delle route Minimal API
- `tests/`: test unit e integration backend
- `FilmFrontend/Program.cs`: bootstrap static server frontend
- `FilmFrontend/wwwroot/`: pagine HTML, CSS, JS, componenti riusabili
- `scripts/seed_realistic_data.py`: reset + seed dati realistici

## 3) Backend: come funziona

### 3.1 Bootstrap API

In `Program.cs` backend vengono configurati:
- dependency injection
- DbContext EF Core (MariaDB)
- Swagger (documentazione endpoint)
- CORS per consentire chiamate dal frontend locale (`http://localhost:5001`)
- mapping endpoint CRUD

Porta locale configurata: `http://localhost:5000`.

### 3.2 Modello dati e relazioni

Entita principali:

- `Regista`
  - `Id`, `Nome`, `Cognome`, `Nazionalita`
  - relazione 1-N con `Film`

- `Film`
  - `Id`, `Titolo`, `DataProduzione`, `RegistaId`, `Durata`
  - opzionali: `CopertinaPath`, `FilmatoPath`

- `Cinema`
  - `Id`, `Nome`, `Indirizzo`, `Citta`

- `Proiezione`
  - `Id`, `CinemaId`, `FilmId`, `Data`, `Ora`
  - indice univoco su `(CinemaId, FilmId, Data, Ora)` per evitare duplicati

### 3.3 Endpoint esposti

I gruppi CRUD principali sono:

- `/registi`
  - `GET /registi`
  - `GET /registi/{id}`
  - `POST /registi`
  - `PUT /registi/{id}`
  - `DELETE /registi/{id}`
  - `GET /registi/{id}/films` (vista film del regista)

- `/films`
  - `GET /films`
  - `GET /films/{id}`
  - `POST /films`
  - `PUT /films/{id}`
  - `DELETE /films/{id}`

- `/cinemas`
  - `GET /cinemas`
  - `GET /cinemas/{id}`
  - `POST /cinemas`
  - `PUT /cinemas/{id}`
  - `DELETE /cinemas/{id}`

- `/proiezioni`
  - `GET /proiezioni`
  - `GET /proiezioni/{id}`
  - `POST /proiezioni`
  - `PUT /proiezioni/{id}`
  - `DELETE /proiezioni/{id}`

## 4) Frontend: come funziona

### 4.1 Static server

`FilmFrontend/Program.cs` usa:
- `UseDefaultFiles()`
- `UseStaticFiles()`

Quindi `index.html` viene servita come home e tutti i file in `wwwroot` sono raggiungibili via URL.

Porta locale configurata: `http://localhost:5001`.

### 4.2 Organizzazione frontend

In `FilmFrontend/wwwroot` trovi:

- `components/`
  - `navbar.html`
  - `footer.html`

- `css/`
  - `styles.css` (design system condiviso)

- `js/`
  - `api-config.js`: definisce `API_BASE_URL`
  - `api-client.js`: wrapper fetch (`get`, `post`, `put`, `delete`)
  - `template-loader.js`: carica navbar/footer nelle pagine
  - `navbar.js`: logica nav (active link + mobile toggle)
  - `dashboard.js`: logica home dashboard
  - `registi.js`, `films.js`, `cinemas.js`, `proiezioni.js`: logica CRUD per pagina

- pagine HTML
  - `index.html` (dashboard)
  - `registi.html`
  - `films.html`
  - `cinemas.html`
  - `proiezioni.html`
  - `profile.html` (mock)

## 5) Flusso dettagliato di una chiamata fetch

Esempio: creazione regista da `registi.html`.

1. Utente compila il form e clicca "Crea regista".
2. `registi.js` intercetta `submit` e costruisce il payload JSON.
3. `registi.js` invoca `ApiClient.post('/registi', payload)`.
4. `api-client.js` esegue `fetch('http://localhost:5000/registi', { method: 'POST', body: ... })`.
5. Il backend riceve la richiesta e valida i dati.
6. Se valido, inserisce su MariaDB e restituisce `201 Created` con entita creata.
7. `api-client.js` effettua parse JSON e lo ritorna allo script pagina.
8. `registi.js` mostra feedback di successo e ricarica la lista (`GET /registi`).

Lo stesso schema si applica a `film`, `cinema`, `proiezione` con differenze solo nel payload.

## 6) CRUD nel frontend: schema comune

Ogni pagina CRUD ha lo stesso pattern:

1. `loadList()` all'avvio
2. render tabella
3. form create/update
4. delete con conferma
5. refresh lista dopo ogni operazione
6. messaggi stato in pagina (`info`, `success`, `error`)

Questo rende il codice uniforme e facile da estendere.

## 7) Gestione errori HTTP

Nel frontend, gli errori fetch vengono normalizzati da `api-client.js` in un oggetto con:
- `status`
- `message`
- `details`

Mappatura pratica:

- `400`: input non valido
- `404`: risorsa non trovata
- `409`: conflitto (es. proiezione duplicata)
- `500+`: errore server

Le pagine mostrano il problema nel box stato invece di fallire in silenzio.

## 8) CORS: perche serve

Frontend e backend girano su porte diverse (`5001` e `5000`), quindi sono origini diverse.

Senza CORS, il browser blocca le chiamate fetch cross-origin.

Per questo il backend consente esplicitamente le origini del frontend locale.

## 9) Avvio locale completo

### 9.1 Avvio DB

Da root repo:

```bash
docker compose up -d
```

### 9.2 Avvio backend

Da root repo:

```bash
dotnet run
```

Endpoint utili:
- `http://localhost:5000/`
- `http://localhost:5000/swagger`

### 9.3 Avvio frontend

Da `FilmFrontend`:

```bash
dotnet run
```

URL:
- `http://localhost:5001`

## 10) Reset e seed dati realistici

Script disponibile:

```bash
python scripts/seed_realistic_data.py http://localhost:5000
```

Cosa fa:
- svuota i dati esistenti in ordine corretto
- inserisce dataset realistico
- usa `CopertinaPath` con immagini esterne (URL https)
- stampa conteggi finali

Conteggi attesi (dataset corrente):
- registi: 10
- films: 14
- cinemas: 6
- proiezioni: 24

## 11) Testing

Per test backend:

```bash
dotnet test tests/FilmAPI.Tests.csproj
```

I test coprono:
- unit test sui servizi
- integration test sugli endpoint CRUD

## 12) Troubleshooting rapido

### 12.1 Frontend visibile ma niente dati
- controlla backend attivo su `5000`
- controlla CORS backend
- apri console browser per eventuali errori fetch

### 12.2 Errore connessione MariaDB
- verifica `docker compose up -d`
- controlla `.env` (`DB_HOST`, `DB_PORT`, `DB_USER`, `DB_PASSWORD`)

### 12.3 Build frontend fallisce con file lock
- probabile `FilmFrontend.exe` gia in esecuzione
- chiudi processo attivo e rilancia build

## 13) Come estendere il progetto

Esempi evolutivi naturali:
- autenticazione reale (JWT/cookie)
- autorizzazioni ruolo admin/viewer
- paginazione e filtri avanzati lato frontend
- upload reale copertine/filmati
- dashboard con grafici e trend temporali

---

Se stai studiando il flusso, il punto chiave da ricordare e':

**UI (HTML/JS) -> `ApiClient` -> endpoint Minimal API -> EF Core -> MariaDB -> risposta JSON -> aggiornamento UI.**
