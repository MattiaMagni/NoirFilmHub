# Piano di lavoro - Iterazione 5: Identity & Security Management Enterprise-Grade

## 1) Obiettivo iterazione

Evolvere l'attuale autenticazione JWT locale verso una piattaforma completa di identity & security management enterprise-grade per "CineBase".

Il sistema finale deve supportare:
- login email/password (esistente, rafforzato)
- login social Google OpenID Connect
- login social Microsoft OpenID Connect multi-tenant
- account misti social + password (hybrid)
- cambio password autenticato
- recupero password via email (forgot/reset)
- setup password per account social-only
- gestione ruoli avanzata con regole di sicurezza
- invalidazione JWT lato backend (AuthVersion + refresh token revocabili)
- audit sicurezza completo
- gestione utenti admin (listing, filtri, ricerca, promozione/degradazione, disable/enable, inviti)
- protezione anti-abuso e hardening sicurezza

---

## 2) Stato di partenza (as-is)

### 2.1 Riepilogo sistema auth attuale (Iterazione 3)
- **Modello Utente**: `Id`, `Email`, `PasswordHash` (BCrypt), `Nome`, `Cognome`, `Telefono`, `Ruolo` (stringa), `CinemaPreferitoId`, `RefreshToken`, `RefreshTokenExpiryTime`
- **Ruoli**: `admin`, `power_user`, `utente` (classe statica `RuoloUtente`)
- **Token**: JWT HMAC-SHA256 con claims `sub`, `email`, `name`, `role`; refresh token 64-byte random in DB
- **Endpoint auth esistenti**: register, login, refresh, logout, me, update profile, cinema preferito, list users (admin), change role (admin), delete user (admin)
- **Frontend**: login.html, register.html, profile.html, utenti.html; auth-service.js con localStorage; auth-guard.js; api-client.js con auto-refresh
- **Seed**: unico admin creato a runtime da variabili ambiente
- **Configurazione JWT**: valori hardcoded in codice (non in `.env`)

### 2.2 Lacune principali vs requisiti Iterazione 5

| Dominio | Mancante |
|---------|----------|
| Social login | Nessun supporto Google/Microsoft OIDC |
| Password management | Nessun forgot/reset, nessun cambio password autenticato, nessun setup password per social |
| JWT hardening | Nessun AuthVersion, nessuna invalidazione forzata, nessun controllo OnTokenValidated |
| Modello dati auth | Nessun `NormalizedEmail`, `LocalCredentialsEnabled`, `IsDisabled`, `LastLoginAtUtc`; `PasswordHash` not nullable |
| Audit sicurezza | Nessuna entità di audit log per eventi auth |
| Rate limiting | Non implementato su endpoint `/auth/*` |
| Email infrastruttura | Nessun servizio email dedicato per auth |
| Admin avanzato | Nessun filtro/ricerca avanzata, nessun invito, nessun disable/enable utente |
| Sicurezza frontend | Refresh token in localStorage, nessuna protezione CSRF, nessun Content Security Policy |
| Test auth | Nessun file di test per autenticazione/RBAC |

---

## 3) Evoluzione modello dati (to-be)

### 3.1 Estensione entità `Utente`

Campi da aggiungere/modificare:

| Campo | Tipo | Vincoli | Note |
|-------|------|---------|------|
| `NormalizedEmail` | `string(256)` | Index unique | Email uppercase per lookup case-insensitive e anti-duplicazione |
| `LocalCredentialsEnabled` | `bool` | Default `true` | `false` per account social-only |
| `PasswordHash` | `string?` | Modificato in nullable | `null` per account social-only |
| `AuthVersion` | `int` | Default `1` | Incrementato ad ogni evento di invalidazione (cambio password, reset, modifica ruolo, disable) |
| `LastLoginAtUtc` | `DateTime?` | Nullable | Timestamp ultimo login riuscito |
| `LastLoginProvider` | `string(32)?` | Nullable | `"local"`, `"google"`, `"microsoft"` |
| `IsDisabled` | `bool` | Default `false` | Disabilitazione account (no login possibile) |
| `SecurityStamp` | `string(64)` | | Valore random generato alla creazione, rinnovato a eventi critici |
| `EmailVerified` | `bool` | Default `false` | Email verificata (via provider social o verifica manuale) |
| `CreatedAtUtc` | `DateTime` | Default `UtcNow` | Data creazione account |
| `CreditoPiattaforma` | `decimal(10,2)` | Default `0` | (differito da Iterazione 4, qui incluso per completezza) |

Modifiche vincoli e indici:
- `NormalizedEmail`: unique index (`IX_Utenti_NormalizedEmail`)
- `Email`: unique index (esistente, da mantenere)
- `IsDisabled`: filtered index per query amministrative
- `Ruolo`: index per filtri listing admin

### 3.2 Nuova entità: `UserExternalLogin`

| Campo | Tipo | Vincoli | Note |
|-------|------|---------|------|
| `Id` | `int` | PK, autoincrement | |
| `UtenteId` | `int` | FK -> Utente.Id, required | |
| `Provider` | `string(32)` | Required | `"google"` o `"microsoft"` |
| `ProviderKey` | `string(256)` | Required | `sub` claim (OIDC) |
| `ProviderDisplayName` | `string(256)?` | Nullable | Nome restituito dal provider |
| `TenantId` | `string(128)?` | Nullable | Per Microsoft: `tid` claim (solo tenant aziendali) |
| `Email` | `string(256)` | Required | Email associata al provider |
| `CreatedAtUtc` | `DateTime` | Default `UtcNow` | |

Vincoli:
- Unique index: `(Provider, ProviderKey)` -- impedisce doppio linking stesso account social
- Unique index: `(Provider, TenantId, ProviderKey)` -- per Microsoft multi-tenant, identificazione stabile
- FK `UtenteId` con cascade delete (se utente eliminato, cancella login esterni)

### 3.3 Nuova entità: `AccountActionToken`

Token single-use per:
- reset password
- setup password (account social)
- invito admin/poweruser
- verifica email

| Campo | Tipo | Vincoli | Note |
|-------|------|---------|------|
| `Id` | `int` | PK, autoincrement | |
| `UtenteId` | `int` | FK -> Utente.Id, required | |
| `TokenHash` | `string(128)` | Required, unique index | SHA256 del token (il token in chiaro viene inviato via email e mai salvato) |
| `TokenType` | `string(32)` | Required | `"PasswordReset"`, `"PasswordSetup"`, `"AdminInvite"`, `"EmailVerification"` |
| `ExpiresAtUtc` | `DateTime` | Required | TTL configurabile per tipo (es. 1h reset, 72h invite) |
| `ConsumedAtUtc` | `DateTime?` | Nullable | Data consumo (single-use enforcement) |
| `CreatedAtUtc` | `DateTime` | Default `UtcNow` | |

Vincoli:
- Unique index su `TokenHash`
- Check constraint: `ConsumedAtUtc IS NULL OR ConsumedAtUtc > CreatedAtUtc`
- FK `UtenteId` con cascade delete

### 3.4 Nuova entità: `ExternalAuthState`

Gestione stato OIDC per prevenire CSRF negli exchange code.

| Campo | Tipo | Vincoli | Note |
|-------|------|---------|------|
| `Id` | `string(128)` | PK (GUID) | State parameter OIDC |
| `ReturnUrl` | `string(512)?` | Nullable | URL di ritorno post-login (validato anti open-redirect) |
| `Provider` | `string(32)` | Required | `"google"` o `"microsoft"` |
| `CreatedAtUtc` | `DateTime` | Default `UtcNow` | |
| `ExpiresAtUtc` | `DateTime` | Required | TTL breve (10 minuti) |

Vincoli:
- Cleanup job: rimuove record scaduti ogni 5 minuti

### 3.5 Nuova entità: `ExternalAuthExchangeCode`

Tracciamento one-time exchange code (PKCE/authorization code flow) per prevenire replay.

| Campo | Tipo | Vincoli | Note |
|-------|------|---------|------|
| `Id` | `int` | PK, autoincrement | |
| `CodeHash` | `string(128)` | Required, unique index | SHA256 del code scambiato |
| `StateId` | `string(128)` | FK -> ExternalAuthState.Id | |
| `ConsumedAtUtc` | `DateTime?` | Nullable | |
| `CreatedAtUtc` | `DateTime` | Default `UtcNow` | |

### 3.6 Nuova entità: `UserSecurityAuditLog`

Tracciamento completo eventi di sicurezza.

| Campo | Tipo | Vincoli | Note |
|-------|------|---------|------|
| `Id` | `long` | PK, autoincrement (bigint) | Volume potenzialmente alto |
| `UtenteId` | `int?` | FK -> Utente.Id, nullable | NULL per eventi anonimi (es. login fallito senza utente noto) |
| `EventType` | `string(64)` | Required, index | `"LoginSuccess"`, `"LoginFailed"`, `"PasswordChanged"`, `"PasswordReset"`, `"SocialLinked"`, `"RoleChanged"`, `"AccountDisabled"`, `"AccountEnabled"`, `"TokenRefreshed"`, `"Logout"`, `"AccountDeleted"` |
| `Provider` | `string(32)?` | Nullable | `"local"`, `"google"`, `"microsoft"` |
| `IpAddress` | `string(64)?` | Nullable | IP richiedente |
| `UserAgent` | `string(512)?` | Nullable | User-Agent header |
| `Details` | `string(1024)?` | Nullable | Dettagli aggiuntivi JSON |
| `CreatedAtUtc` | `DateTime` | Default `UtcNow`, index | |

Indici:
- `IX_AuditLog_UtenteId_CreatedAtUtc` per query per utente
- `IX_AuditLog_EventType_CreatedAtUtc` per query per tipo evento
- `IX_AuditLog_CreatedAtUtc` per cleanup periodico

Strategia cleanup:
- Job settimanale: cancella record piu vecchi di 90 giorni (configurabile)
- Record di eventi critici (`RoleChanged`, `AccountDeleted`) conservati per 365 giorni

### 3.7 Riepilogo schema completo auth Iterazione 5

```
Utente (estensione)
  ├── 1:N → UserExternalLogin
  ├── 1:N → AccountActionToken
  ├── 1:N → UserSecurityAuditLog
  └── 1:N → Prenotazione (esistente)

ExternalAuthState (standalone, TTL breve)
  └── 1:N → ExternalAuthExchangeCode
```

