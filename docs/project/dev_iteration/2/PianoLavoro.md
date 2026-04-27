# Piano di lavoro - Iterazione 2 Frontend (FilmAPI)

## 1) Obiettivo iterazione
Realizzare il frontend web didattico in HTML, CSS e JavaScript che consumi gli endpoint esistenti del backend FilmAPI tramite Fetch API, mantenendo coerenza grafica con i mock presenti in `docs/project/dev_iteration/2/stitch/`.

Questa iterazione include:
- setup di un web server statico separato (ASP.NET Core Minimal API dedicata)
- pagine UI principali e componenti riutilizzabili
- CRUD completo lato frontend per le entita API principali
- gestione errori, validazioni client e feedback utente
- nessuna autenticazione reale (login rinviato)

## 2) Stato iniziale e vincoli
- Backend Minimal API e MariaDB gia disponibili e funzionanti.
- Test backend (unit + integration) gia completati con esito positivo.
- Endpoint disponibili:
  - `/registi` (e `/registi/{id}/films`)
  - `/films`
  - `/cinemas`
  - `/proiezioni`
- Login non implementato in questa iterazione (mock o assente).

## 3) Architettura proposta

### 3.1 Progetti separati
- `FilmAPI` (esistente): backend API + DB access.
- `FilmFrontend` (nuovo): applicazione ASP.NET Core Minimal API dedicata al serving dei file statici.

### 3.2 Web server frontend
Nel progetto `FilmFrontend`:
- configurare `UseDefaultFiles()`
- configurare `UseStaticFiles()`
- endpoint opzionale health-check (`GET /health`)

### 3.3 Comunicazione frontend-backend
- consumo API via `fetch`
- URL backend centralizzato in configurazione JS (`api-config.js`)
- configurazione CORS lato backend per sviluppo locale su porta/domain del frontend

### 3.4 CORS backend (sviluppo locale)
Nel backend `FilmAPI` configurare una policy CORS dedicata al frontend, ad esempio:
- `http://localhost:5001`
- `http://127.0.0.1:5001`

Applicare la policy prima del mapping degli endpoint, mantenendo il comportamento aperto solo in sviluppo.

