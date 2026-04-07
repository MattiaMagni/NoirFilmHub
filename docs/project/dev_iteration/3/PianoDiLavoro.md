# Piano di lavoro - Iterazione 3: Autenticazione JWT, RBAC e Sistema di Prenotazioni

## Stato iniziale
- Backend FilmAPI con endpoint CRUD completi per Registi, Film, Cinema, Proiezioni
- Frontend FilmFrontend con pagine HTML per gestione catalogo
- Database MariaDB con EF Core e migrations esistenti
- Nessun sistema di autenticazione/autorizzazione

## Obiettivo
Implementare sistema completo di autenticazione JWT con refresh token, autorizzazione RBAC (Role-Based Access Control), area personale utenti, sistema di prenotazioni e categorie film.

---

## FASE 1: Setup Autenticazione Backend

### 1.1 Modelli Utente e Ruoli
**File da creare/modificare:**
- `Model/Utente.cs` - Entità utente con campi: Id, Email, PasswordHash, Nome, Cognome, Ruolo, RefreshToken, RefreshTokenExpiryTime
- `Model/Ruolo.cs` - Enum ruoli: Admin, PowerUser, Utente
- `DTOs/LoginRequestDTO.cs` - Input login (email, password)
- `DTOs/LoginResponseDTO.cs` - Output login (accessToken, refreshToken, utente info)
- `DTOs/RegisterRequestDTO.cs` - Input registrazione
- `DTOs/UtenteDTO.cs` - Output dati utente (senza password)

**Note tecniche:**
- Password salvate come hash bcrypt/argon2
- Refresh token come stringa crittografica random (64 bytes)

### 1.2 Configurazione JWT nel Backend
**File da modificare:**
- `FilmAPI.csproj` - Aggiungere pacchetti:
  - `Microsoft.AspNetCore.Authentication.JwtBearer` 9.0.x
  - `BCrypt.Net-Next` o `Isopoh.Cryptography.Argon2`

- `Program.cs`:
  - Configurare `AddAuthentication()` con JWT Bearer
  - Configurare `AddAuthorization()` con policy per ruoli
  - Aggiungere middleware `UseAuthentication()` prima di `UseAuthorization()`

**Variabili ambiente (.env):**
```
JWT_SECRET_KEY=<chiave-segreta-32-char-min>
JWT_ISSUER=FilmAPI
JWT_AUDIENCE=FilmFrontend
JWT_ACCESS_TOKEN_EXPIRY_MINUTES=15
JWT_REFRESH_TOKEN_EXPIRY_DAYS=7
```

### 1.3 Servizio Autenticazione
**File da creare:**
- `Services/AuthService.cs` - Logica business:
  - `Register()` - registrazione nuovo utente (ruolo default: Utente)
  - `Login()` - validazione credenziali, generazione token
  - `RefreshToken()` - rinnovo access token con refresh token
  - `Logout()` - invalidazione refresh token
  - `GenerateAccessToken()` - creazione JWT firmato
  - `GenerateRefreshToken()` - creazione token random

- `Services/PasswordService.cs` - Helper hash/verify password

### 1.4 Endpoint Autenticazione
**File da creare:**
- `Endpoints/AuthEndpoints.cs` - Extension method per mappare:
  - `POST /auth/register` - registrazione nuovo utente (pubblico)
  - `POST /auth/login` - login e ritorno token (pubblico)
  - `POST /auth/refresh` - rinnovo access token (pubblico, richiede refresh token valido)
  - `POST /auth/logout` - logout (richiede autenticazione)
  - `GET /auth/me` - profilo utente corrente (richiede autenticazione)
  - `PUT /auth/me` - aggiornamento profilo (richiede autenticazione)

---

## FASE 2: Modello Dati Aggiornato

### 2.1 Categorie Film (Relazione Many-to-Many)
**File da creare/modificare:**
- `Model/Categoria.cs` - Entità: Id, Nome, Descrizione
- `Model/FilmCategoria.cs` - Entità join: FilmId, CategoriaId
- Modifica `Model/Film.cs` - Aggiungere `ICollection<FilmCategoria> FilmCategorie`
- `DTOs/CategoriaDTO.cs` - Input/Output categoria