---

## 4) Social Login (Google + Microsoft OpenID Connect)

### 4.1 Architettura generale

Il flusso OIDC segue il pattern Authorization Code + PKCE lato server:

1. **Frontend**: pulsanti "Accedi con Google" / "Accedi con Microsoft" su `login.html` e `register.html`
2. **Backend**: genera URL di autorizzazione con state parameter (GUID salvato in `ExternalAuthState`)
3. **Redirect**: utente va al provider (Google/Microsoft), autorizza
4. **Callback**: provider reindirizza a `/auth/external/callback?code=xxx&state=yyy`
5. **Backend**: valida state, scambia code per token, valida id_token, mappa claims, crea/linka utente
6. **Redirect finale**: frontend riceve access+refresh token e reindirizza a pagina destinazione

### 4.2 Endpoint social login

#### `GET /auth/external/{provider}`
- Provider: `google` o `microsoft`
- Query params:
  - `returnUrl` (opzionale, validato anti open-redirect)
  - `mode` = `"login"` o `"link"` (link per utente gia autenticato che vuole collegare account social)
- Azione:
  - Genera state GUID
  - Salva `ExternalAuthState(Id=state, ReturnUrl, Provider, ExpiresAtUtc=+10min)`
  - Costruisce URL OIDC verso Google o Microsoft
  - Restituisce `{ "authorizationUrl": "..." }` al frontend
- Frontend: `window.location.href = authorizationUrl`

#### `GET /auth/external/callback?code=&state=`
- Azione:
  1. Valida state: cerca `ExternalAuthState` per Id=state, verifica non scaduto
  2. Verifica code non sia gia stato usato (controlla hash in `ExternalAuthExchangeCode`)
  3. Scambia code per token al provider (POST token endpoint)
  4. Valida `id_token`: firma, issuer, audience, expiry, nonce (se usato)
  5. Estrae claims: `sub`, `email`, `email_verified`, `name`, `picture` (+ `tid` per Microsoft)
  6. Salva code hash in `ExternalAuthExchangeCode` (anti-replay)
  7. Determina azione:
     - **Mode=login**: cerca utente esistente via `UserExternalLogin(Provider, ProviderKey)` oppure via email
     - **Mode=link**: utente gia autenticato (da JWT), collega nuovo provider social
  8. Crea/linka account secondo regole sezione 4.3
  9. Genera access+refresh token
  10. Registra `UserSecurityAuditLog` evento
  11. Redirect a frontend `social-login-complete.html` con token in URL fragment

### 4.3 Regole di linking utenti

#### Login (mode=login)
1. **Match per `UserExternalLogin(Provider, ProviderKey)`**: login diretto, genera token, OK.
2. **Match per email (stessa email, nessun external login)**: linking automatico SOLO se `Ruolo == utente`.
   - Se utente e `LocalCredentialsEnabled == false`: aggiungi record `UserExternalLogin`.
   - Se utente ha password locale: aggiungi record `UserExternalLogin`, account diventa hybrid.
3. **Nessun match**: creazione automatica nuovo utente:
   - `Email` = email provider
   - `NormalizedEmail` = UPPER(email)
   - `Nome`/`Cognome` = da claims `given_name`/`family_name` o split `name`
   - `LocalCredentialsEnabled` = `false`
   - `PasswordHash` = `NULL`
   - `Ruolo` = `utente`
   - `EmailVerified` = `true` (il provider ha gia verificato)
   - `AuthVersion` = `1`
   - Crea record `UserExternalLogin`

#### Vincoli di sicurezza linking
- **PowerUser e Admin NON sono autenticabili via social**. Se l'email matcha un utente con ruolo `admin` o `power_user`, il login social viene rifiutato con errore: "Questo account richiede autenticazione locale. Usa email e password."
- **Social-only non promuovibile** a PowerUser/Admin (vedi sezione 7).
- **Linking manuale** (mode=link): solo utenti autenticati possono collegare provider aggiuntivi al proprio account.

### 4.4 Configurazione Google OIDC

Variabili ambiente:
```
GOOGLE_CLIENT_ID=<client-id>
GOOGLE_CLIENT_SECRET=<client-secret>
GOOGLE_AUTHORITY=https://accounts.google.com
```

Endpoint:
- Authorization: `https://accounts.google.com/o/oauth2/v2/auth`
- Token: `https://oauth2.googleapis.com/token`
- JWKS: `https://www.googleapis.com/oauth2/v3/certs`

Claims mappati:
- `sub` → `UserExternalLogin.ProviderKey`
- `email` → `Utente.Email`
- `email_verified` → deve essere `true`
- `given_name` → `Utente.Nome`
- `family_name` → `Utente.Cognome`
- `picture` → non persistito (opzionale)

Validazioni aggiuntive:
- `hd` claim (hosted domain): opzionalmente configurabile per restringere a domini specifici (es. solo `@azienda.it`)

### 4.5 Configurazione Microsoft OIDC multi-tenant

Variabili ambiente:
```
MICROSOFT_CLIENT_ID=<client-id>
MICROSOFT_CLIENT_SECRET=<client-secret>
MICROSOFT_AUTHORITY=https://login.microsoftonline.com/common
```

Note multi-tenant:
- Authority: `/common` per supportare account personali e aziendali
- Identificazione stabile: `tid` (tenant ID) + `oid` (object ID) combinati
  - Per account personali Microsoft: `tid` e `9188040d-6c67-4c5b-b112-36a304b66dad`, `oid` e univoco
  - Per account aziendali: `tid` identifica il tenant, `oid` identifica l'utente nel tenant
- Salvare `tid` in `UserExternalLogin.TenantId`, `oid` in `UserExternalLogin.ProviderKey`
- Unique index `(Provider, TenantId, ProviderKey)` garantisce unicita cross-tenant

Claims mappati:
- `oid` → `UserExternalLogin.ProviderKey`
- `tid` → `UserExternalLogin.TenantId`
- `email` → `Utente.Email` (o `upn` o `preferred_username` come fallback)
- `given_name` → `Utente.Nome`
- `family_name` → `Utente.Cognome`
- `name` → `UserExternalLogin.ProviderDisplayName`

### 4.6 Gestione errori provider

| Scenario | HTTP | Comportamento |
|----------|------|---------------|
| State non valido/scaduto | 400 | Messaggio: "Sessione di login scaduta. Riprova." |
| Code gia usato (replay) | 400 | Messaggio: "Codice di autorizzazione non valido." + audit alert |
| Token provider non valido | 502 | Messaggio: "Errore di comunicazione con il provider. Riprova." + log dettagliato |
| Email non verificata dal provider | 400 | Messaggio: "Email non verificata dal provider." |
| Account PowerUser/Admin tenta social | 403 | Messaggio: "Questo account richiede autenticazione locale." |
| Provider non configurato | 500 | Log server, messaggio generico all'utente |

### 4.7 Anti open-redirect su callback

- `ExternalAuthState.ReturnUrl` validato prima del redirect finale
- Whitelist domini consentiti: solo URL relativi (`/...`) o stesso host dell'applicazione
- Implementare validatore: `IsValidReturnUrl(url)` che verifica:
  - URL relativo (inizia con `/`)
  - URL assoluto con stesso host/port dell'app
  - Blocca redirect a domini esterni

### 4.8 Logout/revoca provider

- Il logout locale invalida solo i token CineBase
- Non viene effettuata revoca lato provider (Google/Microsoft) di default
- Opzionale: revoca token provider se configurato (richiede chiamata API aggiuntiva a Google/Microsoft)
- L'utente puo scollegare un provider social dal proprio account (endpoint `DELETE /auth/me/external-logins/{id}`)

---

## 5) Password Management

### 5.1 Cambio password autenticato

Endpoint: `POST /auth/me/change-password`

DTO input:
```json
{
  "currentPassword": "string",
  "newPassword": "string"
}
```

Validazioni:
- `currentPassword` obbligatoria, verificata contro hash esistente
- `newPassword`: min 8 caratteri, almeno 1 maiuscola, 1 minuscola, 1 numero, 1 carattere speciale
- `newPassword` != `currentPassword`
- Utente deve avere `LocalCredentialsEnabled == true` (altrimenti 400: "Account social-only. Imposta prima una password.")

Azione:
1. Verifica password corrente (BCrypt verify)
2. Hash nuova password
3. Aggiorna `PasswordHash`, `SecurityStamp`, `PasswordChangedAtUtc = UtcNow`
4. Incrementa `AuthVersion += 1`
5. Invalida tutti i refresh token esistenti (setta `RefreshToken = NULL`, `RefreshTokenExpiryTime = NULL`)
6. Registra audit: `EventType = "PasswordChanged"`
7. Restituisce nuovi access+refresh token (quelli vecchi sono invalidati da `AuthVersion`)

### 5.2 Forgot password

Endpoint: `POST /auth/forgot-password`

DTO input:
```json
{
  "email": "string"
}
```

Validazioni:
- Rate limiting: max 3 richieste per IP ogni 15 minuti
- Rate limiting: max 5 richieste per email ogni ora

Azione (anti-enumerazione email):
1. Cerca utente per `NormalizedEmail = UPPER(email)` E `LocalCredentialsEnabled == true`
2. **SEMPRE** restituisce `200 OK` con messaggio: "Se l'email e associata a un account, riceverai un link di recupero."
3. Se utente trovato:
   - Genera token crittografico random (64 bytes)
   - Hash SHA256 del token → `AccountActionToken.TokenHash`
   - Salva `AccountActionToken` con `TokenType = "PasswordReset"`, `ExpiresAtUtc = +1 ora`
   - Invia email con link: `{BASE_URL}/reimposta-password.html?token={token_raw}&email={email_encoded}`
   - Il token RAW viene inviato via email e MAI salvato nel DB (solo hash)
