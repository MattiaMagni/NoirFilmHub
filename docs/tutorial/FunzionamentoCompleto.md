# Noir Film Hub (FilmAPI) — Funzionamento e Funzionalità Complete

Questo documento descrive in modo dettagliato il funzionamento, l'architettura e tutti i casi d'uso dell'applicazione **Noir Film Hub** (nome tecnico: FilmAPI + FilmFrontend), una piattaforma di gestione cinematografica full-stack.

---

## 1. Panoramica del Progetto

Noir Film Hub è una web application per la gestione completa di un circuito di cinema: catalogo film, programmazione, vendita biglietti, pagamenti, validazione e amministrazione. È composta da due applicazioni .NET:

| Componente | Tecnologia | Porta |
|------------|-----------|-------|
| **FilmAPI** (backend) | ASP.NET Core Minimal API + EF Core + MariaDB | `5000` |
| **FilmFrontend** (frontend) | ASP.NET Core Static Server + HTML/CSS/JS | `5001` |

- **Linguaggio**: C# (.NET 9.0)
- **Database**: MariaDB 10.11 (Docker)
- **ORM**: Entity Framework Core con provider Pomelo.MySql
- **Autenticazione**: JWT Bearer + Google/Microsoft OIDC social login
- **Pagamenti**: Stripe Hosted Checkout
- **PDF Ticket**: QuestPDF + SkiaSharp + ZXing.Net (barcode/QR)
- **Email**: MailKit (SMTP)
- **API esterne**: TMDB (The Movie Database)
- **Testing**: xUnit (62 test unit)

---

## 2. Architettura

```
Browser (FilmFrontend)  ──fetch──>  FilmAPI (backend)  ──EF Core──>  MariaDB
       :5001                              :5000                        :3306
                                          │
                                          ├── Stripe API (pagamenti)
                                          ├── TMDB API (arricchimento film)
                                          ├── Google OIDC (social login)
                                          ├── Microsoft OIDC (social login)
                                          └── SMTP (invio email)
```

Il frontend comunica con il backend esclusivamente via HTTP REST. Non c'è accesso diretto al database dal frontend.

---

## 3. Modello Dati

### 3.1 Entità principali

```
Regista (1) ────── (*) Film (*) ────── (*) Categoria     [FilmCategoria join]
   │                    │
   │                    ├── (*) Proiezione
   │                    │        │
Cinema (1) ── (*) Sala ─┘        │
   │                              │
   │                    (*) Prenotazione (*) ────── (1) Utente
   │                              │                      │
   └──────────────────────────────┘                      ├── (*) UserExternalLogin
                                                         ├── (*) AccountActionToken
                                                         ├── (*) SeatLock
                                                         └── (*) UserSecurityAuditLog
```

### 3.2 Entità nel dettaglio

| Entità | Campi chiave | Descrizione |
|--------|-------------|-------------|
| **Regista** | Id, Nome, Cognome, Nazionalita | Regista di uno o più film |
| **Film** | Id, Titolo, TitoloOriginale, DataProduzione, DataUscita, RegistaId, Durata, CopertinaPath, BackdropPath, FilmatoPath, DescrizioneLunga, CastPrincipale, TmdbMovieId, TmdbSyncStato | Film nel catalogo |
| **Categoria** | Id, Nome, Descrizione | Genere cinematografico (Azione, Commedia, etc.) |
| **FilmCategoria** | FilmId, CategoriaId | Join many-to-many Film-Categoria |
| **Cinema** | Id, Nome, Indirizzo, Citta, Capienza, Latitudine, Longitudine, CodiceLocale, Attivo | Cinema fisico della catena |
| **Sala** | Id, CinemaId, NumeroProgressivo, Tipologia (ISENSE/XL/3D/2D), Nome, NumeroFile, PostiPerFila, MappaPostiJson, Attiva | Sala di proiezione con mappa posti |
| **Proiezione** | Id, CinemaId, SalaId, FilmId, Data, Ora, PrezzoBase | Spettacolo programmato (show) |
| **Utente** | Id, Email, NormalizedEmail, PasswordHash, Nome, Cognome, Telefono, Ruolo, CinemaPreferitoId, RefreshToken, LocalCredentialsEnabled, AuthVersion, SecurityStamp, IsDisabled, EmailVerified, CreditoPiattaforma, FailedLoginAttempts, LockoutEndUtc | Account utente con 20+ campi |
| **Prenotazione** | Id, UtenteId, ProiezioneId, DataPrenotazione, NumeroPosti, PostiSelezionati, TotalePrezzo, ImportoCartaUsato, StripeSessionId, CodiceAcquisto, Validato, ValidatoAtUtc, ValidatoDaUtenteId, CinemaValidazioneId, Stato | Biglietto/ordine di acquisto |
| **SeatLock** | Id, ProiezioneId, UtenteId, PostoCodice, CreatedAtUtc, ExpiresAtUtc | Blocco temporaneo posto (anti race-condition) |
| **UserExternalLogin** | Id, UtenteId, Provider, ProviderKey, ProviderDisplayName, TenantId, Email | Account social collegato |
| **AccountActionToken** | Id, UtenteId, TokenHash, TokenType, ExpiresAtUtc, ConsumedAtUtc | Token monouso (reset password, setup, invito) |
| **ExternalAuthState** | Id, ReturnUrl, Provider, Mode, CreatedAtUtc, ExpiresAtUtc | Stato OIDC per flusso social login |
| **ExternalAuthExchangeCode** | Id, CodeHash, StateId, ConsumedAtUtc, CreatedAtUtc | Anti-replay per callback OIDC |
| **UserSecurityAuditLog** | Id, UtenteId, EventType, Provider, IpAddress, UserAgent, Details, CreatedAtUtc | Audit trail eventi di sicurezza |