**File da modificare:**
- `Data/FilmDbContext.cs`:
  - Aggiungere `DbSet<Categoria>` e `DbSet<FilmCategoria>`
  - Configurare relazione many-to-many con Fluent API
  - Seed categorie iniziali (Fantasy, Horror, Drammatico, Commedia, Azione, Thriller, Animazione, Documentario)

- `DTOs/FilmDTO.cs` - Aggiungere `List<int> CategorieIds` per input/output

### 2.2 Prenotazioni (Area Personale)
**File da creare:**
- `Model/Prenotazione.cs` - Entità: Id, UtenteId, ProiezioneId, DataPrenotazione, NumeroPosti, Stato (Confermata, Annullata, Completata)
- `DTOs/PrenotazioneDTO.cs` - Output prenotazione con dettagli film/cinema
- `DTOs/PrenotazioneCreateDTO.cs` - Input creazione prenotazione

**File da modificare:**
- `Data/FilmDbContext.cs`:
  - Aggiungere `DbSet<Utente>` e `DbSet<Prenotazione>`
  - Configurare relazioni: Utente 1-N Prenotazione, Proiezione 1-N Prenotazione

---

## FASE 3: Protezione Endpoint Esistenti con RBAC

### 3.1 Attributi Autorizzazione
**File da modificare:**
- `Endpoints/RegistiEndpoints.cs`:
  - `GET /registi` - [AllowAnonymous]
  - `GET /registi/{id}` - [AllowAnonymous]
  - `POST /registi` - [Authorize(Roles = "Admin,PowerUser")]
  - `PUT /registi/{id}` - [Authorize(Roles = "Admin,PowerUser")]
  - `DELETE /registi/{id}` - [Authorize(Roles = "Admin,PowerUser")]

- `Endpoints/FilmEndpoints.cs`:
  - `GET /films` - [AllowAnonymous]
  - `GET /films/{id}` - [AllowAnonymous]
  - `POST /films` - [Authorize(Roles = "Admin,PowerUser")]
  - `PUT /films/{id}` - [Authorize(Roles = "Admin,PowerUser")]
  - `DELETE /films/{id}` - [Authorize(Roles = "Admin,PowerUser")]

- `Endpoints/CinemaEndpoints.cs`:
  - `GET /cinemas` - [AllowAnonymous]
  - `GET /cinemas/{id}` - [AllowAnonymous]
  - `POST /cinemas` - [Authorize(Roles = "Admin")]
  - `PUT /cinemas/{id}` - [Authorize(Roles = "Admin")]
  - `DELETE /cinemas/{id}` - [Authorize(Roles = "Admin")]

- `Endpoints/ProiezioniEndpoints.cs`:
  - `GET /proiezioni` - [AllowAnonymous]
  - `GET /proiezioni/{id}` - [AllowAnonymous]
  - `POST /proiezioni` - [Authorize(Roles = "Admin,PowerUser")]
  - `PUT /proiezioni/{id}` - [Authorize(Roles = "Admin,PowerUser")]
  - `DELETE /proiezioni/{id}` - [Authorize(Roles = "Admin,PowerUser")]

### 3.2 Endpoint Categorie (Nuovi)
**File da creare:**
- `Endpoints/CategorieEndpoints.cs`:
  - `GET /categorie` - [AllowAnonymous] - lista categorie
  - `GET /categorie/{id}` - [AllowAnonymous] - dettaglio categoria
  - `GET /categorie/{id}/films` - [AllowAnonymous] - film per categoria
  - `POST /categorie` - [Authorize(Roles = "Admin")] - creazione
  - `PUT /categorie/{id}` - [Authorize(Roles = "Admin")] - modifica
  - `DELETE /categorie/{id}` - [Authorize(Roles = "Admin")] - eliminazione