4. Registra audit (senza rivelare se l'utente esiste): log della richiesta

### 5.3 Reset password

Endpoint: `POST /auth/reset-password`

DTO input:
```json
{
  "email": "string",
  "token": "string (raw)",
  "newPassword": "string"
}
```

Azione:
1. Cerca utente per `NormalizedEmail = UPPER(email)` e `LocalCredentialsEnabled == true`
2. Hash del token ricevuto: `SHA256(token_raw)`
3. Cerca `AccountActionToken` con `TokenHash` corrispondente, `TokenType = "PasswordReset"`, `ConsumedAtUtc IS NULL`, `ExpiresAtUtc > UtcNow`
4. Se non trovato: 400 "Token non valido o scaduto."
5. Marca token come consumato: `ConsumedAtUtc = UtcNow`
6. Hash nuova password, aggiorna `PasswordHash`, `SecurityStamp`, `PasswordChangedAtUtc`
7. Incrementa `AuthVersion += 1`
8. Invalida tutti i refresh token
9. Registra audit: `EventType = "PasswordReset"`
10. Restituisce 200 con nuovi access+refresh token

### 5.4 Setup password per account social-only

Endpoint: `POST /auth/me/setup-password` (richiede autenticazione)

DTO input:
```json
{
  "newPassword": "string"
}
```

Validazioni:
- Utente autenticato deve avere `LocalCredentialsEnabled == false` (altrimenti 400: "Hai gia una password.")
- `newPassword` come validazioni cambio password

Flusso alternativo (non autenticato, via email):
1. Utente social-only clicca "Imposta password" su `profile.html`
2. Richiama `POST /auth/me/request-password-setup` (con JWT)
3. Backend crea `AccountActionToken` con `TokenType = "PasswordSetup"`
4. Invia email con link di setup (simile a forgot password)
5. Utente clicca link, arriva su `setup-password.html?token=...&email=...`
6. Frontend chiama `POST /auth/setup-password` (senza JWT, validato dal token)
7. Backend valida token, imposta password, attiva `LocalCredentialsEnabled = true`

Endpoint per setup via token email: `POST /auth/setup-password`

DTO input:
```json
{
  "email": "string",
  "token": "string (raw)",
  "newPassword": "string"
}
```

Azione:
1. Stessa logica di validazione token del reset password (single-use, TTL, hash matching)
2. Aggiorna `PasswordHash`, `LocalCredentialsEnabled = true`
3. Registra audit: `EventType = "PasswordSetup"`
4. Restituisce nuovi token

### 5.5 Riepilogo endpoint password management

| Endpoint | Auth | Rate Limit | Note |
|----------|------|------------|------|
| `POST /auth/me/change-password` | Authenticated | 5/min per utente | Richiede password corrente |
| `POST /auth/forgot-password` | Anonymous | 3/15min per IP, 5/h per email | Anti-enumerazione email |
| `POST /auth/reset-password` | Anonymous | 5/15min per IP | Token single-use email |
| `POST /auth/me/request-password-setup` | Authenticated | 3/h per utente | Per account social-only |
| `POST /auth/setup-password` | Anonymous | 5/15min per IP | Token single-use email |

### 5.6 Configurazione TTL token

Variabili ambiente:
```
ACCOUNT_TOKEN_PASSWORD_RESET_TTL_MINUTES=60
ACCOUNT_TOKEN_PASSWORD_SETUP_TTL_MINUTES=1440
ACCOUNT_TOKEN_ADMIN_INVITE_TTL_HOURS=72
ACCOUNT_TOKEN_EMAIL_VERIFICATION_TTL_HOURS=24
```

---

## 6) JWT & Session Security

### 6.1 Evoluzione schema JWT token

Claims attuali da mantenere + nuovi:

| Claim | Tipo | Note |
|-------|------|------|
| `sub` | user ID | Esistente |
| `email` | email | Esistente |
| `name` | nome cognome | Esistente |
| `role` | ClaimTypes.Role | Esistente |
| `auth_version` | int | NUOVO: per invalidazione globale |
| `security_stamp` | string | NUOVO: per invalidazione selettiva |
| `iat` | issued at | Esistente |
| `exp` | expiration | Esistente |
| `iss` | issuer | Esistente |
| `aud` | audience | Esistente |

### 6.2 AuthVersion: invalidazione globale token

Meccanismo:
1. Ogni utente ha `AuthVersion` (int, default 1).
2. JWT contiene claim `auth_version` = `Utente.AuthVersion` al momento dell'emissione.
3. Middleware `OnTokenValidated`:
   - Estrae `sub` e `auth_version` dal token
   - Carica utente dal DB (o cache)
   - Se `token.auth_version != utente.AuthVersion` → token invalidato → 401
   - Se `utente.IsDisabled` → token rifiutato → 401
   - Se `utente.PasswordChangedAtUtc > token.iat` → token invalidato → 401
4. Eventi che incrementano `AuthVersion`:
   - Cambio password
   - Reset password
   - Setup password
   - Modifica ruolo (promozione/degradazione)
   - Disabilitazione account
   - Revoca amministrativa forzata

### 6.3 OnTokenValidated middleware

File: `Middleware/TokenValidationMiddleware.cs` (o inline in `Program.cs`)

```csharp
options.Events = new JwtBearerEvents
{
    OnTokenValidated = async context =>
    {
        var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var authVersionClaim = context.Principal?.FindFirst("auth_version")?.Value;
        
        if (userIdClaim == null || authVersionClaim == null)
        {
            context.Fail("Token claims incompleti");
            return;
        }
        
        var dbContext = context.HttpContext.RequestServices.GetRequiredService<FilmDbContext>();
        var utente = await dbContext.Utenti
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == int.Parse(userIdClaim));
        
        if (utente == null)
        {
            context.Fail("Utente non trovato");
            return;
        }
        
        if (utente.IsDisabled)
        {
            context.Fail("Account disabilitato");
            return;
        }
        
        if (utente.AuthVersion.ToString() != authVersionClaim)
        {
            context.Fail("Token invalidato - sessione scaduta");
            return;
        }
        
        // Opzionale: verifica PasswordChangedAtUtc vs iat
        var iatClaim = context.Principal?.FindFirst(JwtRegisteredClaimNames.Iat)?.Value;
        if (iatClaim != null && utente.PasswordChangedAtUtc.HasValue)
        {
            var iat = DateTimeOffset.FromUnixTimeSeconds(long.Parse(iatClaim)).UtcDateTime;
            if (utente.PasswordChangedAtUtc.Value > iat)
            {
                context.Fail("Password cambiata - token invalidato");
                return;
            }
        }
    }
};
```

### 6.4 Refresh token revocabili

Evoluzione rispetto al sistema attuale:

Funzionalita attuali (da mantenere):
- Refresh token 64-byte random salvato in DB
- Rotazione ad ogni refresh
- Scadenza configurabile (7 giorni default)

Nuove funzionalita:
- **Revoca globale**: tutte le sessioni utente vengono invalidate:
  - `Utente.RefreshToken = NULL`
  - `Utente.RefreshTokenExpiryTime = NULL`
  - `Utente.AuthVersion += 1`
- **Revoca selettiva** (futuro): salvare refresh token multipli per dispositivo, revocare singolarmente
- **Cleanup automatico**: job periodico che cancella refresh token scaduti (`RefreshTokenExpiryTime < UtcNow`)

Endpoint revoca (nuovo):
- `POST /auth/revoke-all-sessions` (authenticated): revoca tutte le sessioni tranne quella corrente (opzionale: revoca anche corrente se richiesto)
- Azione: `AuthVersion += 1`, `RefreshToken = NULL`

### 6.5 Logout globale

Endpoint esistente `POST /auth/logout`:
- Attuale: invalida solo refresh token corrente
- Evoluzione: opzionale parametro `allDevices=true`:
  - `allDevices=false` (default): comportamento attuale (invalida refresh corrente)
  - `allDevices=true`: incrementa `AuthVersion`, invalida tutti i refresh token

### 6.6 Lifecycle JWT

```
[Creazione] → Access Token (15 min) + Refresh Token (7 giorni)
     ↓
[Uso normale] → Access token usato per richieste API
     ↓
[Access token scade] → Frontend chiama /auth/refresh col refresh token
     ↓
[Refresh riuscito] → Nuovo Access Token + Rotazione Refresh Token
     ↓
[Refresh fallito] → 401, redirect a login
     ↓
[Evento invalidante] → AuthVersion++, Refresh Token NULL
     ↓
[Tutti i token] → Invalidati al prossimo OnTokenValidated check
```

### 6.7 Gestione replay protection

- `ExternalAuthExchangeCode` garantisce che un authorization code OIDC non venga usato piu di una volta
- `AccountActionToken.ConsumedAtUtc` garantisce single-use per token email
- `jti` claim (JWT ID): opzionale, per tracciamento univoco token (non implementato in questa iterazione, differibile)

---

## 7) Admin & RBAC Avanzato

### 7.1 Evoluzione endpoint gestione utenti

Sostituire l'attuale `GET /auth/utenti` e `PUT /auth/utenti/{id}/ruolo` con interfaccia avanzata.

#### `GET /auth/admin/utenti`
- Auth: AdminOnly
- Query params:
  - `search` (string): ricerca per email, nome, cognome
  - `ruolo` (string, opzionale): filtra per ruolo (`admin`, `power_user`, `utente`)
  - `isDisabled` (bool?, opzionale): filtra per stato abilitazione
  - `hasLocalCredentials` (bool?, opzionale): filtra social-only vs local/hybrid
  - `page` (int, default 1)
  - `pageSize` (int, default 20, max 100)
  - `orderBy` (string, default `id`): `id`, `email`, `nome`, `createdAtUtc`, `lastLoginAtUtc`
  - `orderDirection` (string, default `asc`): `asc` o `desc`
- Output paginato:
```json
{
  "items": [ UtenteAdminDTO ],
  "totalCount": 150,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8
}
```

`UtenteAdminDTO`:
```json
{
  "id": 1,
  "email": "mario.rossi@email.it",
  "nome": "Mario",
  "cognome": "Rossi",
  "ruolo": "utente",
  "isDisabled": false,
  "emailVerified": true,
  "localCredentialsEnabled": true,
  "lastLoginAtUtc": "2026-05-01T10:30:00Z",
  "createdAtUtc": "2026-01-15T08:00:00Z",
  "externalLogins": ["google"],
  "hasPassword": true,
  "authVersion": 3
}
```

#### `GET /auth/admin/utenti/{id}`
- Auth: AdminOnly
- Output: `UtenteAdminDetailDTO` con tutte le info di sicurezza:
  - Dati profilo
  - Stato account (abilitato, email verificata, provider social collegati, versione auth)
  - Timestamp: creato, ultimo login, ultimo cambio password
  - Numero refresh token attivi
  - Lista `UserExternalLogin` con provider e date collegamento
  - Ultimi 20 audit log eventi

#### `PUT /auth/admin/utenti/{id}/ruolo`
- Auth: AdminOnly
- Input: `{ "ruolo": "power_user" }`
- Regole di sicurezza:
  1. **Ultimo admin non degradabile**: se `ruolo` != `"admin"` e l'utente e l'unico admin rimasto → 400 "Impossibile degradare l'ultimo amministratore."
  2. **Social-only non promuovibile**: se l'utente ha `LocalCredentialsEnabled == false` e il nuovo ruolo e `admin` o `power_user` → 400 "Account social-only non promuovibile. L'utente deve prima impostare una password."
  3. **Self-degradazione bloccata**: il proprio ruolo non puo essere modificato da se stessi → 400 "Non puoi modificare il tuo ruolo."
  4. **Promozione solo admin**: solo Admin puo promuovere/degradare
- Azione:
  - Aggiorna `Ruolo`
  - Incrementa `AuthVersion += 1`
  - Invalida refresh token
  - Registra audit: `EventType = "RoleChanged"`, `Details = "Ruolo cambiato da 'utente' a 'power_user' da Admin {id}"`

#### `PUT /auth/admin/utenti/{id}/disable`
- Auth: AdminOnly
- Azione:
  - Imposta `IsDisabled = true`
  - Incrementa `AuthVersion += 1`
  - Invalida refresh token
  - Registra audit: `EventType = "AccountDisabled"`

#### `PUT /auth/admin/utenti/{id}/enable`
- Auth: AdminOnly
- Azione:
  - Imposta `IsDisabled = false`
  - Incrementa `AuthVersion += 1`
  - Registra audit: `EventType = "AccountEnabled"`
  - NOTA: non ripristina refresh token

#### `POST /auth/admin/utenti/{id}/force-password-reset`
- Auth: AdminOnly
- Azione:
  - Incrementa `AuthVersion += 1`
  - Invalida refresh token
  - Crea `AccountActionToken` per reset password
  - Invia email di reset forzato all'utente
  - Registra audit: `EventType = "PasswordResetForced"`

#### `DELETE /auth/admin/utenti/{id}`
- Esistente, esteso:
  - Blocca eliminazione ultimo admin
  - Soft-delete o hard-delete con cascade su tutte le tabelle collegate
  - Registra audit dettagliato prima della cancellazione

### 7.2 Inviti admin/poweruser

Endpoint: `POST /auth/admin/invite`
- Auth: AdminOnly
- Input:
```json
{
  "email": "nuovo.poweruser@email.it",
  "ruolo": "power_user",
  "nome": "Nuovo",
  "cognome": "Operatore",
  "sendSetupEmail": true
}
```
- Azione:
  1. Verifica che l'email non sia gia registrata
  2. Crea utente con `PasswordHash = NULL`, `LocalCredentialsEnabled = false`, `IsDisabled = true` (attivo solo dopo setup password)
  3. Crea `AccountActionToken` con `TokenType = "AdminInvite"`
  4. Invia email di invito con link di setup password
  5. Registra audit: `EventType = "AdminInvite"`
- L'utente riceve email con link `{BASE_URL}/setup-password.html?token=...&email=...`
- Dopo setup password:
  - `LocalCredentialsEnabled = true`
  - `IsDisabled = false`
  - `EmailVerified = true`

### 7.3 Policy autorizzative

Policy definite in `Program.cs` (da mantenere + estendere):

```csharp
// Esistenti
options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
options.AddPolicy("AdminOrPowerUser", policy => policy.RequireRole("admin", "power_user"));

// Nuove
options.AddPolicy("AuthenticatedOnly", policy => policy.RequireAuthenticatedUser());
options.AddPolicy("NotDisabled", policy => policy.RequireAssertion(context =>
    context.User.HasClaim(c => c.Type == "is_disabled" && c.Value == "false")));
```

### 7.4 Middleware RBAC per handlers

Creare helper per validazioni ricorrenti:
- `RequireNotDisabled(Utente utente)` - verifica account attivo
- `RequireLocalCredentials(Utente utente)` - verifica che abbia password locale
- `RequireNotLastAdmin(Utente utente, FilmDbContext db)` - verifica non sia ultimo admin (per degradazione)
- `RequireNotSelf(int currentUserId, int targetUserId)` - verifica non stia operando su se stesso (per operazioni critiche)

### 7.5 Matrice RBAC completa Iterazione 5

| Endpoint | Admin | PowerUser | Utente | Anonimo |
|----------|-------|-----------|--------|---------|
| `POST /auth/register` | ✅ | ✅ | ✅ | ✅ |
| `POST /auth/login` | ✅ | ✅ | ✅ | ✅ |
| `POST /auth/refresh` | ✅ | ✅ | ✅ | ✅ |
| `POST /auth/logout` | ✅ | ✅ | ✅ | ❌ |
| `POST /auth/revoke-all-sessions` | ✅ | ✅ | ✅ | ❌ |
| `GET /auth/me` | ✅ | ✅ | ✅ | ❌ |
| `PUT /auth/me` | ✅ | ✅ | ✅ | ❌ |
| `GET /auth/me/cinema-preferito` | ✅ | ✅ | ✅ | ❌ |
| `PUT /auth/me/cinema-preferito` | ✅ | ✅ | ✅ | ❌ |
| `POST /auth/me/change-password` | ✅ | ✅ | ✅ | ❌ |
| `POST /auth/me/setup-password` | ✅ | ✅ | ✅ | ❌ |
| `POST /auth/me/request-password-setup` | ✅ | ✅ | ✅ | ❌ |
| `DELETE /auth/me/external-logins/{id}` | ✅ | ✅ | ✅ | ❌ |
| `POST /auth/forgot-password` | ✅ | ✅ | ✅ | ✅ |
| `POST /auth/reset-password` | ✅ | ✅ | ✅ | ✅ |
| `POST /auth/setup-password` | ✅ | ✅ | ✅ | ✅ |
| `GET /auth/external/{provider}` | ✅ | ✅ | ✅ | ✅ |
| `GET /auth/external/callback` | ✅ | ✅ | ✅ | ✅ |
| `GET /auth/admin/utenti` | ✅ | ❌ | ❌ | ❌ |
| `GET /auth/admin/utenti/{id}` | ✅ | ❌ | ❌ | ❌ |
| `PUT /auth/admin/utenti/{id}/ruolo` | ✅ | ❌ | ❌ | ❌ |
| `PUT /auth/admin/utenti/{id}/disable` | ✅ | ❌ | ❌ | ❌ |
| `PUT /auth/admin/utenti/{id}/enable` | ✅ | ❌ | ❌ | ❌ |
| `POST /auth/admin/utenti/{id}/force-password-reset` | ✅ | ❌ | ❌ | ❌ |
| `DELETE /auth/admin/utenti/{id}` | ✅ | ❌ | ❌ | ❌ |
| `POST /auth/admin/invite` | ✅ | ❌ | ❌ | ❌ |

---

## 8) Email Infrastructure

### 8.1 Servizio email dedicato

File: `Services/EmailService.cs`

Interfaccia:
```csharp
interface IEmailService
{
    Task SendPasswordResetEmail(string toEmail, string token, string nome);
    Task SendPasswordSetupEmail(string toEmail, string token, string nome);
    Task SendAdminInviteEmail(string toEmail, string token, string nome, string ruolo);
    Task SendRoleChangedEmail(string toEmail, string nome, string nuovoRuolo);
    Task SendPasswordChangedEmail(string toEmail, string nome);
    Task SendSecurityAlertEmail(string toEmail, string nome, string alertType, string details);
}
```

### 8.2 Implementazione SMTP

Variabili ambiente (alcune gia esistenti in `.env`):
```
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USERNAME=<username>
SMTP_PASSWORD=<app-password>
SMTP_FROM_EMAIL=noreply@cinebase.local
SMTP_FROM_NAME=CineBase
```

Utilizzo: `SmtpClient` o libreria `MailKit` per costruzione MIME email HTML + testo plain.

### 8.3 Template email

- Ogni template in HTML + fallback testo semplice
- Sostituzione placeholder: `{{NOME}}`, `{{TOKEN}}`, `{{LINK}}`, `{{RUOLO}}`, `{{ANNO}}`
- Stile inline CSS per compatibilita client email
- Link con tracking disabilitato (no pixel di tracciamento)
- Footer con: "Questa e un'email automatica, non rispondere. Se non hai richiesto questa azione, contatta l'amministratore."

### 8.4 Retry policy e queue

- **Retry immediato**: 3 tentativi con exponential backoff (1s, 5s, 25s)
- **Coda persistente** (opzionale, differibile): tabella `EmailQueue` per email non inviate
  - Campi: `Id`, `ToEmail`, `Subject`, `Body`, `CreatedAtUtc`, `SentAtUtc`, `RetryCount`, `LastError`, `Status` (Pending/Sent/Failed)
  - Job periodico: processa email in stato Pending
- **Circuit breaker**: dopo 10 fallimenti consecutivi, mette in pausa invio per 5 minuti

### 8.5 Logging e monitoring

- Log di ogni invio: destinatario, tipo email, timestamp, esito
- Metriche: numero email inviate/fallite per tipo, latenza SMTP
- Alert: se tasso fallimento supera 20% in finestra di 15 minuti

---

## 9) Frontend

### 9.1 Evoluzione `auth-service.js`

Nuove funzioni da aggiungere:

```javascript
// Social login
AuthService.initiateSocialLogin(provider, mode)  // mode: 'login' | 'link'
AuthService.handleSocialCallback()                // elabora token da URL fragment

// Password management
AuthService.changePassword(currentPassword, newPassword)
AuthService.forgotPassword(email)
AuthService.resetPassword(email, token, newPassword)
AuthService.setupPassword(email, token, newPassword)

// Session management
AuthService.revokeAllSessions()

// External login management
AuthService.getExternalLogins()
AuthService.unlinkExternalLogin(loginId)

// Admin
AuthService.searchUsers(filters, page, pageSize)
AuthService.getUserDetail(userId)
AuthService.changeUserRole(userId, ruolo)
AuthService.disableUser(userId)
AuthService.enableUser(userId)
AuthService.forcePasswordReset(userId)
AuthService.inviteUser(email, ruolo, nome, cognome)
```

### 9.2 Pagine frontend nuove/modificate

#### `login.html` (modifica)
- Aggiungere sezione "Oppure accedi con":
  - Pulsante "Accedi con Google" (con logo)
  - Pulsante "Accedi con Microsoft" (con logo)
- Supporto parametro `callback` (esistente) + `mode=link` per collegamento social
- Azione pulsanti social: `AuthService.initiateSocialLogin(provider, 'login')`

#### `register.html` (modifica)
- Aggiungere sezione "Oppure registrati con":
  - Pulsanti social (Google, Microsoft)
- Se registrazione social: redirect a provider con `mode=login` (non esiste `mode=register` perche il backend decide se creare o linkare)

#### `social-login-complete.html` (NUOVO)
- Riceve token da URL fragment (access_token, refresh_token, user)
- Salva in localStorage tramite `AuthService`
- Redirect alla pagina di destinazione (da `returnUrl` o `/index.html`)
- Gestione errori: mostra errore se callback ha fallito
- UI: spinner + messaggio "Accesso in corso..." / "Errore di autenticazione"

#### `profile.html` (modifica)
- Sezione "Sicurezza account" aggiuntiva:
  - Stato password: "Password impostata" o "Nessuna password (account social)"
  - Bottone "Cambia password" (visibile solo se `localCredentialsEnabled`)
  - Bottone "Imposta password" (visibile solo se social-only)
  - Provider social collegati: lista con pulsante "Scollega"
  - Bottone "Collega account Google/Microsoft"
- Sezione "Sessioni":
  - Bottone "Disconnetti tutti i dispositivi" (POST /auth/revoke-all-sessions)
- Sezione "Elimina account" (opzionale, con doppia conferma)

#### `recupera-password.html` (NUOVO)
- Form con campo email
- Bottone "Invia link di recupero"
- Validazione email lato client
- Messaggio di successo: "Se l'email e associata a un account, riceverai un link di recupero."
- Loading state sul bottone
- Rate limiting visivo: dopo 3 tentativi, countdown 60 secondi

#### `reimposta-password.html` (NUOVO)
- Arriva da link email con parametri `token` e `email`
- Legge parametri da query string
- Form: nuova password + conferma password
- Validazione robustezza password in tempo reale
- Submit: `POST /auth/reset-password`
- Successo: messaggio + redirect a login dopo 3 secondi
- Errore: messaggi specifici ("Token scaduto", "Token gia usato", "Email non trovata")

#### `setup-password.html` (NUOVO)
- Simile a `reimposta-password.html` ma per setup password (social e inviti)
- Arriva da link email con `token` e `email`
- Form: nuova password + conferma
- Submit: `POST /auth/setup-password`
- Successo: messaggio + redirect a login

#### `utenti.html` (modifica estesa)
- Sostituire tabella attuale con interfaccia avanzata:
  - Barra di ricerca con filtro in tempo reale
  - Filtri: dropdown ruolo, toggle "Solo disabilitati", toggle "Solo social"
  - Tabella paginata con colonne: ID, Nome, Email, Ruolo (dropdown editabile), Provider, Stato, Ultimo Accesso, Azioni
  - Azioni per utente:
    - Modifica ruolo (con conferma modale)
    - Disabilita/Riabilita (toggle)
    - Forza reset password
    - Elimina (con doppia conferma)
    - Visualizza dettaglio sicurezza
  - Pulsante "Invita utente" (apre modale con form: email, ruolo, nome, cognome)
- Dettaglio utente (modale o pagina separata):
  - Tutte le info di sicurezza
  - Audit log eventi recenti
  - Provider social collegati
  - Data ultimo accesso e ultimo cambio password
  - Versione Auth e numero sessioni attive

### 9.3 Validazioni frontend

- **Password**: min 8 caratteri, 1 maiuscola, 1 minuscola, 1 numero, 1 speciale (validatore live mentre si digita)
- **Email**: formato RFC 5322 semplificato
- **Nome/Cognome**: 1-100 caratteri, no numeri/speciali (eccetto apostrofo e trattino)
- **Confirm password**: match in tempo reale
- **Token**: presenza in URL, formato valido (base64url)

### 9.4 Loading states e UX

- Tutti i form: bottone disabilitato durante submit, spinner inline
- Social login: redirect con overlay "Reindirizzamento in corso..."
- Callback: `social-login-complete.html` con spinner e messaggio
- Operazioni admin: conferma modale per azioni irreversibili (cancellazione, degradazione admin)
- Feedback: toast notifications per successo/errore (sistema centralizzato)

### 9.5 Route guard aggiornati

File: `js/auth-guard.js` — estendere con:
- `requireAdmin()`: verifica ruolo admin
- `requireNotDisabled()`: verifica account non disabilitato (ricevuto da GET /auth/me)
- `requireLocalCredentials()`: per pagine che richiedono password (es. cambio password)
- `handleSocialCallback()`: logica specifica per pagina `social-login-complete.html`

### 9.6 Protezione XSS/CSRF frontend

- **Output encoding**: tutti i dati utente renderizzati via `textContent` (non `innerHTML`)
- **Content Security Policy** header:
  ```
  default-src 'self';
  script-src 'self';
  style-src 'self' 'unsafe-inline';
  img-src 'self' data: https://*.googleusercontent.com https://graph.microsoft.com;
  connect-src 'self' https://accounts.google.com https://login.microsoftonline.com;
  frame-src https://accounts.google.com https://login.microsoftonline.com;
  ```
- **CSRF token**: per operazioni sensibili, includere header `X-CSRF-TOKEN` (se si usa cookie auth in futuro)
- **Referrer-Policy**: `strict-origin-when-cross-origin`

### 9.7 Storage sicuro token

Raccomandazione evolutiva:
- **Fase 1 (questa iterazione)**: Access token in `sessionStorage`, refresh token in `localStorage` (miglioramento rispetto a entrambi in localStorage)
- **Fase 2 (futuro)**: Refresh token in httpOnly Secure SameSite=Strict cookie
- **Mai** esporre token in URL (gia cosi, usiamo URL fragment per social callback)

---

## 10) Security Hardening

### 10.1 Rate limiting

Utilizzare `AspNetCoreRateLimit` o middleware custom.

Configurazione (in `.env` o `appsettings.json`):

| Endpoint | Limite | Periodo | Motivazione |
|----------|--------|---------|-------------|
| `POST /auth/login` | 5 | 1 min per IP | Anti brute force |
| `POST /auth/login` | 10 | 15 min per email | Anti password spraying |
| `POST /auth/register` | 3 | 15 min per IP | Anti mass registration |
| `POST /auth/refresh` | 10 | 1 min per IP | Anti refresh abuse |
| `POST /auth/forgot-password` | 3 | 15 min per IP | Anti enumeration |
| `POST /auth/reset-password` | 5 | 15 min per IP | Anti brute force token |
| `GET /auth/external/{provider}` | 10 | 1 min per IP | Anti redirect abuse |

Implementazione:
- Middleware rate limiting (es. `RateLimiter` built-in .NET 7+ o custom)
- Store contatori in memoria (sviluppo) o Redis (produzione)
- Header response: `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`, `Retry-After`

### 10.2 Brute force protection

- **Delay progressivo**: dopo N tentativi falliti, introdurre delay crescente
  - Tentativo 1-3: 0 ms
  - Tentativo 4-5: 500 ms
  - Tentativo 6-10: 2000 ms
  - Tentativo 11+: 5000 ms
- **Notifica utente**: dopo 5 tentativi falliti, inviare email di alert sicurezza ("Tentativi di accesso sospetti")
- **Lockout temporaneo** (opzionale): dopo 10 tentativi falliti, bloccare account per 15 minuti
  - Campi aggiuntivi su Utente: `FailedLoginAttempts` (int), `LockoutEndUtc` (DateTime?)
  - Sblocco automatico dopo `LockoutEndUtc`
  - Sblocco manuale admin

### 10.3 Anti open-redirect

Centralizzare in una funzione `IsValidReturnUrl(string url)` usata in:
- Callback social login
- Redirect post-login
- Link in email

Logica:
```csharp
bool IsValidReturnUrl(string returnUrl)
{
    if (string.IsNullOrEmpty(returnUrl)) return false;
    if (returnUrl.StartsWith("/")) return true; // URL relativo
    if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri))
    {
        // Stesso host
        return uri.Host.Equals(_allowedHost, StringComparison.OrdinalIgnoreCase);
    }
    return false;
}
```

### 10.4 Anti token replay

- `ExternalAuthExchangeCode`: hash del code salvato, consumato una sola volta
- `AccountActionToken`: `ConsumedAtUtc` single-use enforcement
- JWT `jti` claim: (differibile) lista nera token revocati in cache Redis

### 10.5 Secure cookies

Per future implementazioni con httpOnly cookie (fase 2):
```
Set-Cookie: refresh_token=xxx;
  HttpOnly;
  Secure;
  SameSite=Strict;
  Path=/auth;
  Max-Age=604800
```

### 10.6 HTTPS enforcement

- `app.UseHttpsRedirection()` gia configurato
- `app.UseHsts()` in produzione
- Strict-Transport-Security header: `max-age=31536000; includeSubDomains`

### 10.7 Audit logging

Tutti gli eventi di sicurezza vengono registrati in `UserSecurityAuditLog`:
- Login riuscito/fallito (con IP e User-Agent)
- Cambio/reset/setup password
- Collegamento/scollegamento provider social
- Modifica ruolo
- Disabilitazione/riabilitazione account
- Invito utente
- Eliminazione account
- Refresh token
- Logout (tutti i dispositivi)
- Revoca sessioni

Implementazione: servizio `SecurityAuditService` con metodo `LogEventAsync(...)`.

### 10.8 IP/Device tracking opzionale

Campi `IpAddress` e `UserAgent` in `UserSecurityAuditLog`.
Alert automatico se:
- Login da IP in paese diverso da ultimo login (geolocalizzazione opzionale)
- Login da nuovo dispositivo/browser non visto prima

---

## 11) Testing

### 11.1 Strategia test

| Livello | Tipologia | Estensione |
|---------|-----------|------------|
| Unit | Servizi auth, password service, token service, validatori | Copertura > 90% |
| Unit | Regole RBAC, helper autorizzativi | Copertura > 90% |
| Integration | Endpoint auth: register, login, refresh, logout | Tutti i casi |
| Integration | Password management: forgot, reset, change, setup | Ciclo completo |
| Integration | Social login: callback, linking, errori provider | Con mock provider OIDC |
| Integration | Admin: change role, disable/enable, invite | Tutti i vincoli sicurezza |
| Integration | Rate limiting: verifica header e risposta 429 | Per ogni endpoint protetto |
| Integration | JWT invalidation: AuthVersion, OnTokenValidated | Scenari multipli |
| Integration | Anti-replay: code exchange e token email | Verifica single-use |
| Security | Anti open-redirect: vari URL malevoli | Test parametrizzato |
| Security | RBAC: accesso non autorizzato a endpoint admin | Tutti i ruoli |
| E2E | Flusso completo: register → login → change password → logout → login | |
| E2E | Flusso social: Google login → acquire token → accesso risorse | |
| E2E | Flusso forgot/reset password | |
| E2E | Flusso admin: create user, change role, disable, delete | |
| Smoke | Avvio applicazione, health check, endpoint pubblici OK | |

### 11.2 Checklist QA finale

- [ ] Login email/password funzionante
- [ ] Login Google OIDC funzionante
- [ ] Login Microsoft OIDC funzionante
- [ ] Account ibrido social+password: entrambi i metodi di login funzionanti
- [ ] Account social-only: solo social login, non password
- [ ] Linking social a account esistente
- [ ] Cambio password invalida token esistenti
- [ ] Reset password invalida token esistenti
- [ ] Token reset single-use (non riutilizzabile)
- [ ] Token reset scade dopo TTL configurato
- [ ] AuthVersion blocca token dopo cambio password/ruolo
- [ ] Anti open-redirect blocca URL esterni malevoli
- [ ] Rate limiting restituisce 429 dopo superamento soglia
- [ ] PowerUser/Admin non possono autenticarsi via social
- [ ] Social-only non possono essere promossi a PowerUser/Admin
- [ ] Ultimo admin non puo essere degradato o eliminato
- [ ] Account disabilitato non puo fare login (nemmeno con token esistente)
- [ ] Account disabilitato non puo fare refresh token
- [ ] Audit log registra tutti gli eventi di sicurezza richiesti
- [ ] Email inviate correttamente (reset, setup, invito, ruolo, alert)
- [ ] CSP header presente su tutte le pagine
- [ ] CORS configurato per sviluppo e produzione

---

## 12) Deployment & Migration

### 12.1 Strategia migrazione

1. **Nuova migrazione EF Core**:
   ```
   dotnet ef migrations add Iteration5_IdentityAndSecurity
   ```
   Contiene:
   - Modifiche tabella `Utenti`: nuovi campi, alter colonna `PasswordHash` -> nullable
   - Nuove tabelle: `UserExternalLogins`, `AccountActionTokens`, `ExternalAuthStates`, `ExternalAuthExchangeCodes`, `UserSecurityAuditLogs`
   - Nuovi indici e vincoli

2. **Migrazione dati utenti esistenti**:
   - Per ogni utente esistente: `NormalizedEmail = UPPER(Email)`, `LocalCredentialsEnabled = true`, `AuthVersion = 1`, `EmailVerified = true` (presumibilmente verificati), `SecurityStamp = new GUID()`, `CreatedAtUtc = now` (se non presente)
   - Seed admin: `NormalizedEmail = UPPER(admin email)`, `AuthVersion = 1`

### 12.2 Rollout progressivo

#### Fase 1: Setup infrastruttura (no breaking changes)
- Eseguire migrazione DB
- Aggiungere nuovi campi a Utente con valori default
- Deploy servizi email e audit
- **Nessun cambiamento ai flussi esistenti**

#### Fase 2: Password management e JWT hardening
- Attivare nuovi endpoint password (change, forgot, reset, setup)
- Attivare OnTokenValidated con AuthVersion
- Deploy frontend pagine password
- **Possibile invalidazione token esistenti**: comunicare agli utenti (email informativa)

#### Fase 3: Social login
- Configurare Google e Microsoft OAuth applications
- Deploy endpoint social e callback
- Deploy frontend pulsanti social
- **Nessun impatto su utenti esistenti**

#### Fase 4: Admin avanzato
- Deploy nuovi endpoint admin
- Deploy `utenti.html` aggiornato
- **Nessun breaking change**

### 12.3 Feature flags

Utilizzare variabili ambiente per attivazione incrementale:

```
FEATURE_SOCIAL_LOGIN=true         # default false finche non configurato
FEATURE_SOCIAL_GOOGLE_ENABLED=true
FEATURE_SOCIAL_MICROSOFT_ENABLED=true
FEATURE_PASSWORD_SETUP=true
FEATURE_ADMIN_INVITE=true
FEATURE_AUDIT_LOGGING=true
FEATURE_RATE_LIMITING=true
FEATURE_IP_TRACKING=false          # privacy, default off
```

### 12.4 Rollback plan

- Le nuove tabelle sono additive: nessuna distruzione dati esistenti
- I nuovi campi su `Utente` hanno default sicuri
- Se necessario rollback:
  1. Disabilitare feature flags
  2. Rollback codice (puntare a commit precedente)
  3. Le nuove tabelle possono rimanere (non bloccano il funzionamento esistente)
  4. La migrazione DB non viene revertita (forward-only), ma i nuovi campi non sono usati
- Rollback critico: revert migrazione (solo se necessario e testato in staging)

### 12.5 Configurazioni environment

Tutte le nuove variabili ambiente da aggiungere a `.env`:

```
# === Identity & Security ===

# JWT (da spostare qui da hardcoded)
JWT_SECRET_KEY=<generare-chiave-64-char-min>
JWT_ISSUER=FilmAPI
JWT_AUDIENCE=FilmFrontend
JWT_ACCESS_TOKEN_EXPIRY_MINUTES=15
JWT_REFRESH_TOKEN_EXPIRY_DAYS=7

# Google OIDC
GOOGLE_CLIENT_ID=<client-id>
GOOGLE_CLIENT_SECRET=<client-secret>

# Microsoft OIDC
MICROSOFT_CLIENT_ID=<client-id>
MICROSOFT_CLIENT_SECRET=<client-secret>

# Password management
ACCOUNT_TOKEN_PASSWORD_RESET_TTL_MINUTES=60
ACCOUNT_TOKEN_PASSWORD_SETUP_TTL_MINUTES=1440
ACCOUNT_TOKEN_ADMIN_INVITE_TTL_HOURS=72

# Rate limiting
RATE_LIMIT_LOGIN_PER_MINUTE=5
RATE_LIMIT_REGISTER_PER_15MIN=3
RATE_LIMIT_FORGOT_PASSWORD_PER_15MIN=3

# Feature flags
FEATURE_SOCIAL_LOGIN=true
FEATURE_SOCIAL_GOOGLE_ENABLED=true
FEATURE_SOCIAL_MICROSOFT_ENABLED=true
FEATURE_AUDIT_LOGGING=true
FEATURE_RATE_LIMITING=true
FEATURE_IP_TRACKING=false

# Base URL (per link email)
APP_BASE_URL=https://localhost:5001

# Admin seed (esistenti)
DEFAULT_ADMIN_EMAIL=admin@filmapi.local
DEFAULT_ADMIN_PASSWORD=Admin123!
```

### 12.6 Secrets management

- **MAI** committare `.env` con valori reali (gia in `.gitignore`)
- `.env.example` aggiornato con tutte le nuove variabili (valori placeholder)
- In produzione: usare Azure Key Vault / AWS Secrets Manager / variabili ambiente di sistema
- Ruotare `JWT_SECRET_KEY` periodicamente (ogni 90 giorni): invalida tutti i token esistenti, comunicare manutenzione programmata

---

## 13) Roadmap implementativa (ordine di sviluppo consigliato)

### Sprint 1 — Fondazioni modello dati e servizi core (Giorni 1-3)
1. [ ] Aggiungere pacchetti NuGet: `MailKit`, `AspNetCoreRateLimit` (o equivalente)
2. [ ] Creare nuovi modelli: `UserExternalLogin`, `AccountActionToken`, `ExternalAuthState`, `ExternalAuthExchangeCode`, `UserSecurityAuditLog`
3. [ ] Estendere `Utente.cs` con nuovi campi
4. [ ] Aggiornare `FilmDbContext.cs` con nuove entita, relazioni, indici
5. [ ] Aggiornare `UtenteDTO` e creare nuovi DTO (dettaglio admin, paginazione, etc.)
6. [ ] Creare `SecurityAuditService.cs`
7. [ ] Creare `EmailService.cs` con template base
8. [ ] Creare migrazione `Iteration5_IdentityAndSecurity`
9. [ ] Aggiornare seed utenti (admin e popolazione campi default)
10. [ ] Spostare JWT config in `.env` e `Program.cs` centralizzato

### Sprint 2 — Password Management (Giorni 4-5)
11. [ ] Implementare `POST /auth/me/change-password`
12. [ ] Implementare `POST /auth/forgot-password` con anti-enumerazione
13. [ ] Implementare `POST /auth/reset-password` con token single-use
14. [ ] Implementare `POST /auth/me/request-password-setup` e `POST /auth/setup-password`
15. [ ] Implementare `POST /auth/revoke-all-sessions`
16. [ ] Aggiornare email template (reset, setup)
17. [ ] Aggiornare `AuthService` con logica cambio/reset/setup password

### Sprint 3 — JWT Hardening (Giorni 6-7)
18. [ ] Implementare AuthVersion in generazione token
19. [ ] Implementare OnTokenValidated middleware con controlli DB
20. [ ] Implementare invalidazione automatica post cambio password, reset, modifica ruolo
21. [ ] Implementare cleanup job refresh token scaduti
22. [ ] Implementare cleanup job `ExternalAuthState` scaduti
23. [ ] Implementare cleanup job `UserSecurityAuditLog` vecchi

### Sprint 4 — Social Login (Giorni 8-10)
24. [ ] Creare `SocialAuthService.cs` con logica OIDC
25. [ ] Implementare `GET /auth/external/{provider}`
26. [ ] Implementare `GET /auth/external/callback`
27. [ ] Configurare Google OIDC (client ID/secret, endpoint, validazioni)
28. [ ] Configurare Microsoft OIDC multi-tenant
29. [ ] Implementare mapping claims e linking ibrido
30. [ ] Implementare `DELETE /auth/me/external-logins/{id}`
31. [ ] Implementare `GET /auth/me/external-logins`
32. [ ] Registrare audit per eventi social (link, unlink, login)
33. [ ] Implementare anti open-redirect per callback

### Sprint 5 — Admin Avanzato (Giorni 11-12)
34. [ ] Implementare `GET /auth/admin/utenti` con filtri, ricerca, paginazione
35. [ ] Implementare `GET /auth/admin/utenti/{id}` dettaglio sicurezza
36. [ ] Aggiornare `PUT /auth/admin/utenti/{id}/ruolo` con regole sicurezza
37. [ ] Implementare `PUT /auth/admin/utenti/{id}/disable` / `enable`
38. [ ] Implementare `POST /auth/admin/utenti/{id}/force-password-reset`
39. [ ] Implementare `POST /auth/admin/invite` con email setup password
40. [ ] Aggiornare `DELETE /auth/admin/utenti/{id}` con protezione ultimo admin
41. [ ] Aggiornare email template (invito, cambio ruolo, alert sicurezza)

### Sprint 6 — Security Hardening (Giorni 13-14)
42. [ ] Implementare rate limiting su tutti gli endpoint auth
43. [ ] Implementare brute force protection (delay + lockout)
44. [ ] Implementare CSP headers
45. [ ] Verificare HTTPS enforcement e HSTS
46. [ ] Implementare anti-replay per exchange code e token email
47. [ ] Audit logging completo su tutti gli eventi di sicurezza

### Sprint 7 — Frontend (Giorni 15-18)
48. [ ] Aggiornare `auth-service.js` con nuove funzioni
49. [ ] Aggiornare `auth-guard.js` con nuovi guard
50. [ ] Aggiornare `api-client.js` per gestione errori 429 (rate limit)
51. [ ] Modificare `login.html` con pulsanti social
52. [ ] Modificare `register.html` con pulsanti social
53. [ ] Creare `social-login-complete.html`
54. [ ] Modificare `profile.html` con sezione sicurezza, password, social, sessioni
55. [ ] Creare `recupera-password.html` + `js/recupera-password.js`
56. [ ] Creare `reimposta-password.html` + `js/reimposta-password.js`
57. [ ] Creare `setup-password.html` + `js/setup-password.js`
58. [ ] Riscrivere `utenti.html` con interfaccia avanzata admin + `js/utenti.js`
59. [ ] Aggiornare `navbar.html` e `navbar.js` per nuove pagine
60. [ ] Implementare toast notification system centralizzato

### Sprint 8 — Testing e QA (Giorni 19-21)
61. [ ] Unit test: `AuthService`, `PasswordService`, `JwtTokenService`, `SocialAuthService`
62. [ ] Unit test: validatori, helper autorizzativi, anti open-redirect
63. [ ] Integration test: endpoint password management (ciclo completo)
64. [ ] Integration test: social login (mock provider OIDC)
65. [ ] Integration test: admin RBAC e regole sicurezza
66. [ ] Integration test: JWT invalidation (AuthVersion, OnTokenValidated)
67. [ ] Integration test: rate limiting
68. [ ] Integration test: anti-replay
69. [ ] E2E test: flussi principali (register → login → change password → logout)
70. [ ] E2E test: flusso forgot/reset password
71. [ ] E2E test: flusso admin (invito → setup password → promozione → degradazione → disable)
72. [ ] Security test: anti open-redirect, RBAC enforcement, brute force
73. [ ] Smoke test: avvio, health check, endpoint pubblici

---

## 14) WBS — Checklist operativa

### Fondazioni
- [ ] Spostare tutte le variabili JWT in `.env` (rimuovere hardcoded)
- [ ] Aggiornare `.env.example` con tutte le nuove variabili
- [ ] Modelli creati: `UserExternalLogin`, `AccountActionToken`, `ExternalAuthState`, `ExternalAuthExchangeCode`, `UserSecurityAuditLog`
- [ ] `Utente.cs` esteso con: `NormalizedEmail`, `LocalCredentialsEnabled`, `PasswordHash` nullable, `AuthVersion`, `SecurityStamp`, `IsDisabled`, `LastLoginAtUtc`, `LastLoginProvider`, `EmailVerified`, `CreatedAtUtc`
- [ ] `FilmDbContext.cs` aggiornato con DbSet, Fluent API, indici, vincoli
- [ ] Migrazione `Iteration5_IdentityAndSecurity` creata e applicata
- [ ] DTOs creati: `UtenteAdminDTO`, `UtenteAdminDetailDTO`, `UtenteListResponseDTO`, `ChangePasswordDTO`, `ForgotPasswordDTO`, `ResetPasswordDTO`, `SetupPasswordDTO`, `InviteUserDTO`, `UpdateRuoloDTO` (aggiornato), nuovi request/response DTO
- [ ] `EmailService.cs` implementato con template HTML
- [ ] `SecurityAuditService.cs` implementato

### Password Management
- [ ] `POST /auth/me/change-password` (authenticated, con password corrente)
- [ ] `POST /auth/forgot-password` (anonymous, anti-enumerazione)
- [ ] `POST /auth/reset-password` (anonymous, token single-use)
- [ ] `POST /auth/me/request-password-setup` (authenticated, per social-only)
- [ ] `POST /auth/setup-password` (anonymous, token email)
- [ ] `POST /auth/revoke-all-sessions` (authenticated, invalida tutto)

### JWT Hardening
- [ ] Claim `auth_version` aggiunto a JWT
- [ ] OnTokenValidated verifica `AuthVersion`, `IsDisabled`, `PasswordChangedAtUtc`
- [ ] Invalidazione token dopo cambio password (incrementa AuthVersion)
- [ ] Invalidazione token dopo reset password
- [ ] Invalidazione token dopo modifica ruolo
- [ ] Invalidazione token dopo disable account
- [ ] Refresh token revocabili su eventi di sicurezza
- [ ] Cleanup job: refresh token scaduti, ExternalAuthState scaduti, audit log vecchi

### Social Login
- [ ] `GET /auth/external/{provider}` genera URL OIDC e salva state
- [ ] `GET /auth/external/callback` valida state, scambia code, valida token, crea/linka utente
- [ ] Anti-replay code exchange (ExternalAuthExchangeCode)
- [ ] Anti open-redirect su ReturnUrl
- [ ] Configurazione Google OIDC completa
- [ ] Configurazione Microsoft OIDC multi-tenant completa
- [ ] Mapping claims Google: `sub`, `email`, `email_verified`, `given_name`, `family_name`
- [ ] Mapping claims Microsoft: `oid`, `tid`, `email`, `given_name`, `family_name`
- [ ] Linking automatico per utenti standard
- [ ] Blocco social login per PowerUser/Admin
- [ ] `GET /auth/me/external-logins` lista provider collegati
- [ ] `DELETE /auth/me/external-logins/{id}` scollega provider
- [ ] Gestione errori provider (state scaduto, code invalido, email non verificata)

### Admin & RBAC
- [ ] `GET /auth/admin/utenti` con search, filtri, paginazione
- [ ] `GET /auth/admin/utenti/{id}` dettaglio sicurezza completo
- [ ] `PUT /auth/admin/utenti/{id}/ruolo` con regole:
  - [ ] Non degradabile se ultimo admin
  - [ ] Social-only non promuovibile
  - [ ] Non self-degradazione
  - [ ] Audit log registrato
- [ ] `PUT /auth/admin/utenti/{id}/disable` / `enable`
- [ ] `POST /auth/admin/utenti/{id}/force-password-reset`
- [ ] `DELETE /auth/admin/utenti/{id}` con protezione ultimo admin + audit
- [ ] `POST /auth/admin/invite` con email setup password
- [ ] Policy autorizzative aggiornate in `Program.cs`
- [ ] Helper RBAC: `RequireNotDisabled`, `RequireLocalCredentials`, `RequireNotLastAdmin`, `RequireNotSelf`

### Email
- [ ] Template HTML: reset password
- [ ] Template HTML: setup password
- [ ] Template HTML: invito admin/poweruser
- [ ] Template HTML: cambio ruolo notifica
- [ ] Template HTML: cambio password notifica
- [ ] Template HTML: alert sicurezza
- [ ] Testo plain fallback per tutti i template
- [ ] Retry policy con exponential backoff
- [ ] Logging invio email
- [ ] SMTP configurato via `.env`

### Security Hardening
- [ ] Rate limiting su `POST /auth/login` (5/min per IP, 10/15min per email)
- [ ] Rate limiting su `POST /auth/register` (3/15min per IP)
- [ ] Rate limiting su `POST /auth/forgot-password` (3/15min per IP)
- [ ] Rate limiting su `POST /auth/reset-password` (5/15min per IP)
- [ ] Rate limiting su `POST /auth/refresh` (10/1min per IP)
- [ ] Rate limiting su `GET /auth/external/{provider}` (10/1min per IP)
- [ ] Brute force: delay progressivo dopo tentativi falliti
- [ ] Brute force: notifica email dopo 5+ tentativi falliti
- [ ] Anti open-redirect: validatore `IsValidReturnUrl`
- [ ] CSP header su tutte le risposte HTML
- [ ] HTTPS enforcement e HSTS in produzione
- [ ] Audit log: tutti gli eventi di sicurezza registrati
- [ ] Cleanup audit log: job periodico

### Frontend
- [ ] `auth-service.js`: nuove funzioni social, password, admin
- [ ] `auth-guard.js`: nuovi guard (admin, not-disabled, local-credentials)
- [ ] `api-client.js`: gestione HTTP 429 (Rate Limit) con retry-after
- [ ] `login.html`: pulsanti Google / Microsoft
- [ ] `register.html`: pulsanti Google / Microsoft
- [ ] `social-login-complete.html`: elaborazione callback social
- [ ] `profile.html`: sezioni sicurezza, password, social, sessioni
- [ ] `recupera-password.html` + JS: form forgot password
- [ ] `reimposta-password.html` + JS: form reset password
- [ ] `setup-password.html` + JS: form setup password
- [ ] `utenti.html`: interfaccia admin avanzata (search, filtri, paginazione, azioni)
- [ ] `navbar.html`: aggiornamento link per nuove pagine
- [ ] Toast notification system centralizzato
- [ ] Loading states su tutti i form
- [ ] Validazioni client robustezza password live
- [ ] Gestione errori API (messaggi user-friendly da backend)

### Testing
- [ ] Unit test servizi core auth
- [ ] Unit test regole RBAC
- [ ] Integration test endpoint password management
- [ ] Integration test social login (mock provider)
- [ ] Integration test admin RBAC e vincoli
- [ ] Integration test JWT invalidation
- [ ] Integration test rate limiting
- [ ] Integration test anti-replay
- [ ] E2E test flussi principali
- [ ] Security test (open redirect, RBAC, brute force)
- [ ] Smoke test

---

## 15) Criteri di accettazione

1. **Login**: email/password, Google e Microsoft tutti funzionanti.
2. **Account ibridi**: utente con password + social collegati puo accedere in entrambi i modi.
3. **Account social-only**: login solo via social, setup password possibile.
4. **Password management**:
   - Cambio password (autenticato) con invalidazione token esistenti.
   - Forgot/reset password via email con token single-use.
   - Setup password per account social-only (autenticato e via email).
5. **JWT hardening**:
   - Cambio password, reset password, modifica ruolo, disable account invalidano tutti i token.
   - OnTokenValidated verifica AuthVersion, IsDisabled, PasswordChangedAtUtc.
6. **Admin**:
   - Ricerca, filtri, paginazione utenti.
   - Promozione/degradazione con vincoli (social-only non promuovibile, ultimo admin non degradabile, no self-degradazione).
   - Disabilitazione/riabilitazione utente.
   - Invito amministratori con setup password via email.
7. **Email**: tutte le email (reset, setup, invito, ruolo, alert) vengono inviate correttamente.
8. **Audit**: ogni evento di sicurezza registrato in `UserSecurityAuditLog`.
9. **Rate limiting**: endpoint sensibili restituiscono 429 dopo superamento soglia.
10. **Anti open-redirect**: URL esterni malevoli bloccati su callback social e redirect.
11. **Anti-replay**: exchange code OIDC e token email non riutilizzabili.
12. **Frontend**: tutte le pagine richieste funzionanti con UX coerente (loading, errori, validazioni).
13. **Test**: suite completa (unit, integration, E2E, security) eseguita con successo.
14. **Configurazione**: tutte le variabili in `.env`, nessun segreto hardcoded in codice.
15. **Migrazione DB**: applicata con successo, utenti esistenti migrati correttamente.

---

## 16) Deliverable Iterazione 5

- `docs/project/dev_iteration/5/PianoDiLavoro.md` (questo documento)
- Migrazione DB `Iteration5_IdentityAndSecurity`
- Nuovi modelli: `UserExternalLogin`, `AccountActionToken`, `ExternalAuthState`, `ExternalAuthExchangeCode`, `UserSecurityAuditLog`
- Modello `Utente` esteso con nuovi campi
- Nuovi DTOs per tutte le operazioni
- Nuovi endpoint backend (social login, password management, admin avanzato, sessioni)
- Nuovi/aggiornati servizi backend (`SocialAuthService`, `EmailService`, `SecurityAuditService`, aggiornamento `AuthService`, `JwtTokenService`)
- Middleware `OnTokenValidated` con controlli DB
- Sistema rate limiting
- Sistema email con template HTML
- 7 pagine frontend nuove/modificate (login, register, social-login-complete, profile, recupera-password, reimposta-password, setup-password)
- Riscrittura `utenti.html` con interfaccia admin avanzata
- Aggiornamento `auth-service.js`, `auth-guard.js`, `api-client.js`
- Toast notification system
- Suite test completa (unit, integration, E2E, security)
- Aggiornamento `.env.example` con tutte le nuove variabili
- Aggiornamento `docs/project/status.md` e `docs/project/changelog.md`
- Validazione QA secondo checklist sezione 11.2

---

## 17) Riepilogo dipendenze tecniche

| Dipendenza | Versione | Utilizzo |
|------------|----------|----------|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 9.0.x | JWT validation (esistente) |
| `Microsoft.AspNetCore.Authentication.Google` | 9.0.x | Google OIDC (nuovo, opzionale vs implementazione custom) |
| `Microsoft.AspNetCore.Authentication.MicrosoftAccount` | 9.0.x | Microsoft OIDC (nuovo, opzionale vs implementazione custom) |
| `MailKit` | 4.x | Invio email SMTP (nuovo) |
| `BCrypt.Net-Next` | esistente | Hash password (esistente) |
| `Pomelo.EntityFrameworkCore.MySql` | esistente | Database (esistente) |
| `DotNetEnv` | esistente | Variabili ambiente (esistente) |

Nota su Google/Microsoft auth:
- Opzione A: usare pacchetti Microsoft ufficiali (`Microsoft.AspNetCore.Authentication.Google`, `.MicrosoftAccount`) che gestiscono OIDC nativamente
- Opzione B: implementazione custom con `HttpClient` + validazione manuale id_token (maggiore controllo su mapping claims e linking ibrido)
- **Raccomandazione**: Opzione B per mapping claims completo e gestione multi-tenant Microsoft con `tid`/`oid`

---

## 18) Note implementative per AI agent

1. **Coerenza nomenclatura**: tutte le nuove entita e endpoint seguono le convenzioni esistenti (italiano per modello: `Utente`, `Prenotazione`; inglese per technical: `JwtTokenService`, `OnTokenValidated`).

2. **Nullable PasswordHash**: e una modifica breaking. Verificare che tutte le query e proiezioni gestiscano `PasswordHash == null`. La validazione login locale deve fallire se `PasswordHash` e nullo.

3. **NormalizedEmail**: popolato automaticamente in fase di creazione/aggiornamento email. Usare `ToUpperInvariant()` per coerenza.

4. **Token hashati**: i token email non vengono mai salvati in chiaro nel DB. Si salva `SHA256(token_raw)`. Il token raw viene inviato una sola volta via email e mai loggato. Nei log, registrare solo l'ID del token (non l'hash).