### 3.3 Ruoli utente

| Ruolo | Costante | Permessi |
|-------|----------|----------|
| **Admin** | `"admin"` | Accesso completo: gestione utenti, cinema, sale, categorie, TMDB, validazione biglietti, inviti |
| **PowerUser** | `"power_user"` | Gestione film, registi, proiezioni, sale, TMDB, validazione biglietti. NO gestione utenti/categorie/cinema |
| **Utente** | `"utente"` | Acquisto biglietti, profilo personale, cinema preferito, visualizzazione programmazione |

---

## 4. Autenticazione e Sicurezza

### 4.1 Flusso JWT

1. **Registrazione** (`POST /auth/register`): crea utente con ruolo `utente`, password hashata con BCrypt
2. **Login** (`POST /auth/login`): valida credenziali, restituisce `AccessToken` (JWT, scadenza 15 min) + `RefreshToken` (opaco, scadenza 7 giorni)
3. **Refresh** (`POST /auth/refresh`): usa il RefreshToken per ottenere un nuovo AccessToken senza rifare login
4. **Logout** (`POST /auth/logout`): invalida il RefreshToken. Con `AllDevices=true` invalida tutte le sessioni
5. **OnTokenValidated middleware**: ad ogni richiesta verifica che l'utente esista, non sia disabilitato, e che l'AuthVersion corrisponda (invalidazione globale token)

### 4.2 Social Login (Google / Microsoft)

- **Inizio** (`GET /auth/external/{provider}`): genera URL di autorizzazione OIDC
- **Callback** (`GET /auth/external/callback`): scambia il codice per token, crea/collega account
- **Linking ibrido**: un utente può avere sia password che social login. Regole:
  - PowerUser/Admin non possono usare solo social
  - Un account social-only non può essere promosso a PowerUser/Admin
  - Se è l'unico metodo di accesso, non si può scollegare il provider social

### 4.3 Gestione Password

- **Cambio password** (`POST /auth/me/change-password`): richiede password corrente. Incrementa AuthVersion (invalida tutti i token esistenti)
- **Password dimenticata** (`POST /auth/forgot-password`): invia email con token monouso. Anti-enumerazione (restituisce sempre 200 OK)
- **Reset password** (`POST /auth/reset-password`): consuma il token e imposta nuova password
- **Setup password** (`POST /auth/setup-password`): per account social-only, imposta una password locale
- **Forza reset** (`POST /auth/admin/utenti/{id}/force-password-reset`): solo Admin, invia email di reset forzato

### 4.4 Audit di Sicurezza

Ogni evento di sicurezza viene tracciato in `UserSecurityAuditLog`:
- Login riuscito/fallito
- Registrazione
- Cambio/Reset/Setup password
- Social login collegato/scollegato
- Disabilitazione/Abilitazione account
- Cambio ruolo
- Invito utente
- Logout globale

---

## 5. Casi d'Uso per Ruolo

### 5.1 Visitatore Anonimo (non autenticato)