### 3.3 Endpoint Prenotazioni (Nuovi)
**File da creare:**
- `Endpoints/PrenotazioniEndpoints.cs`:
  - `GET /prenotazioni` - [Authorize(Roles = "Admin")] - tutte le prenotazioni (admin)
  - `GET /prenotazioni/mie` - [Authorize] - prenotazioni utente corrente
  - `POST /prenotazioni` - [Authorize] - creazione prenotazione (utente autenticato)
  - `PUT /prenotazioni/{id}/annulla` - [Authorize] - annulla propria prenotazione
  - `GET /prenotazioni/{id}` - [Authorize] - dettaglio prenotazione (solo propria o admin)

---

## FASE 4: Migrazioni Database

### 4.1 Creazione Migrazione
```bash
dotnet ef migrations add Iteration3_AuthAndPrenotazioni --project FilmAPI.csproj
dotnet ef database update --project FilmAPI.csproj
```

### 4.2 Seed Dati Iniziali
**File da modificare:**
- `Data/FilmDbContext.cs` - Aggiungere `HasData` per:
  - Categorie predefinite
  - Utente admin iniziale (email: admin@filmapi.com, password: da configurare in .env)

---

## FASE 5: Frontend - Autenticazione

### 5.1 Servizi JavaScript Auth
**File da creare:**
- `wwwroot/js/auth-service.js`:
  - `login(email, password)` - chiamata POST /auth/login, salvataggio token in localStorage/sessionStorage
  - `register(userData)` - chiamata POST /auth/register
  - `logout()` - chiamata POST /auth/logout, rimozione token
  - `refreshAccessToken()` - chiamata POST /auth/refresh quando token scade
  - `getCurrentUser()` - ritorna utente da token JWT decodificato
  - `isAuthenticated()` - verifica presenza token valido
  - `hasRole(role)` - verifica ruolo utente corrente
  - `getAuthHeaders()` - ritorna header Authorization con Bearer token

- `wwwroot/js/auth-guard.js`:
  - `requireAuth()` - redirect a login se non autenticato
  - `requireRole(roles[])` - redirect se ruolo insufficiente
  - `redirectIfAuthenticated()` - redirect a homepage se già logato

### 5.2 Aggiornamento API Client
**File da modificare:**
- `wwwroot/js/api-client.js`:
  - Aggiungere header Authorization automatico alle richieste
  - Gestire automaticamente il refresh del token su 401
  - Redirect a login su refresh fallito

### 5.3 Pagine Autenticazione
**File da creare:**
- `wwwroot/login.html`:
  - Form login (email, password)
  - Link a registrazione
  - Messaggi errore (401, 404, etc.)
  - Redirect after login alla pagina precedente o homepage

- `wwwroot/register.html`:
  - Form registrazione (nome, cognome, email, password, conferma password)
  - Validazione client password (min 8 char, maiuscola, numero, speciale)
  - Redirect a login dopo registrazione successo

---

## FASE 6: Frontend - Aggiornamento Navbar e UI

### 6.1 Navbar Dinamica per Ruolo
**File da modificare:**
- `wwwroot/components/navbar.html` - Aggiungere sezioni:
  - Link admin visibile solo per ruolo Admin
  - Link area personale visibile solo per utenti autenticati
  - Menu login/registrazione per utenti anonimi
  - Menu logout/profilo per utenti autenticati

- `wwwroot/js/navbar.js` - Aggiornare per:
  - Nascondere/mostrare link basato su ruolo
  - Mostrare nome utente loggato
  - Gestire logout

### 6.2 Protezione Pagine Frontend
**File da modificare (tutte le pagine HTML esistenti):**
- Aggiungere script iniziale che chiama `requireAuth()` o `requireRole()` dove necessario:
  - `index.html` - pubblico
  - `films.html` - richiede Admin o PowerUser
  - `registi.html` - richiede Admin o PowerUser
  - `cinemas.html` - richiede Admin
  - `proiezioni.html` - richiede Admin o PowerUser
  - `profile.html` - richiede autenticazione