## 4) Struttura cartelle frontend (`wwwroot`)
```text
wwwroot/
|-- assets/
|   |-- images/
|   `-- favicon.ico
|-- components/
|   |-- navbar.html
|   `-- footer.html
|-- css/
|   `-- styles.css
|-- js/
|   |-- api-config.js
|   |-- api-client.js
|   |-- template-loader.js
|   |-- navbar.js
|   |-- home.js
|   |-- registi.js
|   |-- films.js
|   |-- cinemas.js
|   `-- proiezioni.js
|-- index.html
|-- registi.html
|-- films.html
|-- cinemas.html
|-- proiezioni.html
`-- profile.html
```

## 5) Specifiche UX/UI
Riferimento visuale: mock in `docs/project/dev_iteration/2/stitch/` (stile "Noir Concierge").

Linee guida principali:
- Home con navbar, hero section, card film in programmazione e footer.
- Design responsive desktop/mobile.
- CSS custom con variabili (`:root`) per palette, typography e spacing.
- Componenti riutilizzabili caricati dinamicamente (`template-loader.js`).

## 6) Funzionalita pagina per pagina

### 6.1 `index.html` (landing/home)
- Hero introduttiva.
- Sezione "Film in programmazione" con card dinamiche da API `/films`.
- Navbar e footer comuni.

### 6.2 `registi.html`
- Elenco registi (`GET /registi`).
- Creazione (`POST /registi`).
- Modifica (`PUT /registi/{id}`).
- Eliminazione (`DELETE /registi/{id}`).
- Vista correlata film regista (`GET /registi/{id}/films`) come supporto.

### 6.3 `films.html`
- CRUD completo su `/films`.
- Validazione `RegistaId` esistente e `Durata > 0`.
- Gestione campi opzionali `CopertinaPath`, `FilmatoPath`.

### 6.4 `cinemas.html`
- CRUD completo su `/cinemas`.

### 6.5 `proiezioni.html`
- CRUD completo su `/proiezioni`.
- Form con selezioni guidate film/cinema.
- Gestione errore duplicato proiezione (`409 Conflict`).

### 6.6 `profile.html`
- Pagina placeholder (utente guest/mock).

## 7) Layer JavaScript

### 7.1 `api-client.js`
Utility centralizzata per chiamate HTTP:
- `get`, `post`, `put`, `delete`
- parsing JSON robusto
- normalizzazione errori HTTP in messaggi user-friendly

### 7.2 Script di pagina
Ogni pagina CRUD include:
- funzione `loadList()`
- rendering tabella/card
- submit form create/update
- conferma eliminazione e refresh lista

### 7.3 Gestione stato UI
- loading state
- empty state
- error state
- feedback operazioni (alert/toast/banner)

### 7.4 Contratto minimo moduli JS
- `api-config.js`: espone `API_BASE_URL`.
- `api-client.js`: wrapper unico `fetch` con gestione `Content-Type`, parsing JSON e errore normalizzato (`status`, `message`, `details`).
- script pagina (`registi.js`, `films.js`, `cinemas.js`, `proiezioni.js`): orchestrano rendering, submit form e refresh lista.
- `template-loader.js`: carica i componenti HTML condivisi (`navbar`, `footer`) con fallback in caso di errore di rete.

## 8) Validazioni frontend minime
- campi obbligatori non vuoti
- numeri interi/positivi dove richiesto (`Durata`, FK id)
- date/ora in formato corretto per proiezioni
- messaggi chiari per errori `400`, `404`, `409`

### 8.1 Mappatura errori HTTP (UX)
- `400 Bad Request`: mostrare errore di validazione vicino al form + messaggio sintetico.
- `404 Not Found`: mostrare messaggio "risorsa non trovata" e ricarica lista.
- `409 Conflict`: mostrare messaggio esplicito di conflitto (es. proiezione duplicata).
- `500+`: mostrare errore generico e invitare al retry.

## 9) Login in iterazione 2
- Nessuna autenticazione reale.
- Navbar con stato "Guest" o voce login mock.
- Struttura JS predisposta per futura evoluzione a login/logout reale.

## 10) Criteri di accettazione
1. Frontend servito da app ASP.NET Core Minimal API separata con static files.
2. Home conforme a struttura richiesta (navbar + hero + card film + footer).
3. CRUD frontend completo per `registi`, `films`, `cinemas`, `proiezioni`.
4. Tutte le operazioni usano Fetch API verso endpoint backend reali.
5. Gestione errori HTTP con feedback utente.
6. Layout funzionante su desktop e mobile.
7. Codice organizzato in componenti e moduli JS.

## 11) Deliverable iterazione
- `docs/project/dev_iteration/2/PianoLavoro.md` (questo documento)
- nuovo progetto frontend `FilmFrontend`
- cartella `wwwroot` completa di pagine/componenti/css/js
- integrazione con backend via Fetch API
- breve guida run locale (backend + frontend)

## 12) Sequenza attivita (WBS)
1. Setup progetto `FilmFrontend` e middleware static files.
2. Creazione struttura `wwwroot`.
3. Implementazione componenti comuni (`navbar`, `footer`).
4. Implementazione `index.html` con sezione film in programmazione.
5. Implementazione `registi.html` (CRUD + vista film regista).
6. Implementazione `films.html` (CRUD).
7. Implementazione `cinemas.html` (CRUD).
8. Implementazione `proiezioni.html` (CRUD).
9. Rifiniture responsive/UI e gestione errori.
10. Verifica end-to-end manuale delle operazioni principali.

## 13) Verifica e run locale
Sequenza di validazione consigliata:
1. Avviare backend `FilmAPI` sulla porta configurata (es. `http://localhost:5000`).
2. Avviare frontend `FilmFrontend` (es. `http://localhost:5001`).
3. Verificare caricamento componenti comuni (`navbar`, `footer`) su tutte le pagine.
4. Verificare CRUD completo su tutte le entita da UI.
5. Verificare gestione degli errori (`400`, `404`, `409`) con feedback utente.
6. Verificare responsive su viewport mobile e desktop.