| Caso d'uso | Come si realizza |
|------------|-----------------|
| **Navigare la programmazione** | `programmazione.html`: tab In evidenza/In uscita/Tutti, ricerca titolo, filtro categoria |
| **Vedere dettaglio film** | `scheda-film.html?idFilm={id}`: dati completi, cast, regista, calendario show, trailer |
| **Scegliere cinema preferito** | Modale in programmazione: elenco cinema ordinato per distanza (via geolocalizzazione), scelta salvata in localStorage |
| **Vedere mappa posti** | `acquista.html`: mappa interattiva della sala con posti liberi/bloccati/occupati |
| **Registrarsi** | `register.html`: form email/password/nome/cognome + pulsanti Google/Microsoft |
| **Login** | `login.html`: email/password + pulsanti Google/Microsoft |
| **Recuperare password** | `recupera-password.html`: inserisce email, riceve link di reset |
| **Validare biglietto (solo lettura)** | `GET /tickets/validate/{codiceAcquisto}`: accesso pubblico via QR |
| **Vedere cinema per città/tipologia** | `my-cinemas.html`: lista cinema con filtri città, tipologia sala, geolocalizzazione |

### 5.2 Utente Autenticato (ruolo `utente`)

Tutti i casi d'uso anonimi, più:

| Caso d'uso | Come si realizza |
|------------|-----------------|
| **Acquistare biglietti** | `acquista.html` → `pagamento.html`: seleziona posti su mappa, lock scade in 8-10 min, paga con Stripe o credito |
| **Vedere i miei biglietti** | `GET /prenotazioni/mie`: storico prenotazioni con stato |
| **Scaricare PDF ticket** | `GET /tickets/{codiceAcquisto}/pdf`: PDF con barcode Code128, QR, dettagli posto |
| **Gestire profilo** | `profile.html`: modifica nome/cognome/telefono, cambio password, revoca sessioni |
| **Collegare account social** | `profile.html` → login con Google/Microsoft, linking all'account esistente |
| **Setup password (se social-only)** | `profile.html`: richiede email di setup, poi `setup-password.html` |
| **Cinema preferito persistente** | `profile.html` o programmazione: salvato lato backend, sincronizzato col frontend |
| **Annullare prenotazione** | `PUT /prenotazioni/{id}/annulla`: solo prenotazioni proprie |
| **Vedere programmazione cinema** | `my-cinemas.html?IdCinema={id}`: timeline 31 giorni con show raggruppati per tipologia sala |

### 5.3 PowerUser

Tutti i casi d'uso Utente, più:

| Caso d'uso | Come si realizza |
|------------|-----------------|
| **CRUD Film** | `films.html`: crea/modifica/elimina film con categorie, copertina, trailer, cast |
| **CRUD Registi** | `registi.html`: gestione anagrafica registi |
| **CRUD Proiezioni** | `proiezioni.html`: crea spettacoli (data, ora, sala, prezzo), validazione automatica overlap |
| **CRUD Sale** | `sale.html`: gestione sale per cinema (tipologia, file, posti, mappa) |
| **Validare biglietti** | `validazione-biglietti.html`: inserimento codice, scansione barcode, validazione. Vincolo: stesso cinema dell'operatore |
| **Sincronizzare TMDB** | `tmdb-admin.html`: sync manuale per film, sync batch, ricerca/import da TMDB, visualizzazione ultimi film |
| **Ricarica credito** | `POST /pagamenti/ricarica` (se implementato): ricarica credito piattaforma a utenti |

### 5.4 Admin

Tutti i casi d'uso PowerUser, più:

| Caso d'uso | Come si realizza |
|------------|-----------------|
| **CRUD Cinema** | `cinemas.html`: gestione cinema (nome, indirizzo, città, coordinate, capienza, codice locale) |
| **CRUD Categorie** | `categorie.html`: crea/modifica/elimina generi cinematografici |
| **Gestione Utenti** | `utenti.html`: tabella con ricerca, filtri (ruolo, stato), paginazione, azioni (promuovi/degradazione, disabilita/abilita, forza reset password, elimina) |
| **Dettaglio sicurezza utente** | `GET /auth/admin/utenti/{id}`: dati completi utente, external login, audit log recente |
| **Invitare utenti** | `POST /auth/admin/invite`: invia email di invito con ruolo specificato (Admin/PowerUser) |
| **Vedere tutti i biglietti** | `GET /prenotazioni`: lista completa di tutte le prenotazioni |
| **Dashboard admin** | `dashboard.html`: KPI e statistiche |

---

## 6. Flusso di Acquisto Biglietti (End-to-End)