---

## FASE 7: Frontend - Area Personale Utente

### 7.1 Pagina Profilo Utente
**File da creare/modificare:**
- `wwwroot/profile.html` - Area personale con:
  - Dati utente (nome, cognome, email) - modificabili
  - Sezione "Le mie prenotazioni" - lista prenotazioni attive
  - Possibilità annullare prenotazione
  - Storico prenotazioni passate

- `wwwroot/js/profile.js`:
  - Caricamento dati utente: GET /auth/me
  - Caricamento prenotazioni: GET /prenotazioni/mie
  - Aggiornamento profilo: PUT /auth/me
  - Annullamento prenotazione: PUT /prenotazioni/{id}/annulla

### 7.2 Pagina Proiezioni Pubblica
**File da creare:**
- `wwwroot/proiezioni-pubblico.html` - Vista pubblica proiezioni:
  - Lista proiezioni attuali (solo lettura)
  - Bottoni "Prenota" che:
    - Se utente autenticato: apre modal prenotazione
    - Se utente anonimo: redirect a login.html?redirect=/proiezioni-pubblico.html

- `wwwroot/js/proiezioni-pubblico.js`:
  - Caricamento proiezioni: GET /proiezioni
  - Form prenotazione con numero posti
  - Submit prenotazione: POST /prenotazioni (solo se autenticato)

---

## FASE 8: Frontend - Gestione Categorie

### 8.1 Aggiornamento CRUD Film
**File da modificare:**
- `wwwroot/films.html`:
  - Aggiungere sezione categorie nel form creazione/modifica
  - Checkbox multipli per selezione categorie
  - Visualizzazione categorie nella lista film

- `wwwroot/js/films.js`:
  - Caricamento categorie disponibili: GET /categorie
  - Includere categorieIds nei payload POST/PUT
  - Rendering categorie nella tabella

### 8.2 Pagina Gestione Categorie (Admin)
**File da creare:**
- `wwwroot/categorie.html` - Solo per Admin:
  - Lista categorie
  - Form creazione nuova categoria
  - Modifica/eliminazione categorie

---

## FASE 9: Frontend - Gestione Admin

### 9.1 Dashboard Admin
**File da modificare:**
- `wwwroot/index.html` (attuale dashboard):
  - Aggiungere sezione gestione utenti (solo admin)
  - Visualizzazione statistiche: numero utenti, prenotazioni, etc.

### 9.2 Gestione Utenti (Admin)
**File da creare:**
- `wwwroot/utenti.html` - Solo per Admin:
  - Lista utenti con ruolo
  - Cambio ruolo utente (Admin/PowerUser/Utente)
  - Disabilitazione utente
  - Visualizzazione prenotazioni per utente

---

## FASE 10: Sicurezza e Refinements

### 10.1 Sicurezza Frontend
- Validazione input robusta (XSS protection)
- Sanitizzazione output HTML
- CSRF protection (se necessario per form tradizionali)
- Storage sicuro token (valutare httpOnly cookie vs localStorage)

### 10.2 Gestione Errori Centralizzata
- Interceptor globale per gestione 401 (redirect login)
- Interceptor per gestione 403 (messaggio permessi insufficienti)
- Toast/notification system per feedback operazioni

### 10.3 UI/UX Polish
- Stato loading su tutte le operazioni async
- Empty states per liste vuote
- Form validation visiva
- Responsive design per area personale

---

## Schema RBAC Completo