5. **ExternalAuthState cleanup**: implementare come `IHostedService` con `PeriodicTimer` ogni 5 minuti, cancellando record con `ExpiresAtUtc < UtcNow`.

6. **Anti-enumerazione email**: `POST /auth/forgot-password` risponde SEMPRE 200 OK con lo stesso messaggio, sia che l'email esista o meno. La differenza e solo interna (invio email effettivo vs nessuna azione). Il tempo di risposta deve essere costante (evitare timing attack).

7. **Gestione multi-tenant Microsoft**: l'authority `/common` supporta sia account personali che aziendali. L'identificazione stabile e `(tid, oid)`. Per account personali, `tid` sara `9188040d-6c67-4c5b-b112-36a304b66dad`. Non fare assunzioni sul formato di `tid`.

8. **Rate limiting store**: in memoria per sviluppo (singolo server), Redis per produzione (multi-server). L'interfaccia deve essere astratta per permettere lo swap.

9. **OnTokenValidated performance**: la query DB ad ogni richiesta puo essere un collo di bottiglia. Per produzione, considerare caching in-memory con TTL breve (30-60 secondi) dei dati utente rilevanti (AuthVersion, IsDisabled, PasswordChangedAtUtc).

10. **Logout globale**: quando `AuthVersion` viene incrementato, TUTTI i token esistenti (inclusa la sessione corrente che ha fatto l'operazione) diventano invalidi alla prossima richiesta. Questo significa che dopo un cambio password, anche la richiesta che ha eseguito il cambio perdera il token successivo. Il frontend deve gestire questa situazione richiedendo nuovi token dopo l'operazione.

11. **Social login callback URL**: deve essere configurato come redirect URI autorizzato nelle console Google Cloud e Microsoft Entra ID. URL tipico: `https://{host}/auth/external/callback`.

12. **Nessuna dipendenza da Iterazione 4 non completata**: le funzionalita di questa iterazione sono indipendenti da PDF ticket e scanner barcode. L'unica sovrapposizione e il campo `CreditoPiattaforma` su `Utente`, che viene incluso nella migrazione di questa iterazione per non creare conflitti.

---

## 19) Riepilogo rischi e mitigazioni

| Rischio | Impatto | Probabilita | Mitigazione |
|---------|---------|-------------|-------------|
| Modifica `PasswordHash` a nullable rompe codice esistente | Alto | Media | Audit completo di tutte le query; test di regressione su login locale |
| Token OIDC non validabile (firma, issuer, audience) | Alto | Bassa | Test approfonditi con token reali; logging dettagliato degli errori di validazione |
| Rate limiting blocca utenti legittimi | Medio | Media | Soglie conservative; whitelist IP interni; header informative per client |
| Performance OnTokenValidated con query DB ogni richiesta | Medio | Alta | Caching in-memory con TTL breve; indici ottimizzati su `Utente.Id` |
| Email SMTP non recapitate | Medio | Media | Retry policy; coda persistente; logging; alert su tasso fallimento |
| Microsoft multi-tenant: cambiamenti endpoint/claims | Medio | Bassa | Configurazione via `.env`; monitoraggio changelog Microsoft |
| Migrazione dati utenti esistenti con campi nullable | Alto | Bassa | Script di migrazione dati testato in staging; backup pre-migrazione |
| Incompatibilita token dopo deploy (AuthVersion) | Alto | Media | Rollout graduale con feature flag; comunicazione agli utenti; finestra di manutenzione |
| Social login account PowerUser/Admin per errore di configurazione | Alto | Bassa | Test automatici; controllo esplicito ruolo prima di permettere linking |
| Open redirect non bloccato per edge case | Alto | Bassa | Test parametrizzati con payload malevoli noti; validatore whitelist |