```
1. Navigazione               2. Scelta Show           3. Mappa Posti              4. Lock Posti
programmazione.html ──────> scheda-film.html ──────> acquista.html ──────────> POST /checkout/locks
(esplora film)              (calendario date,         (mappa interattiva,        (blocca posti per
                             seleziona orario)         stati: libero/bloccato/    8-10 minuti)
                                                       occupato/selezionato)

5. Pagamento                                    6. Conferma                           7. Download
pagamento.html ────────────────────────────> Stripe Checkout ──────────────────> esito-pagamento.html
(sceglie carta/credito/misto)                  (pagamento esterno)                   (riepilogo, link PDF)

8. Email + PDF                                   9. Validazione al Cinema
Email con PDF allegato ────────────────────> validazione-biglietti.html
(barcode Code128, QR, dettagli film/posto)       (operatore scannerizza/vidima)
```

### Dettaglio race-condition:
- **SeatLock**: quando un utente seleziona un posto, viene creato un lock con TTL 8-10 minuti
- Due utenti non possono lockare/acquistare lo stesso posto
- Se il lock scade, un job di cleanup lo rilascia automaticamente
- Ogni utente vede i lock degli altri come "bloccati" (non selezionabili)

### Modalità di pagamento:
- **Solo carta**: Stripe Hosted Checkout
- **Solo credito**: usa `CreditoPiattaforma` dell'utente
- **Misto**: parte credito + parte carta (il frontend calcola le quote)

---

## 7. Integrazione TMDB

Il sistema si integra con The Movie Database per arricchire i film locali:

| Operazione | Endpoint | Descrizione |
|------------|----------|-------------|
| Stato configurazione | `GET /tmdb/status` | Verifica se TMDB è configurato |
| Sync singolo film | `POST /tmdb/sync/film/{id}` | Arricchisce un film con dati TMDB (trama, cast, poster, trailer) |
| Sync batch | `POST /tmdb/sync/films` | Sincronizza tutti i film con dati mancanti |
| Ultime uscite | `GET /tmdb/latest` | Recupera ultimi film da TMDB (con paginazione) |
| Ricerca | `GET /tmdb/search?title=` | Cerca film su TMDB per titolo |
| Import | `POST /tmdb/import-latest` | Importa film selezionati nel database locale |
| Film mancanti | `GET /tmdb/missing` | Elenca film locali senza metadati TMDB |

Inoltre un **job notturno** (`TmdbSyncHostedService`) sincronizza automaticamente i film all'orario configurato.

**Sicurezza**: il token TMDB API è solo lato server, mai esposto al frontend.

---

## 8. Sistema di Biglietti e Validazione

### 8.1 Emissione Biglietti

Dopo il pagamento andato a buon fine:
1. La prenotazione passa da `PendingStripe` a `Confermata`
2. Viene generato un `CodiceAcquisto` univoco (formato `NFH-YYYYMMDDHHmmss-RANDOM`)
3. Viene creato il PDF ticket con:
   - Titolo film, data/ora, sala, settore, fila, posto
   - Nome e codice locale del cinema
   - Barcode Code128 del codice acquisto
   - QR code con URL `https://{host}/tickets/validate/{codiceAcquisto}`
   - Breakdown del prezzo
4. Il PDF viene inviato via email all'utente

### 8.2 Validazione al Cinema

- **Pagina**: `validazione-biglietti.html` (ottimizzata per tablet/smartphone addetto)
- **Metodi di input**: inserimento manuale codice, scansione barcode, scansione QR
- **Controlli**:
  - Il cinema dell'operatore deve coincidere con il cinema dello show (no validazione cross-cinema)
  - Non si può validare due volte lo stesso biglietto (409 Conflict)
  - Viene registrato chi ha validato, quando e in quale cinema
- **Ruolo richiesto**: Admin o PowerUser

---

## 9. API Endpoints Completi

### 9.1 Auth (`/auth/*`)