| Endpoint | Admin | PowerUser | Utente | Anonimo |
|----------|-------|-----------|--------|---------|
| GET /auth/me | ✅ | ✅ | ✅ | ❌ |
| GET /cinemas | ✅ | ✅ | ✅ | ✅ |
| POST/PUT/DELETE /cinemas | ✅ | ❌ | ❌ | ❌ |
| GET /films | ✅ | ✅ | ✅ | ✅ |
| POST/PUT/DELETE /films | ✅ | ✅ | ❌ | ❌ |
| GET /registi | ✅ | ✅ | ✅ | ✅ |
| POST/PUT/DELETE /registi | ✅ | ✅ | ❌ | ❌ |
| GET /proiezioni | ✅ | ✅ | ✅ | ✅ |
| POST/PUT/DELETE /proiezioni | ✅ | ✅ | ❌ | ❌ |
| GET /categorie | ✅ | ✅ | ✅ | ✅ |
| POST/PUT/DELETE /categorie | ✅ | ❌ | ❌ | ❌ |
| GET /prenotazioni | ✅ | ❌ | ❌ | ❌ |
| GET /prenotazioni/mie | ✅ | ✅ | ✅ | ❌ |
| POST /prenotazioni | ✅ | ✅ | ✅ | ❌ |
| PUT /prenotazioni/{id}/annulla | ✅* | ✅* | ✅ (solo proprie) | ❌ |

*Admin e PowerUser possono annullare qualsiasi prenotazione

---

## Pagine Frontend e Permessi

| Pagina | Accesso | Note |
|--------|---------|------|
| index.html | Tutti | Homepage con proiezioni in corso |
| login.html | Anonimi | Redirect se già autenticato |
| register.html | Anonimi | Redirect se già autenticato |
| proiezioni-pubblico.html | Tutti | Vista solo lettura + prenotazione (richiede login) |
| profile.html | Autenticati | Area personale con prenotazioni |
| films.html | Admin, PowerUser | CRUD film |
| registi.html | Admin, PowerUser | CRUD registi |
| cinemas.html | Admin | CRUD cinema |
| proiezioni.html | Admin, PowerUser | CRUD proiezioni |
| categorie.html | Admin | CRUD categorie |
| utenti.html | Admin | Gestione utenti |

---

## Struttura Cartelle Backend (aggiunte)

```
/
├── Model/
│   ├── Utente.cs
│   ├── Ruolo.cs (enum)
│   ├── Categoria.cs
│   ├── FilmCategoria.cs
│   └── Prenotazione.cs
├── DTOs/
│   ├── LoginRequestDTO.cs
│   ├── LoginResponseDTO.cs
│   ├── RegisterRequestDTO.cs
│   ├── UtenteDTO.cs
│   ├── CategoriaDTO.cs
│   ├── PrenotazioneDTO.cs
│   └── PrenotazioneCreateDTO.cs
├── Services/
│   ├── AuthService.cs
│   └── PasswordService.cs
├── Endpoints/
│   ├── AuthEndpoints.cs
│   ├── CategorieEndpoints.cs
│   └── PrenotazioniEndpoints.cs
└── Data/
    └── (FilmDbContext aggiornato)
```

---

## Struttura Cartelle Frontend (aggiunte)

```
wwwroot/
├── js/
│   ├── auth-service.js
│   └── auth-guard.js
├── login.html
├── register.html
└── proiezioni-pubblico.html
```

---

## Criteri di Accettazione

1. **Autenticazione:**
   - Login funzionante con JWT access token (15 min) e refresh token (7 giorni)
   - Token refresh automatico in background quando token scade
   - Logout che invalida refresh token lato server
   - Registrazione nuovo utente con ruolo default "Utente"

2. **RBAC:**
   - Admin può fare tutto (CRUD completo su tutte le entità)
   - PowerUser può fare CRUD su Film, Registi, Proiezioni ma solo Read su Cinema
   - Utente autenticato può vedere proiezioni e fare prenotazioni
   - Utente anonimo può solo vedere proiezioni (redirect su tentativo prenotazione)

3. **Pagine protette:**
   - Utente anonimo viene rediretto a login se prova ad accedere a pagine protette
   - Utente senza permessi sufficienti vede messaggio errore appropriato
   - Navbar mostra solo link accessibili al ruolo corrente

4. **Area personale:**
   - Utente autenticato vede le proprie prenotazioni
   - Può annullare prenotazioni non completate
   - Può aggiornare i propri dati profilo

5. **Categorie:**
   - Film può avere multiple categorie
   - API supporta filtraggio film per categoria
   - Frontend permette selezione categorie in form film