| Method | Endpoint | Auth | Descrizione |
|--------|----------|------|-------------|
| POST | `/auth/register` | Pubblica | Registra nuovo utente |
| POST | `/auth/login` | Pubblica | Login con email/password |
| POST | `/auth/refresh` | Pubblica | Rinnova access token |
| POST | `/auth/logout` | Utente | Logout (o logout globale) |
| GET | `/auth/me` | Utente | Profilo utente corrente |
| PUT | `/auth/me` | Utente | Modifica nome/cognome/telefono |
| GET | `/auth/me/cinema-preferito` | Utente | Cinema preferito |
| PUT | `/auth/me/cinema-preferito` | Utente | Imposta cinema preferito |
| POST | `/auth/me/change-password` | Utente | Cambio password |
| POST | `/auth/forgot-password` | Pubblica | Richiedi reset password |
| POST | `/auth/reset-password` | Pubblica | Completa reset password |
| POST | `/auth/me/request-password-setup` | Utente | Richiedi setup password (social) |
| POST | `/auth/setup-password` | Pubblica | Completa setup password |
| POST | `/auth/revoke-all-sessions` | Utente | Revoca tutte le sessioni |
| GET | `/auth/me/external-logins` | Utente | Lista provider social collegati |
| DELETE | `/auth/me/external-logins/{id}` | Utente | Scollega provider social |
| GET | `/auth/external/{provider}` | Pubblica | Inizia social login |
| GET | `/auth/external/callback` | Pubblica | Callback OIDC |
| GET | `/auth/admin/utenti` | Admin | Lista utenti con filtri/paginazione |
| GET | `/auth/admin/utenti/{id}` | Admin | Dettaglio completo utente |
| PUT | `/auth/admin/utenti/{id}/ruolo` | Admin | Cambia ruolo utente |
| PUT | `/auth/admin/utenti/{id}/disable` | Admin | Disabilita utente |
| PUT | `/auth/admin/utenti/{id}/enable` | Admin | Abilita utente |
| POST | `/auth/admin/utenti/{id}/force-password-reset` | Admin | Forza reset password |
| DELETE | `/auth/admin/utenti/{id}` | Admin | Elimina utente |
| POST | `/auth/admin/invite` | Admin | Invita nuovo utente |

### 9.2 Registi (`/registi/*`)

| Method | Endpoint | Auth | Descrizione |
|--------|----------|------|-------------|
| GET | `/registi` | Pubblica | Lista registi |
| GET | `/registi/{id}` | Pubblica | Dettaglio regista |
| POST | `/registi` | Admin/PowerUser | Crea regista |
| PUT | `/registi/{id}` | Admin/PowerUser | Modifica regista |
| DELETE | `/registi/{id}` | Admin/PowerUser | Elimina regista |
| GET | `/registi/{id}/films` | Pubblica | Film del regista |
| POST | `/registi/{id}/films` | Admin/PowerUser | Crea film per regista |

### 9.3 Film (`/films/*`)

| Method | Endpoint | Auth | Descrizione |
|--------|----------|------|-------------|
| GET | `/films` | Pubblica | Lista film con categorie |
| GET | `/films/{id}` | Pubblica | Dettaglio film |
| POST | `/films` | Admin/PowerUser | Crea film |
| PUT | `/films/{id}` | Admin/PowerUser | Modifica film |
| DELETE | `/films/{id}` | Admin/PowerUser | Elimina film |

### 9.4 Cinema (`/cinemas/*`)

| Method | Endpoint | Auth | Descrizione |
|--------|----------|------|-------------|
| GET | `/cinemas` | Pubblica | Lista cinema |
| GET | `/cinemas/nearby?lat=&lng=` | Pubblica | Cinema vicini (distanza km) |
| GET | `/cinemas/{id}` | Pubblica | Dettaglio cinema |
| POST | `/cinemas` | Admin | Crea cinema |
| PUT | `/cinemas/{id}` | Admin | Modifica cinema |
| DELETE | `/cinemas/{id}` | Admin | Elimina cinema |

### 9.5 Sale (`/sale/*`)

| Method | Endpoint | Auth | Descrizione |
|--------|----------|------|-------------|
| GET | `/sale?cinemaId=` | Pubblica | Lista sale (filtrabile per cinema) |
| GET | `/sale/{id}` | Pubblica | Dettaglio sala |
| POST | `/sale` | Admin/PowerUser | Crea sala |
| PUT | `/sale/{id}` | Admin/PowerUser | Modifica sala |
| DELETE | `/sale/{id}` | Admin/PowerUser | Elimina sala (solo se senza proiezioni) |

### 9.6 Categorie (`/categorie/*`)

| Method | Endpoint | Auth | Descrizione |
|--------|----------|------|-------------|
| GET | `/categorie` | Pubblica | Lista categorie |
| GET | `/categorie/{id}` | Pubblica | Dettaglio categoria |
| GET | `/categorie/{id}/films` | Pubblica | Film della categoria |
| POST | `/categorie` | Admin | Crea categoria |
| PUT | `/categorie/{id}` | Admin | Modifica categoria |
| DELETE | `/categorie/{id}` | Admin | Elimina categoria |

### 9.7 Proiezioni (`/proiezioni/*`)

| Method | Endpoint | Auth | Descrizione |
|--------|----------|------|-------------|
| GET | `/proiezioni?filmId=&cinemaId=&day=` | Pubblica | Lista proiezioni con filtri |
| GET | `/proiezioni/{id}` | Pubblica | Dettaglio proiezione |
| POST | `/proiezioni` | Admin/PowerUser | Crea proiezione (con controllo overlap) |
| PUT | `/proiezioni/{id}` | Admin/PowerUser | Modifica proiezione |
| DELETE | `/proiezioni/{id}` | Admin/PowerUser | Elimina proiezione |

### 9.8 Programmazione pubblica (`/programmazione/*`)

| Method | Endpoint | Auth | Descrizione |
|--------|----------|------|-------------|
| GET | `/programmazione/shows?filmId=&cinemaId=&day=` | Pubblica | Show raggruppati per tipologia sala |
| GET | `/programmazione/films?search=&categoria=&cinemaId=` | Pubblica | Film in programmazione con ricerca/filtri |
| GET | `/programmazione/films/{filmId}?cinemaId=` | Pubblica | Dettaglio film + calendario 30 giorni |

### 9.9 My Cinemas (`/my-cinemas/*`)

| Method | Endpoint | Auth | Descrizione |
|--------|----------|------|-------------|
| GET | `/my-cinemas/tipologie` | Pubblica | Tipologie sale disponibili |
| GET | `/my-cinemas?citta=&tipologiaSala=&lat=&lng=&raggio=` | Pubblica | Ricerca cinema con filtri |
| GET | `/my-cinemas/{cinemaId}/programmazione?day=` | Pubblica | Programmazione giornaliera di un cinema |

### 9.10 Checkout e Posti (`/checkout/*`)

| Method | Endpoint | Auth | Descrizione |
|--------|----------|------|-------------|
| GET | `/checkout/seats/{proiezioneId}` | Pubblica | Mappa posti con stati (libero/bloccato/occupato) |
| POST | `/checkout/locks` | Utente | Crea/aggiorna lock posti |
| DELETE | `/checkout/locks/{proiezioneId}` | Utente | Rilascia lock posti |

### 9.11 Pagamenti (`/pagamenti/*`)

| Method | Endpoint | Auth | Descrizione |
|--------|----------|------|-------------|
| POST | `/pagamenti/checkout-session` | Utente | Crea sessione Stripe |
| GET | `/pagamenti/esito?session_id=` | Utente | Verifica esito pagamento Stripe |
| POST | `/pagamenti/conferma` | Utente | Flusso acquisto alternativo |
| POST | `/pagamenti/stripe/webhook` | Pubblica | Webhook Stripe |

### 9.12 Prenotazioni (`/prenotazioni/*`)

| Method | Endpoint | Auth | Descrizione |
|--------|----------|------|-------------|
| GET | `/prenotazioni` | Admin | Tutte le prenotazioni |
| GET | `/prenotazioni/mie` | Utente | Mie prenotazioni |
| GET | `/prenotazioni/{id}` | Utente | Dettaglio prenotazione |
| POST | `/prenotazioni` | Utente | Crea prenotazione (flusso semplice) |
| PUT | `/prenotazioni/{id}/annulla` | Utente | Annulla prenotazione |

### 9.13 Biglietti (`/tickets/*`)

| Method | Endpoint | Auth | Descrizione |
|--------|----------|------|-------------|
| GET | `/tickets/{codiceAcquisto}` | Utente | Dettaglio biglietto |
| GET | `/tickets/validate/{codiceAcquisto}` | Pubblica | Anteprima validazione (pubblica) |
| POST | `/tickets/{codiceAcquisto}/validate` | Admin/PowerUser | Valida biglietto |
| GET | `/tickets/{codiceAcquisto}/pdf` | Utente | Scarica PDF ticket |

### 9.14 TMDB (`/tmdb/*`)

| Method | Endpoint | Auth | Descrizione |
|--------|----------|------|-------------|
| GET | `/tmdb/status` | Admin/PowerUser | Stato configurazione TMDB |
| POST | `/tmdb/sync/film/{filmId}` | Admin/PowerUser | Sync singolo film |
| POST | `/tmdb/sync/films` | Admin/PowerUser | Sync batch film |
| GET | `/tmdb/latest?limit=&page=` | Admin/PowerUser | Ultime uscite TMDB |
| GET | `/tmdb/search?title=&limit=&page=` | Admin/PowerUser | Ricerca film su TMDB |
| POST | `/tmdb/import-latest` | Admin/PowerUser | Importa film da TMDB |
| GET | `/tmdb/missing` | Admin/PowerUser | Film senza metadati TMDB |