6. **Prenotazioni:**
   - Solo utenti autenticati possono prenotare
   - Prenotazione include: proiezione, numero posti, data
   - Possibilità annullare prenotazione

---

## Sequenza Implementazione (WBS)

### Sprint 1: Autenticazione Backend
1. [ ] Setup pacchetti JWT e configurazione Program.cs
2. [ ] Creazione modelli Utente, Categoria, FilmCategoria, Prenotazione
3. [ ] Aggiornamento FilmDbContext con nuove entità e relazioni
4. [ ] Creazione DTOs per auth e nuove entità
5. [ ] Implementazione AuthService e PasswordService
6. [ ] Implementazione AuthEndpoints
7. [ ] Migrazione database e seed dati

### Sprint 2: Protezione API e RBAC
8. [ ] Aggiungere attributi [Authorize] agli endpoint esistenti
9. [ ] Implementazione CategorieEndpoints
10. [ ] Implementazione PrenotazioniEndpoints
11. [ ] Verifiche permessi granulari (ownership risorse)
12. [ ] Test endpoint protetti con token

### Sprint 3: Frontend Auth
13. [ ] Creazione auth-service.js con gestione token
14. [ ] Creazione auth-guard.js per protezione rotte
15. [ ] Aggiornamento api-client.js con header Authorization
16. [ ] Creazione login.html e login.js
17. [ ] Creazione register.html e register.js
18. [ ] Test flusso login/logout/registrazione

### Sprint 4: Area Personale e Prenotazioni
19. [ ] Aggiornamento navbar.html con menu dinamico
20. [ ] Aggiornamento navbar.js per stato autenticazione
21. [ ] Modifica profile.html per area personale completa
22. [ ] Implementazione profile.js (dati + prenotazioni)
23. [ ] Creazione proiezioni-pubblico.html
24. [ ] Implementazione sistema prenotazioni lato frontend

### Sprint 5: Categorie e Gestione Admin
25. [ ] Aggiornamento films.html con gestione categorie
26. [ ] Modifica films.js per salvataggio categorie
27. [ ] Creazione categorie.html (solo admin)
28. [ ] Creazione utenti.html (gestione utenti admin)
29. [ ] Verifiche finali RBAC su tutte le pagine

### Sprint 6: Testing e Refinements
30. [ ] Test E2E flussi completi per ogni ruolo
31. [ ] Validazione gestione errori
32. [ ] Ottimizzazione UX/UI
33. [ ] Documentazione API aggiornata

---

## Note Tecniche Importanti

### Storage Token
Valutare approccio ibrido:
- **Access Token**: localStorage (short-lived, 15 min)
- **Refresh Token**: httpOnly secure cookie (7 giorni, XSS-safe)

Alternativa più semplice per inizio:
- Entrambi in localStorage con consapevolezza rischio XSS

### Gestione Redirect
- Salvare URL richiesto in sessionStorage prima di redirect login
- Dopo login success, redirect all'URL salvato
- Se nessun URL salvato, redirect a homepage

### Pattern Validazione
- Backend: FluentValidation o validation attributes
- Frontend: Validazione HTML5 + JavaScript prima di submit
- Messaggi errore: localizzati in italiano

### Concorrenza Prenotazioni
- Per ora: prenotazione virtuale senza controllo posti disponibili
- Futuro: implementare controllo disponibilità posti

---

## Deliverable Iterazione 3

- `docs/project/dev_iteration/3/PianoDiLavoro.md` (questo documento)
- Backend aggiornato con autenticazione JWT e RBAC
- Nuovi endpoint: auth, categorie, prenotazioni
- Frontend con sistema login/logout completo
- Area personale utente con gestione prenotazioni
- Gestione categorie film (many-to-many)
- Pagina proiezioni pubblica con sistema prenotazione
- Protezione rotte e UI basata sui ruoli
- Migrazioni database aggiornate
- Documentazione API (Swagger aggiornato automaticamente)