---

## 10. Frontend — Pagine e Script

### 10.1 Pagine pubbliche

| Pagina | Descrizione | Script JS |
|--------|-------------|-----------|
| `index.html` | Home cinematografica con hero, carousel film, KPI, modale dettaglio | `home.js` |
| `programmazione.html` | Programmazione pubblica con tab, ricerca, filtri | `programmazione.js`, `programmazione-shared.js` |
| `scheda-film.html` | Dettaglio film con calendario show e trailer | `scheda-film.js`, `show-utils.js` |
| `proiezioni-pubblico.html` | Vista pubblica proiezioni | `proiezioni-pubblico.js` |
| `my-cinemas.html` | Lista cinema e dettaglio programmazione | `my-cinemas.js`, `geo-permission.js` |
| `login.html` | Login (email/password + Google/Microsoft) | `login.js`, `auth-service.js` |
| `register.html` | Registrazione (email/password + Google/Microsoft) | `register.js`, `auth-service.js` |
| `social-login-complete.html` | Callback OIDC | `callback-auth.js` |
| `recupera-password.html` | Form richiesta reset password | `auth-service.js` |
| `reimposta-password.html` | Form inserimento nuova password | `auth-service.js` |
| `setup-password.html` | Setup password per account social | `auth-service.js` |

### 10.2 Pagine autenticate

| Pagina | Ruolo | Descrizione | Script JS |
|--------|-------|-------------|-----------|
| `profile.html` | Utente+ | Profilo, sicurezza, sessioni, social login | `profile.js` |
| `acquista.html` | Utente+ | Selezione posti su mappa, lock, acquisto | `acquista.js`, `seatmap-utils.js` |
| `pagamento.html` | Utente+ | Scelta metodo pagamento, redirect Stripe | `pagamento.js` |
| `esito-pagamento.html` | Utente+ | Riepilogo acquisto, link PDF | `esito-pagamento.js` |

### 10.3 Pagine Admin/PowerUser

| Pagina | Ruolo | Descrizione | Script JS |
|--------|-------|-------------|-----------|
| `dashboard.html` | Admin/PowerUser | Dashboard amministrativa | `dashboard.js` |
| `films.html` | Admin/PowerUser | CRUD Film | `films.js` |
| `registi.html` | Admin/PowerUser | CRUD Registi | `registi.js` |
| `proiezioni.html` | Admin/PowerUser | CRUD Proiezioni | `proiezioni.js` |
| `sale.html` | Admin/PowerUser | CRUD Sale | `sale.js` |
| `categorie.html` | Admin/PowerUser | CRUD Categorie | `categorie.js` |
| `validazione-biglietti.html` | Admin/PowerUser | Validazione biglietti | `validazione-biglietti.js` |
| `tmdb-admin.html` | Admin/PowerUser | Gestione sync TMDB | `tmdb-admin.js` |

### 10.4 Pagine solo Admin

| Pagina | Ruolo | Descrizione | Script JS |
|--------|-------|-------------|-----------|
| `cinemas.html` | Admin | CRUD Cinema | `cinemas.js` |
| `utenti.html` | Admin | Gestione utenti (ricerca, filtri, azioni, inviti) | `utenti.js` |

### 10.5 Componenti condivisi

| File | Descrizione |
|------|-------------|
| `components/navbar.html` | Navbar role-aware (link variabili per ruolo) |
| `components/footer.html` | Footer comune |
| `js/template-loader.js` | Carica navbar/footer in tutte le pagine |
| `js/navbar.js` | Gestione menu, active link, mobile toggle, logout |
| `js/api-client.js` | Wrapper fetch con auth automatica, auto-refresh token, gestione errori, rate-limiting (429) |
| `js/api-config.js` | Configurazione `API_BASE_URL` |
| `js/auth-service.js` | Gestione token, login/register, social login, password management |
| `js/auth-guard.js` | Route guard: `requireAuth()`, `requireAdmin()` |
| `js/theme.js` | Toggle dark/light theme |
| `js/date-utils.js` | Utility formattazione date |
| `js/seatmap-utils.js` | Rendering mappa posti |
| `js/show-utils.js` | Visualizzazione show/calendario |
| `css/styles.css` | Design system completo (CSS custom properties, Inter/Poppins, responsive) |

---

## 11. Servizi Backend

| Servizio | Descrizione |
|----------|-------------|
| **AuthService** | Logica di business autenticazione (login, register, social, password) |
| **JwtTokenService** | Creazione e validazione JWT token |
| **PasswordService** | Hashing password (BCrypt), validazione robustezza |
| **SocialAuthService** | Flussi OIDC Google e Microsoft, scambio codici, linking account |
| **SecurityAuditService** | Registrazione eventi di sicurezza nel database |
| **EmailService** | Invio email HTML con retry policy (reset, setup, inviti, notifiche) |
| **TicketPdfService** | Generazione PDF biglietti con QuestPDF (barcode, QR, layout) |
| **TicketEmailService** | Invio email con allegato PDF biglietto |
| **TmdbService** | Integrazione API TMDB (ricerca, sync, arricchimento metadati) |
| **TmdbSyncHostedService** | Background service: sync notturna automatica TMDB |
| **CleanupHostedService** | Background service: pulizia ExternalAuthState scaduti, token scaduti |
| **SeatPricingUtils** | Calcolo prezzi base e supplemento VIP per posto |
| **GeoHelper** | Calcolo distanza geografica (formula Haversine) |

---

## 12. Avvio e Configurazione

### 12.1 Prerequisiti
- .NET SDK 9.0.306 (pinned via `global.json`)
- Docker (per MariaDB)
- Stripe CLI (opzionale, per test pagamenti in locale)

### 12.2 Configurazione `.env`

```env
# Database
DB_HOST=localhost
DB_PORT=3306
DB_NAME=film-api-db
DB_USER=root
DB_PASSWORD=root

# Admin default (creato automaticamente al primo avvio)
DEFAULT_ADMIN_EMAIL=admin@filmapi.local
DEFAULT_ADMIN_PASSWORD=Admin123!

# JWT
JWT_SECRET_KEY=your-secret-key-min-32-chars
JWT_ISSUER=FilmAPI
JWT_AUDIENCE=FilmFrontend

# Autenticazione
AUTH_ENABLED=true

# Stripe
STRIPE_SECRET_KEY=sk_test_...
STRIPE_WEBHOOK_SECRET=whsec_...
STRIPE_ENABLED=true

# TMDB
TMDB_API_READ_TOKEN=eyJhbGciOi...
TMDB_BASE_URL=https://api.themoviedb.org/3
TMDB_LANGUAGE=it-IT
TMDB_SYNC_ENABLED=true
TMDB_SYNC_HOUR=03

# Google OIDC
GOOGLE_CLIENT_ID=...
GOOGLE_CLIENT_SECRET=...

# Microsoft OIDC
MICROSOFT_CLIENT_ID=...
MICROSOFT_CLIENT_SECRET=...

# Email (SMTP)
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USER=...
SMTP_PASSWORD=...
SMTP_FROM=noreply@filmapi.local
```

### 12.3 Comandi di avvio

```bash
# 1. Avvia MariaDB
docker compose up -d

# 2. Avvia backend (da root)
dotnet run

# 3. Avvia frontend (da FilmFrontend/)
dotnet run
```

### 12.4 Resettare utenti

Impostare `RESET_USERS=true` nel `.env` per cancellare tutti gli utenti e ricreare solo l'admin al prossimo avvio.

### 12.5 Test

```bash
dotnet test tests/FilmAPI.Tests.csproj
```

### 12.6 Seed dati realistici

```bash
python scripts/seed_realistic_data.py http://localhost:5000
```

---

## 13. Riepilogo Tecnologico

| Componente | Stack |
|------------|-------|
| Backend framework | ASP.NET Core 9 Minimal API |
| ORM | Entity Framework Core 9 |
| Database | MariaDB 10.11 (Docker) |
| Provider DB | Pomelo.EntityFrameworkCore.MySql 9.0.0 |
| Auth | JWT Bearer + Google/Microsoft OIDC |
| Password hashing | BCrypt.Net |
| PDF | QuestPDF (Community) |
| Barcode | ZXing.Net |
| Immagini | SkiaSharp |
| Email | MailKit |
| Pagamenti | Stripe Checkout (Session mode) |
| API esterne | TMDB API v3 |
| Frontend | HTML5, CSS3 (custom properties), Vanilla JS |
| Font | Inter (UI), Poppins (heading) |
| Tema | Dark/Light mode via `data-theme` |
| Test | xUnit (62 test) |
| Logging | Console + EF Core sensitive data logging (dev) |

---

*Ultimo aggiornamento: 2026-05-12 — copre tutte le funzionalità delle Iterazioni 1-5.*
