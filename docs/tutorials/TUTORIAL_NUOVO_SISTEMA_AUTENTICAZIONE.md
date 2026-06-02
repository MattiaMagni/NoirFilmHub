## Tutorial: Nuovo Sistema di Autenticazione CineBase (Iterazione 5)

**Autore:** AI Assistant  
**Data:** 10 Maggio 2026  
**Progetto di Riferimento:** FilmAPI / CineBase  
**Versione:** Iterazione 5 — Identity & Security Management Enterprise-Grade  
**Framework:** ASP.NET Core 9.0 Minimal API + EF Core + MariaDB  

---

## Indice

1. [Panoramica del Sistema](#1-panoramica-del-sistema)
2. [Come Funziona l'Autenticazione JWT](#2-come-funziona-lautenticazione-jwt)
3. [Login con Email e Password](#3-login-con-email-e-password)
4. [Login Social: Google e Microsoft](#4-login-social-google-e-microsoft)
5. [Account Ibridi: Social + Password](#5-account-ibridi-social--password)
6. [Gestione Password](#6-gestione-password)
7. [Invalidazione Token e Session Security](#7-invalidazione-token-e-session-security)
8. [Ruoli e Autorizzazioni (RBAC)](#8-ruoli-e-autorizzazioni-rbac)
9. [Gestione Utenti Amministratore](#9-gestione-utenti-amministratore)
10. [Audit di Sicurezza](#10-audit-di-sicurezza)
11. [Rate Limiting e Protezione Anti-Abuso](#11-rate-limiting-e-protezione-anti-abuso)
12. [Flussi Completi Step-by-Step](#12-flussi-completi-step-by-step)
13. [Diagramma dell'Architettura](#13-diagramma-dellarchitettura)
14. [Configurazione](#14-configurazione)
15. [Best Practices e Note di Sicurezza](#15-best-practices-e-note-di-sicurezza)

---

## 1. Panoramica del Sistema

Il nuovo sistema di autenticazione di CineBase e una piattaforma di identity management completa che supporta:

| Modalita di accesso | Descrizione |
|---------------------|-------------|
| **Email + Password** | Login tradizionale con credenziali locali |
| **Google OIDC** | Accesso con account Google (OpenID Connect) |
| **Microsoft OIDC** | Accesso con account Microsoft personale o aziendale (multi-tenant) |
| **Ibrido** | Account con password locale + uno o piu provider social collegati |

Il sistema gestisce tre profili utente distinti:

```
┌──────────────────────────────────────────────────────────────┐
│                    TIPI DI ACCOUNT                           │
│                                                               │
│  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────┐ │
│  │   Local-Only    │  │   Social-Only   │  │    Hybrid     │ │
│  │                 │  │                 │  │               │ │
│  │  Email ✓        │  │  Email ✓        │  │  Email ✓      │ │
│  │  Password ✓     │  │  Password ✗     │  │  Password ✓   │ │
│  │  Google ✗       │  │  Google ✓       │  │  Google ✓     │ │
│  │  Microsoft ✗    │  │  Microsoft ✓    │  │  Microsoft ✓  │ │
│  └─────────────────┘  └─────────────────┘  └──────────────┘ │
│                                                               │
│  Puo essere          Puo impostare        Puo usare tutti    │
│  promosso a          password via email   i metodi di login  │
│  PowerUser/Admin     (diventa Hybrid)                        │
└──────────────────────────────────────────────────────────────┘
```

---

## 2. Come Funziona l'Autenticazione JWT

### 2.1 Il Ciclo di Vita dei Token

```
                         ┌──────────────────────┐
                         │   UTENTE SI LOGGA    │
                         │  (email/password o   │
                         │   provider social)    │
                         └──────────┬───────────┘
                                    │
                                    ▼
                         ┌──────────────────────┐
                         │   BACKEND GENERA     │
                         │                      │
                         │  Access Token (JWT)  │─── scade dopo 15 minuti
                         │  Refresh Token       │─── scade dopo 7 giorni
                         └──────────┬───────────┘
                                    │
                  ┌─────────────────┼─────────────────┐
                  │                 │                 │
                  ▼                 ▼                 ▼
        ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
        │  USO NORMALE  │  │  TOKEN SCADE  │  │   LOGOUT /   │
        │               │  │               │  │ REVOCA FORZ. │
        │ Ogni richiesta│  │ Chiama        │  │               │
        │ API invia     │  │ /auth/refresh │  │ Token         │
        │ Access Token  │  │ col Refresh   │  │ invalidato    │
        │ nell'header   │  │ Token         │  │ lato server   │
        │ Authorization │  │               │  │ (AuthVersion)  │
        └──────────────┘  └──────┬────────┘  └──────────────┘
                                 │
                                 ▼
                      ┌──────────────────────┐
                      │   NUOVO ACCESS TOKEN │
                      │   + ROTAZIONE        │
                      │   REFRESH TOKEN      │
                      └──────────────────────┘
```

### 2.2 Cosa Contiene un JWT (Access Token)

```json
{
  "sub": "42",                          // ID utente
  "email": "mario.rossi@email.it",      // Email
  "name": "Mario Rossi",                // Nome completo
  "role": "utente",                     // Ruolo (admin, power_user, utente)
  "auth_version": "3",                  // Versione auth (per invalidazione)
  "security_stamp": "a1b2c3...",        // Security stamp
  "iat": 1715328000,                    // Issued at (timestamp)
  "exp": 1715328900,                    // Expiration (15 min dopo iat)
  "iss": "FilmAPI",                     // Issuer (chi ha emesso il token)
  "aud": "FilmFrontend"                 // Audience (chi deve accettarlo)
}
```

Il token e firmato digitalmente con HMAC-SHA256 usando una chiave segreta (`JWT_SECRET_KEY`). Qualsiasi modifica al contenuto rende la firma non valida.

### 2.3 Validazione di Ogni Richiesta

```
CLIENT                          MIDDLEWARE JWT                   DATABASE
  │                                    │                              │
  │  GET /films                        │                              │
  │  Authorization: Bearer <JWT>       │                              │
  │ ─────────────────────────────────> │                              │
  │                                    │                              │
  │                                    │  Verifica firma JWT          │
  │                                    │  Verifica scadenza           │
  │                                    │                              │
  │                                    │  Carica utente da DB         │
  │                                    │  (o da cache)                │
  │                                    │ ────────────────────────────>│
  │                                    │                              │
  │                                    │  SELECT AuthVersion,         │
  │                                    │  IsDisabled,                 │
  │                                    │  PasswordChangedAtUtc        │
  │                                    │  FROM Utenti WHERE Id=42     │
  │                                    │ <────────────────────────────│
  │                                    │                              │
  │                                    │  Controlli OnTokenValidated: │
  │                                    │  ✓ auth_version match?       │
  │                                    │  ✓ utente non disabilitato?  │
  │                                    │  ✓ password non cambiata     │
  │                                    │    dopo emissione token?     │
  │                                    │                              │
  │  <── 200 OK (o 401 se fallisce) ── │                              │
```

**Questo significa che revocare un token e istantaneo**: basta incrementare `AuthVersion` e TUTTI i token esistenti di quell'utente vengono rifiutati alla prossima richiesta.

### 2.4 Perche Due Token (Access + Refresh)?

| Aspetto | Access Token | Refresh Token |
|---------|-------------|---------------|
| **Durata** | 15 minuti | 7 giorni |
| **Formato** | JWT (auto-contenuto) | Stringa random (opaca) |
| **Validazione** | Firma crittografica | Ricerca nel database |
| **Dove vive** | sessionStorage (frontend) | localStorage (frontend) |
| **Revoca** | Via AuthVersion | Via NULL nel DB + AuthVersion |
| **Scopo** | Autorizzare richieste API | Ottenere nuovi Access Token |

Il doppio token permette di:
- **Non interrogare il DB a ogni richiesta** (l'Access Token e auto-contenuto, tranne il check OnTokenValidated)
- **Revocare sessioni in modo mirato** (basta cancellare il Refresh Token dal DB)
- **Limitare il danno di un Access Token rubato** (scade in 15 minuti)

---

## 3. Login con Email e Password

### 3.1 Flusso Completo

```
FRONTEND (login.html)                  BACKEND (/auth/login)              DATABASE
       │                                      │                              │
       │  1. Utente compila il form          │                              │
       │     email + password                │                              │
       │                                      │                              │
       │  2. POST /auth/login                │                              │
       │     { "email": "...",               │                              │
       │       "password": "..." }           │                              │
       │ ──────────────────────────────────> │                              │
       │                                      │                              │
       │                                      │  3. Cerca utente per         │
       │                                      │     NormalizedEmail          │
       │                                      │ ────────────────────────────>│
       │                                      │ <────────────────────────────│
       │                                      │                              │
       │                                      │  4. Verifiche:               │
       │                                      │     ✓ Utente esiste?         │
       │                                      │     ✓ LocalCredentials?      │
       │                                      │     ✓ Non disabilitato?      │
       │                                      │     ✓ Password corretta?     │
       │                                      │       (BCrypt.Verify)        │
       │                                      │                              │
       │                                      │  5. Se tutto OK:             │
       │                                      │     - Aggiorna LastLoginAtUtc│
       │                                      │     - Aggiorna LastLoginProv │
       │                                      │     - Genera Access Token    │
       │                                      │     - Genera Refresh Token   │
       │                                      │     - Salva RefreshToken     │
       │                                      │       nel DB                 │
       │                                      │ ────────────────────────────>│
       │                                      │                              │
       │                                      │  6. Registra audit log       │
       │                                      │     EventType: "LoginSuccess"│
       │                                      │ ────────────────────────────>│
       │                                      │                              │
       │  7. Risposta:                       │                              │
       │     { "accessToken": "...",         │                              │
       │       "refreshToken": "...",        │                              │
       │       "utente": { ... } }           │                              │
       │ <────────────────────────────────── │                              │
       │                                      │                              │
       │  8. Frontend salva in               │                              │
       │     localStorage e redirect         │                              │
       │                                      │                              │
```

### 3.2 Sicurezza della Password

- **Mai salvata in chiaro**: la password viene hashata con BCrypt (algoritmo lento, resistente a brute force)
- **Verifica**: `BCrypt.Verify(passwordInChiaro, hashSalvatoNelDB)`
- **Complessita minima**: 8 caratteri, almeno 1 maiuscola, 1 minuscola, 1 numero, 1 carattere speciale

```
Password inserita   →   BCrypt Hash   →   Salvato nel DB
"MiaPass123!"       →   $2a$11$K2...  →   Utente.PasswordHash
```

### 3.3 Casi di Errore

| Scenario | HTTP Status | Messaggio |
|----------|-------------|-----------|
| Email non trovata | 401 | "Email o password non validi." |
| Password errata | 401 | "Email o password non validi." |
| Account disabilitato | 401 | "Account disabilitato. Contatta l'amministratore." |
| Account social-only | 401 | "Questo account usa il login social. Usa Google o Microsoft." |
| Rate limit superato | 429 | "Troppi tentativi. Riprova tra 60 secondi." |

**Nota**: il messaggio per "email non trovata" e "password errata" e volutamente identico, per non rivelare se un'email e registrata (anti-enumerazione).

---

## 4. Login Social: Google e Microsoft

### 4.1 Il Protocollo OpenID Connect (OIDC)

OIDC e uno strato di identita sopra OAuth 2.0. Il flusso utilizzato e l'**Authorization Code Flow + PKCE**:

```
FRONTEND            BACKEND                  GOOGLE / MICROSOFT
  │                    │                              │
  │  1. Clicca        │                              │
  │  "Login Google"   │                              │
  │ ─────────────────>│                              │
  │                    │                              │
  │                    │  2. Genera state (GUID)      │
  │                    │     Salva ExternalAuthState  │
  │                    │     (state, returnUrl, TTL)  │
  │                    │                              │
  │  3. Redirect a    │                              │
  │     Google OAuth  │                              │
  │ <─────────────────│                              │
  │                    │                              │
  │  4. Pagina di     │                              │
  │     consenso      │                              │
  │     Google        │                              │
  │ ─────────────────────────────────────────────────>│
  │                    │                              │
  │                    │      5. Utente autorizza     │
  │                    │      Google reindirizza      │
  │                    │      a /auth/external/       │
  │                    │      callback?code=xxx       │
  │                    │      &state=yyy              │
  │  6. Redirect a    │                              │
  │     backend       │                              │
  │     con code      │                              │
  │ ─────────────────>│                              │
  │                    │                              │
  │                    │  7. Valida state             │
  │                    │     (cerca in                │
  │                    │     ExternalAuthState)       │
  │                    │                              │
  │                    │  8. Scambia code per         │
  │                    │     token (server-to-server) │
  │                    │ ─────────────────────────────>│
  │                    │ <─────────────────────────────│
  │                    │     id_token + access_token  │
  │                    │                              │
  │                    │  9. Valida id_token:         │
  │                    │     - firma (JWKS)           │
  │                    │     - issuer (accounts.      │
  │                    │       google.com)            │
  │                    │     - audience (client ID)   │
  │                    │     - expiry                  │
  │                    │     - email_verified = true  │
  │                    │                              │
  │                    │  10. Anti-replay:            │
  │                    │      hash code → salva in    │
  │                    │      ExternalAuthExchangeCode│
  │                    │      (gia usato? → errore)   │
  │                    │                              │
  │                    │  11. Crea/linka utente       │
  │                    │      (vedi 4.2)              │
  │                    │                              │
  │  12. Redirect a   │                              │
  │      frontend con │                              │
  │      token in URL │                              │
  │ <─────────────────│                              │
  │                    │                              │
  │  13. social-login-│                              │
  │      complete.html│                              │
  │      salva token  │                              │
  │      e redirect    │                              │
  │                    │                              │
```

### 4.2 Linking Utenti: La Logica di Associazione

Quando un utente fa login social, il backend deve decidere se:
- **A)** L'utente e gia conosciuto (ha gia fatto login con questo provider) → login diretto
- **B)** L'utente ha la stessa email ma non ha mai usato questo provider → linking automatico
- **C)** L'utente e completamente nuovo → creazione account

```
Il backend riceve i claims dal provider:
  sub: "1234567890"
  email: "mario.rossi@gmail.com"
  email_verified: true

                       ┌─────────────────────────┐
                       │ Cerca UserExternalLogin │
                       │ (Provider='google',     │
                       │  ProviderKey='123456..')│
                       └───────────┬─────────────┘
                                   │
                    ┌──────────────┼──────────────┐
                    │ Trovato      │ Non trovato   │
                    ▼              ▼               │
           ┌──────────────┐  ┌─────────────────┐  │
           │ LOGIN DIRETTO│  │ Cerca Utente per │  │
           │ (caso A)     │  │ NormalizedEmail  │  │
           └──────────────┘  └────────┬────────┘  │
                                      │            │
                         ┌────────────┼────────┐   │
                         │ Trovato    │        │   │
                         ▼            ▼        ▼   │
                  ┌────────────┐  ┌────────────────┐
                  │ Verifica   │  │ NUOVO ACCOUNT  │
                  │ ruolo      │  │ (caso C)       │
                  └──┬─────────┘  │                │
                     │            │ Crea Utente:   │
          ┌──────────┼──────┐    │ Email, Nome,    │
          │ utente   │admin │    │ Ruolo=utente,   │
          │          │o pw  │    │ LocalCredentials│
          ▼          ▼      │    │ =false,         │
   ┌──────────┐ ┌────────┐ │    │ PasswordHash=   │
   │ LINKING  │ │RIFIUTO!│ │    │ NULL            │
   │ AUTOMAT. │ │        │ │    │                 │
   │ (caso B) │ │"Usa    │ │    │ Crea record     │
   │          │ │creden- │ │    │ UserExternalLogin│
   │ Crea     │ │ziali   │ │    └────────────────┘
   │ record   │ │locali" │ │
   │ UserExt. │ └────────┘ │
   │ Login    │            │
   │          │            │
   │ Account  │            │
   │ diventa  │            │
   │ Hybrid   │            │
   └──────────┘            │
                           │
```

**Regola fondamentale**: PowerUser e Admin **NON possono fare login social**. Se l'email corrisponde a un admin, il login viene rifiutato con un messaggio che invita a usare email e password.

### 4.3 Identificazione Microsoft Multi-Tenant

Microsoft ha un sistema di identita particolare: lo stesso `sub` (chiamato `oid`) puo esistere in tenant diversi. Per questo si usa l'identificazione composta `(tid, oid)`:

```
Account personale Microsoft:
  tid = 9188040d-6c67-4c5b-b112-36a304b66dad  (tenant consumer)
  oid = a1b2c3d4-e5f6-...                      (object ID utente)

Account aziendale (Contoso Ltd):
  tid = 7b3f8c12-9d4e-...                      (tenant Contoso)
  oid = f7e8d9c0-1234-...                      (object ID dipendente)

  → Stesso utente, tenant diversi → due record UserExternalLogin separati
  → Unique index: (Provider, TenantId, ProviderKey)
```

### 4.4 Anti Open-Redirect

Quando il backend riceve un `returnUrl` (dove rimandare l'utente dopo il login), lo valida rigorosamente:

```csharp
bool IsValidReturnUrl(string url)
{
    // Solo URL relativi (es. "/index.html")
    if (url.StartsWith("/")) return true;

    // URL assoluti: solo stesso host
    if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
    {
        return uri.Host == "localhost" || uri.Host == "cinebase.example.com";
    }

    return false;  // Tutto il resto: BLOCCATO
}
```

Questo impedisce attacchi di phishing dove un malicious actor proverebbe a far accettare:
- `?returnUrl=https://evil-site.com` → BLOCCATO
- `?returnUrl=//evil-site.com` → BLOCCATO  
- `?returnUrl=/\evil-site.com` → BLOCCATO (inizia con `/` ma e un URL camuffato)
- `?returnUrl=/profile.html` → OK

---

## 5. Account Ibridi: Social + Password

### 5.1 Cos'e un Account Ibrido

Un account ibrido e un utente che ha **sia** una password locale **sia** uno o piu provider social collegati. Puo accedere in qualsiasi modo:

```
Mario Rossi si e registrato con email/password.
Poi ha collegato il suo account Google.

Ora Mario puo fare login:
  1. Con email "mario.rossi@gmail.com" + password  ✓
  2. Con il pulsante "Accedi con Google"            ✓
```

### 5.2 Come Collegare un Provider Social

```
FRONTEND (profile.html)                     BACKEND
       │                                        │
       │  1. Utente autenticato clicca          │
       │     "Collega account Google"           │
       │                                        │
       │  2. GET /auth/external/google          │
       │     ?mode=link&returnUrl=/profile.html │
       │ ──────────────────────────────────────>│
       │                                        │
       │  3. Stesso flusso OIDC, ma con         │
       │     mode=link. Il backend sa che       │
       │     l'utente e gia autenticato.        │
       │                                        │
       │  ... redirect a Google ...             │
       │  ... autorizzazione ...                │
       │  ... callback ...                      │
       │                                        │
       │  4. Backend crea record                │
       │     UserExternalLogin collegato        │
       │     all'utente corrente                │
       │                                        │
       │  5. Se l'utente era social-only,       │
       │     dopo aver impostato una password   │
       │     diventa Hybrid                     │
       │                                        │
```

### 5.3 Come Scollegare un Provider

Un utente puo rimuovere un provider social dal proprio account:

- Endpoint: `DELETE /auth/me/external-logins/{id}`
- Vincolo: non puoi scollegare l'ultimo metodo di accesso
  - Se l'utente ha `LocalCredentialsEnabled == false` e un solo provider social → errore
  - Soluzione: prima imposta una password, poi scollega il social

---

## 6. Gestione Password

### 6.1 Cambio Password (Utente Autenticato)

```
Endpoint: POST /auth/me/change-password

Input:
  {
    "currentPassword": "VecchiaPass123!",
    "newPassword": "NuovaPass456!"
  }

Cosa succede:
  1. Verifica che la password corrente sia corretta (BCrypt.Verify)
  2. Valida la nuova password (complessita)
  3. Verifica che nuova password != vecchia password
  4. Hash della nuova password
  5. Aggiorna Utente.PasswordHash
  6. Aggiorna Utente.PasswordChangedAtUtc = DateTime.UtcNow
  7. Incrementa Utente.AuthVersion (tutti i token vengono invalidati!)
  8. Cancella Utente.RefreshToken (tutte le sessioni terminate)
  9. Genera nuovi Access Token + Refresh Token
  10. Registra audit: EventType = "PasswordChanged"
  11. Invia email di notifica: "La tua password e stata cambiata"

Risultato: TUTTI i dispositivi vengono disconnessi.
           Solo la sessione corrente riceve i nuovi token.
```

### 6.2 Recupero Password (Password Dimenticata)

E un flusso in due fasi:

#### Fase 1 — Richiesta Reset

```
Endpoint: POST /auth/forgot-password

Input: { "email": "mario.rossi@email.it" }

Cosa succede (anti-enumerazione):
  - Il backend cerca l'email nel DB
  - Se trovata E l'utente ha credenziali locali:
      1. Genera token crittografico random (64 byte)
      2. Calcola SHA256(token) → lo salva in AccountActionToken
      3. Invia email a mario.rossi@email.it con link:
         https://cinebase.local/reimposta-password.html
           ?token=<token_raw_base64url>
           &email=mario.rossi%40email.it
  - Se NON trovata:
      NON invia email, NON registra nulla
  - In ENTRAMBI i casi:
      Risponde 200 OK con messaggio identico:
      "Se l'email e associata a un account, riceverai un link di recupero."
      Il tempo di risposta e costante (evita timing attack).
```

**Perche rispondere sempre 200 OK?** Perche se rispondessi 404 "Email non trovata", un attaccante potrebbe usare l'endpoint per enumerare gli utenti registrati. Rispondendo sempre allo stesso modo, non si rivela nulla.

#### Fase 2 — Impostazione Nuova Password

```
La mail arriva a Mario:

  Oggetto: Recupero Password - CineBase
  Corpo:
    Ciao Mario,
    Hai richiesto il recupero della password.
    Clicca qui per impostare una nuova password:
    https://cinebase.local/reimposta-password.html?token=AbC123...&email=...

    Questo link scade tra 1 ora.
    Se non hai richiesto tu questa operazione, ignora questa email.

Mario clicca il link e arriva su reimposta-password.html

Endpoint: POST /auth/reset-password

Input: { "email": "...", "token": "AbC123...", "newPassword": "NuovaPass456!" }

Cosa succede:
  1. Cerca utente per email
  2. Calcola SHA256(token_raw) e cerca AccountActionToken
     con TokenHash corrispondente
  3. Verifiche:
     ✓ Token trovato?
     ✓ TokenType == "PasswordReset"?
     ✓ ConsumedAtUtc == NULL? (non ancora usato)
     ✓ ExpiresAtUtc > DateTime.UtcNow? (non scaduto)
  4. Marca token come consumato (ConsumedAtUtc = now)
     → single-use garantito
  5. Aggiorna PasswordHash con la nuova password
  6. Aggiorna PasswordChangedAtUtc
  7. Incrementa AuthVersion (invalida tutti i token)
  8. Registra audit: EventType = "PasswordReset"
  9. Restituisce nuovi Access Token + Refresh Token
```

### 6.3 Setup Password per Account Social-Only

Un utente che si e registrato solo con Google non ha una password. Puo impostarne una in due modi:

#### Metodo 1: Da Profilo (Autenticato)

```
L'utente e gia loggato (con Google).
Va su profile.html → "Imposta una password"

Endpoint: POST /auth/me/request-password-setup

Il backend:
  1. Verifica che l'utente abbia LocalCredentialsEnabled == false
  2. Crea AccountActionToken (TokenType = "PasswordSetup", TTL = 24 ore)
  3. Invia email con link di setup

L'utente clicca il link → setup-password.html

Endpoint: POST /auth/setup-password

Input: { "email": "...", "token": "...", "newPassword": "..." }

Il backend:
  1. Valida il token (stessa logica del reset)
  2. Imposta PasswordHash
  3. Imposta LocalCredentialsEnabled = true
  4. L'account diventa Hybrid! (social + password)
  5. Registra audit: EventType = "PasswordSetup"
```

#### Perche il Doppio Passaggio (Richiesta + Setup)?

Per sicurezza: anche se l'utente e gia autenticato, il setup della password richiede la verifica del possesso dell'email. Se un utente malintenzionato avesse accesso a una sessione aperta su un computer pubblico, non potrebbe impostare una password senza accesso alla casella email.

---

## 7. Invalidazione Token e Session Security

### 7.1 AuthVersion: Il Cuore dell'Invalidazione

Ogni utente ha un numero intero `AuthVersion` che parte da 1 e viene incrementato ogni volta che si verifica un evento di sicurezza:

| Evento | AuthVersion | RefreshToken | Tutti i token |
|--------|-------------|--------------|---------------|
| Cambio password | ++ | NULL | Invalidati |
| Reset password | ++ | NULL | Invalidati |
| Setup password | ++ | NULL | Invalidati |
| Modifica ruolo (admin) | ++ | NULL | Invalidati |
| Disabilitazione account | ++ | NULL | Invalidati |
| Logout (tutti i dispositivi) | ++ | NULL | Invalidati |
| Revoca forzata admin | ++ | NULL | Invalidati |

### 7.2 Cosa Succede in Pratica

```
Mario ha 3 dispositivi:
  - Telefono  (Access Token A, emesso con AuthVersion=3)
  - Computer  (Access Token B, emesso con AuthVersion=3)
  - Tablet    (Access Token C, emesso con AuthVersion=3)

Mario cambia password dal telefono:
  → AuthVersion diventa 4
  → RefreshToken cancellato
  → Telefono riceve NUOVO Access Token con AuthVersion=4

Prossima richiesta dal Computer (Token B con AuthVersion=3):
  → OnTokenValidated controlla DB
  → Utente.AuthVersion (4) != Token.auth_version (3)
  → 401 Unauthorized → redirect a login

Prossima richiesta dal Tablet (Token C con AuthVersion=3):
  → Stesso destino: 401 → redirect a login
```

### 7.3 Refresh Token Revocabili

Il Refresh Token e una stringa random di 64 byte salvata nel DB. Revocarlo e semplice:
- `Utente.RefreshToken = NULL`
- `Utente.RefreshTokenExpiryTime = NULL`

Questo invalida immediatamente la possibilita di ottenere nuovi Access Token, anche se il Refresh Token non e ancora scaduto.

### 7.4 Logout Selettivo vs Globale

```
POST /auth/logout
  Body: { "allDevices": false }   ← default
  Effetto: invalida SOLO il refresh token corrente
  Le altre sessioni rimangono attive (fino a scadenza Access Token)

POST /auth/logout
  Body: { "allDevices": true }
  Effetto: incrementa AuthVersion + cancella RefreshToken
  TUTTE le sessioni vengono terminate immediatamente
```

---

## 8. Ruoli e Autorizzazioni (RBAC)

### 8.1 I Tre Ruoli

| Ruolo | Valore DB | Descrizione |
|-------|-----------|-------------|
| **Admin** | `admin` | Accesso totale: gestione utenti, configurazione sistema, audit |
| **PowerUser** | `power_user` | Operatore cinema: CRUD film/registi/proiezioni/sale, ricariche, validazione biglietti |
| **Utente** | `utente` | Utente standard: acquisto biglietti, prenotazioni, profilo personale |

### 8.2 Come Funziona l'Autorizzazione

```
1. L'utente si autentica → riceve un JWT con claim "role": "utente"

2. L'utente chiama GET /auth/admin/utenti (endpoint AdminOnly)

3. Il middleware di autorizzazione controlla:
   - L'endpoint ha [Authorize(Roles = "admin")] ?
   - Il JWT ha claim "role" = "admin" ?
   - Se no → 403 Forbidden

4. L'utente con ruolo "utente" riceve 403
   → Il frontend mostra "Accesso negato"
```

### 8.3 Regole di Sicurezza per i Ruoli

```
┌────────────────────────────────────────────────────────────────┐
│                    REGOLE RBAC                                 │
│                                                                 │
│  1. SOLO ADMIN puo promuovere/degradare ruoli                  │
│                                                                 │
│  2. SOCIAL-ONLY non puo essere promosso a PowerUser/Admin      │
│     Deve prima impostare una password.                          │
│                                                                 │
│  3. ULTIMO ADMIN non puo essere degradato o eliminato.          │
│     Ci deve sempre essere almeno un admin nel sistema.          │
│                                                                 │
│  4. SELF-DEGRADAZIONE bloccata.                                │
│     Un admin non puo degradare se stesso.                       │
│                                                                 │
│  5. POWERUSER/ADMIN non possono fare login social.             │
│     Solo autenticazione locale (email + password).              │
│                                                                 │
│  6. PROMOZIONE immediata: AuthVersion++ e tutte le             │
│     sessioni invalidate. Il nuovo ruolo e attivo subito.        │
│                                                                 │
└────────────────────────────────────────────────────────────────┘
```

---

## 9. Gestione Utenti Amministratore

### 9.1 Dashboard Utenti

L'admin puo vedere e gestire tutti gli utenti da `utenti.html`:

```
┌─────────────────────────────────────────────────────────────────┐
│  GESTIONE UTENTI                                                │
│                                                                  │
│  Cerca: [________________]  Ruolo: [Tutti ▾]  Stato: [Tutti ▾] │
│                                                                  │
│  ┌────┬──────────┬─────────────────────┬─────────┬───────────┐  │
│  │ ID │ Nome     │ Email               │ Ruolo   │ Azioni    │  │
│  ├────┼──────────┼─────────────────────┼─────────┼───────────┤  │
│  │ 1  │ Admin    │ admin@cinebase.it   │ admin ▾ │ [Dettaglio]│  │
│  │ 42 │ Mario R. │ mario@email.it      │ utente ▾│ [Disabil.]│  │
│  │ 99 │ Laura B. │ laura@email.it [G]  │ utente ▾│ [Elimina] │  │
│  └────┴──────────┴─────────────────────┴─────────┴───────────┘  │
│                                                                  │
│  [G] = account Google collegato                                  │
│  [M] = account Microsoft collegato                               │
│                                                                  │
│  ◄ 1  2  3 ... 8  ►    (150 utenti totali)                      │
│                                                                  │
│  [+ Invita Nuovo Utente]                                        │
└─────────────────────────────────────────────────────────────────┘
```

### 9.2 Operazioni Disponibili per l'Admin

| Azione | Descrizione | Cosa succede |
|--------|-------------|--------------|
| **Visualizza dettaglio** | Vedi tutte le info di sicurezza dell'utente | Audit log, provider collegati, ultimo accesso, stato account |
| **Cambia ruolo** | Promuovi/degradi l'utente | AuthVersion++, tutte le sessioni invalidate |
| **Disabilita** | Blocca l'account | IsDisabled=true, AuthVersion++, non puo piu fare login |
| **Riabilita** | Sblocca l'account | IsDisabled=false, AuthVersion++ |
| **Forza reset password** | Obbliga l'utente a cambiare password | Invia email di reset, token esistenti invalidati |
| **Elimina** | Cancella l'account e tutti i dati | Soft-delete con audit finale |

### 9.3 Invito Amministratore

L'admin puo invitare un nuovo PowerUser o Admin senza password:

```
POST /auth/admin/invite
{
  "email": "nuovo.operatore@cinebase.it",
  "ruolo": "power_user",
  "nome": "Nuovo",
  "cognome": "Operatore"
}

Cosa succede:
  1. Verifica che l'email non sia gia registrata
  2. Crea utente con:
     - PasswordHash = NULL
     - LocalCredentialsEnabled = false
     - IsDisabled = true (attivo solo dopo setup password)
     - Ruolo = "power_user" (o "admin")
  3. Crea AccountActionToken (TokenType = "AdminInvite", TTL = 72 ore)
  4. Invia email con link di setup password

Il nuovo operatore riceve la mail:
  "Sei stato invitato come PowerUser su CineBase.
   Clicca qui per impostare la tua password e attivare l'account."

Dopo il setup password:
  - LocalCredentialsEnabled = true
  - IsDisabled = false
  - Puo fare login con email + password
  - Ha immediatamente i privilegi del ruolo assegnato
```

---

## 10. Audit di Sicurezza

### 10.1 Cosa Viene Registrato

Ogni evento di sicurezza viene salvato nella tabella `UserSecurityAuditLog`:

```
┌──────────────────────────────────────────────────────────────┐
│               EVENTI REGISTRATI                              │
│                                                               │
│  Autenticazione:                                              │
│    - LoginSuccess     (provider, IP, User-Agent)             │
│    - LoginFailed      (provider, IP, motivo)                 │
│    - TokenRefreshed   (IP)                                   │
│    - Logout           (tutti dispositivi?)                   │
│                                                               │
│  Password:                                                    │
│    - PasswordChanged                                         │
│    - PasswordReset    (da forgot password)                   │
│    - PasswordSetup    (account social → hybrid)              │
│    - PasswordResetForced (da admin)                          │
│                                                               │
│  Account:                                                     │
│    - SocialLinked     (provider)                             │
│    - SocialUnlinked   (provider)                             │
│    - RoleChanged      (ruolo precedente → nuovo)             │
│    - AccountDisabled  (da admin)                             │
│    - AccountEnabled   (da admin)                             │
│    - AccountDeleted   (dati utente)                          │
│    - AdminInvite      (email, ruolo)                         │
│                                                               │
└──────────────────────────────────────────────────────────────┘
```

### 10.2 Esempio di Record Audit

```json
{
  "id": 15042,
  "utenteId": 42,
  "eventType": "PasswordChanged",
  "provider": "local",
  "ipAddress": "192.168.1.100",
  "userAgent": "Mozilla/5.0 (Windows NT 10.0...) Chrome/125...",
  "details": null,
  "createdAtUtc": "2026-05-10T14:32:15Z"
}
```

### 10.3 Retention e Pulizia

| Categoria eventi | Retention |
|------------------|-----------|
| Login, Refresh, Logout | 90 giorni |
| Password, Social | 90 giorni |
| Ruolo, Disable/Enable, Delete, Invite | 365 giorni |

Un job settimanale pulisce automaticamente i record scaduti.

---

## 11. Rate Limiting e Protezione Anti-Abuso

### 11.1 Limiti per Endpoint

| Endpoint | Limite | Finestra | Scopo |
|----------|--------|----------|-------|
| `POST /auth/login` | 5 richieste | 1 minuto per IP | Anti brute force |
| `POST /auth/login` | 10 richieste | 15 minuti per email | Anti password spraying |
| `POST /auth/register` | 3 richieste | 15 minuti per IP | Anti mass registration |
| `POST /auth/forgot-password` | 3 richieste | 15 minuti per IP | Anti enumerazione email |
| `POST /auth/reset-password` | 5 richieste | 15 minuti per IP | Anti brute force token |
| `POST /auth/refresh` | 10 richieste | 1 minuto per IP | Anti refresh abuse |

### 11.2 Cosa Vede l'Utente

Quando il limite viene superato:

```
HTTP 429 Too Many Requests

Headers:
  X-RateLimit-Limit: 5
  X-RateLimit-Remaining: 0
  X-RateLimit-Reset: 1715328600
  Retry-After: 45

Body:
  {
    "message": "Troppi tentativi. Riprova tra 45 secondi.",
    "retryAfterSeconds": 45
  }
```

### 11.3 Brute Force Protection

Dopo tentativi di login falliti ripetuti:

```
Tentativo 1-3:    Risposta immediata
Tentativo 4-5:    Ritardo 500ms
Tentativo 6-10:   Ritardo 2000ms
Tentativo 11+:    Ritardo 5000ms

Dopo 5 tentativi falliti → Email di alert:
  "Rilevati tentativi di accesso sospetti sul tuo account CineBase.
   Se non eri tu, ti consigliamo di cambiare password."
```

---

## 12. Flussi Completi Step-by-Step

### 12.1 Registrazione + Primo Accesso

```
1. Utente va su /register.html
2. Compila: Nome, Cognome, Telefono, Email, Password, Conferma Password
3. Frontend valida:
   - Email formato corretto
   - Password >= 8 char, 1 maiuscola, 1 minuscola, 1 numero, 1 speciale
   - Password = Conferma Password
4. POST /auth/register
   {
     "nome": "Mario",
     "cognome": "Rossi",
     "email": "mario.rossi@email.it",
     "password": "MiaPass123!",
     "telefono": "+39 333 1234567"
   }
5. Backend:
   - Verifica email non gia registrata (NormalizedEmail)
   - Hash password con BCrypt
   - Crea Utente con:
       Ruolo = "utente"
       LocalCredentialsEnabled = true
       AuthVersion = 1
       SecurityStamp = nuovo GUID
       CreatedAtUtc = DateTime.UtcNow
       NormalizedEmail = "MARIO.ROSSI@EMAIL.IT"
   - Salva nel DB
6. Risposta: 201 Created
7. Frontend mostra "Registrazione completata!"
8. Redirect a /login.html dopo 2 secondi

9. Utente inserisce email e password su /login.html
10. POST /auth/login
11. Backend:
    - Cerca per NormalizedEmail
    - BCrypt.Verify(password, utente.PasswordHash)
    - Genera JWT (Access Token, 15 min)
    - Genera Refresh Token (random 64 byte, 7 giorni)
    - Salva RefreshToken + RefreshTokenExpiryTime nel DB
    - Aggiorna LastLoginAtUtc, LastLoginProvider = "local"
    - Registra audit: LoginSuccess
12. Risposta: { accessToken, refreshToken, utente }
13. Frontend:
    - Salva accessToken in sessionStorage
    - Salva refreshToken in localStorage
    - Salva utente in localStorage
    - Redirect alla homepage o callback URL
```

### 12.2 Login Social (Primo Accesso con Google)

```
1. Utente anonimo clicca "Accedi con Google" su /login.html
2. GET /auth/external/google?mode=login&returnUrl=/index.html
3. Backend:
   - Crea ExternalAuthState con state GUID + returnUrl + TTL 10 min
   - Costruisce URL Google OAuth:
     https://accounts.google.com/o/oauth2/v2/auth?
       client_id=...&
       redirect_uri=https://cinebase.local/auth/external/callback&
       response_type=code&
       scope=openid+email+profile&
       state=<GUID>
4. Frontend reindirizza a Google

5. [Su Google] Utente seleziona account e autorizza
6. Google reindirizza a:
   https://cinebase.local/auth/external/callback?code=AbC123&state=<GUID>

7. Backend:
   - Valida state (cerca ExternalAuthState)
   - Scambia code per token (server-to-server POST a Google)
   - Valida id_token (firma, issuer, audience, expiry, email_verified=true)
   - Estrae claims: sub=123456, email=mario.rossi@gmail.com, given_name=Mario, ...
   - Salva code hash in ExternalAuthExchangeCode (anti-replay)
   - Cerca UserExternalLogin: (google, 123456) → non trovato
   - Cerca Utente per NormalizedEmail: MARIO.ROSSI@GMAIL.COM → non trovato
   - Crea nuovo Utente:
       Email = "mario.rossi@gmail.com"
       Nome = "Mario"
       Cognome = "Rossi"
       Ruolo = "utente"
       LocalCredentialsEnabled = false
       PasswordHash = NULL
       EmailVerified = true
       AuthVersion = 1
   - Crea UserExternalLogin(provider=google, providerKey=123456)
   - Genera Access Token + Refresh Token
   - Registra audit: LoginSuccess, SocialLinked

8. Redirect a:
   /social-login-complete.html
     #access_token=xxx
     &refresh_token=yyy
     &user=zzz
     &return_url=/index.html

9. social-login-complete.html:
   - Legge parametri da URL fragment
   - Salva token in storage
   - Redirect a /index.html
```

### 12.3 Recupero Password Completo

```
1. Utente dimentica la password, va su /recupera-password.html
2. Inserisce email: "mario.rossi@email.it"
3. POST /auth/forgot-password { "email": "mario.rossi@email.it" }
4. Backend:
   - Cerca per NormalizedEmail = "MARIO.ROSSI@EMAIL.IT"
   - Trovato, LocalCredentialsEnabled = true
   - Genera token_raw = crittografico 64 byte
   - SHA256(token_raw) = hash salvato in AccountActionToken
   - AccountActionToken:
       UtenteId = 42
       TokenType = "PasswordReset"
       ExpiresAtUtc = +1 ora
   - Invia email a mario.rossi@email.it
   - Risponde 200 OK (messaggio generico)
5. Frontend mostra: "Se l'email e associata a un account, riceverai un link."

6. Mario apre la mail, clicca il link
7. Arriva su /reimposta-password.html?token=AbC...&email=mario.rossi@email.it
8. Compila: nuova password + conferma
9. POST /auth/reset-password
   { "email": "mario.rossi@email.it",
     "token": "AbC...",
     "newPassword": "NuovaPass456!" }

10. Backend:
    - token_hash = SHA256("AbC...")
    - Cerca AccountActionToken per hash:
      ✓ Trovato
      ✓ TokenType = "PasswordReset" 
      ✓ ConsumedAtUtc = NULL (non usato)
      ✓ ExpiresAtUtc > now (non scaduto)
    - Marca ConsumedAtUtc = now (singolo uso!)
    - BCrypt.Hash("NuovaPass456!") → nuovo PasswordHash
    - PasswordChangedAtUtc = now
    - AuthVersion = 2 (era 1)
    - RefreshToken = NULL
    - Audit: PasswordReset
    - Genera nuovi Access + Refresh Token

11. Risposta: { accessToken, refreshToken, utente }
12. Frontend mostra "Password reimpostata con successo!"
13. Redirect a /login.html dopo 3 secondi
    (oppure auto-login con i nuovi token)
```

### 12.4 Admin Invita PowerUser

```
1. Admin va su /utenti.html, clicca "Invita Nuovo Utente"
2. Compila modale:
   - Email: nuovo.operatore@cinebase.it
   - Ruolo: PowerUser
   - Nome: Nuovo
   - Cognome: Operatore
3. POST /auth/admin/invite
   {
     "email": "nuovo.operatore@cinebase.it",
     "ruolo": "power_user",
     "nome": "Nuovo",
     "cognome": "Operatore"
   }
4. Backend:
   - Verifica email non registrata
   - Crea Utente:
       Ruolo = "power_user"
       LocalCredentialsEnabled = false
       PasswordHash = NULL
       IsDisabled = true
       AuthVersion = 1
   - Crea AccountActionToken (AdminInvite, 72 ore)
   - Invia email di invito
   - Audit: AdminInvite
5. Admin vede conferma

6. Nuovo Operatore riceve email:
   "Sei stato invitato come PowerUser su CineBase.
    Clicca qui per impostare la tua password:
    https://cinebase.local/setup-password.html?token=...&email=..."

7. Nuovo Operatore clicca il link
8. Compila password su /setup-password.html
9. POST /auth/setup-password
10. Backend:
    - Valida token (stessa logica reset)
    - Imposta PasswordHash
    - LocalCredentialsEnabled = true
    - IsDisabled = false
    - AuthVersion = 2 (invalida il token di setup)
    - Audit: PasswordSetup
    - Genera nuovi token (login automatico)
11. Nuovo Operatore e ora attivo con ruolo PowerUser
```

---

## 13. Diagramma dell'Architettura

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        ARCHITETTURA AUTH CINEBASE                            │
│                                                                              │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │                         FRONTEND (FilmFrontend)                       │   │
│  │                                                                       │   │
│  │  ┌──────────┐ ┌─────────────┐ ┌───────────────┐ ┌─────────────────┐ │   │
│  │  │login.html│ │register.html│ │profile.html   │ │utenti.html      │ │   │
│  │  │          │ │             │ │               │ │(admin only)     │ │   │
│  │  └────┬─────┘ └──────┬──────┘ └───────┬───────┘ └────────┬────────┘ │   │
│  │       │              │               │                   │          │   │
│  │  ┌────┴──────────────┴───────────────┴───────────────────┴────┐     │   │
│  │  │                     auth-service.js                        │     │   │
│  │  │  login() | register() | logout() | refreshToken()          │     │   │
│  │  │  initiateSocialLogin() | changePassword()                  │     │   │
│  │  │  forgotPassword() | resetPassword() | setupPassword()      │     │   │
│  │  │  searchUsers() | changeRole() | disableUser() | invite()   │     │   │
│  │  └────────────────────────────┬───────────────────────────────┘     │   │
│  │                               │                                      │   │
│  │  ┌────────────────────────────┴───────────────────────────────┐     │   │
│  │  │                     api-client.js                           │     │   │
│  │  │  Auto-Authorization header, auto-refresh on 401,           │     │   │
│  │  │  rate-limit handling (429 retry-after)                     │     │   │
│  │  └────────────────────────────┬───────────────────────────────┘     │   │
│  └───────────────────────────────┼─────────────────────────────────────┘   │
│                                  │                                          │
│                    HTTP/HTTPS con JWT Bearer                                 │
│                                  │                                          │
│  ┌───────────────────────────────┼─────────────────────────────────────┐   │
│  │                         BACKEND (FilmAPI)                           │   │
│  │                               │                                      │   │
│  │  ┌────────────────────────────┴───────────────────────────────┐    │   │
│  │  │                   MIDDLEWARE PIPELINE                       │    │   │
│  │  │                                                             │    │   │
│  │  │  RateLimit → HttpsRedir → CSP → Auth → OnTokenValidated →  │    │   │
│  │  │    → ControlloAuthVersion → ControlloDisabilitato →        │    │   │
│  │  │    → ControlloPasswordChanged → Endpoint                    │    │   │
│  │  └─────────────────────────────────────────────────────────────┘    │   │
│  │                               │                                      │   │
│  │  ┌────────────────────────────┴───────────────────────────────┐    │   │
│  │  │                      ENDPOINTS                              │    │   │
│  │  │                                                             │    │   │
│  │  │  /auth/login           /auth/register                       │    │   │
│  │  │  /auth/refresh         /auth/logout                         │    │   │
│  │  │  /auth/revoke-all-sessions                                  │    │   │
│  │  │  /auth/me              /auth/me/change-password             │    │   │
│  │  │  /auth/me/external-logins                                   │    │   │
│  │  │  /auth/me/request-password-setup                            │    │   │
│  │  │  /auth/forgot-password /auth/reset-password                 │    │   │
│  │  │  /auth/setup-password                                       │    │   │
│  │  │  /auth/external/{provider} /auth/external/callback          │    │   │
│  │  │  /auth/admin/utenti    /auth/admin/utenti/{id}              │    │   │
│  │  │  /auth/admin/invite                                         │    │   │
│  │  └────────────────────────────┬───────────────────────────────┘    │   │
│  │                               │                                      │   │
│  │  ┌────────────────────────────┴───────────────────────────────┐    │   │
│  │  │                      SERVICES                               │    │   │
│  │  │                                                             │    │   │
│  │  │  AuthService ─── JwtTokenService ─── PasswordService        │    │   │
│  │  │  SocialAuthService ─── SecurityAuditService ─── EmailService│    │   │
│  │  └────────────────────────────┬───────────────────────────────┘    │   │
│  │                               │                                      │   │
│  │  ┌────────────────────────────┴───────────────────────────────┐    │   │
│  │  │                      DATABASE                               │    │   │
│  │  │                                                             │    │   │
│  │  │  ┌──────────┐ ┌──────────────────┐ ┌────────────────────┐  │    │   │
│  │  │  │  Utenti  │ │UserExternalLogins│ │AccountActionTokens │  │    │   │
│  │  │  └──────────┘ └──────────────────┘ └────────────────────┘  │    │   │
│  │  │                                                             │    │   │
│  │  │  ┌──────────────────┐ ┌──────────────────────────────┐     │    │   │
│  │  │  │ExternalAuthStates│ │UserSecurityAuditLog          │     │    │   │
│  │  │  │+ ExchangeCodes   │ │                              │     │    │   │
│  │  │  └──────────────────┘ └──────────────────────────────┘     │    │   │
│  │  └─────────────────────────────────────────────────────────────┘    │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│                         ┌──────────────────┐                                 │
│                         │  GOOGLE / MS OIDC │                                 │
│                         │  (esterno)        │                                 │
│                         └──────────────────┘                                 │
│                                                                              │
│                         ┌──────────────────┐                                 │
│                         │  SMTP SERVER     │                                 │
│                         │  (invio email)    │                                 │
│                         └──────────────────┘                                 │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 14. Configurazione

### 14.1 Variabili d'Ambiente (`.env`)

```bash
# ============================================
# IDENTITY & SECURITY - ITERAZIONE 5
# ============================================

# --- JWT ---
JWT_SECRET_KEY=<generare-chiave-64-caratteri-minimo>
JWT_ISSUER=FilmAPI
JWT_AUDIENCE=FilmFrontend
JWT_ACCESS_TOKEN_EXPIRY_MINUTES=15
JWT_REFRESH_TOKEN_EXPIRY_DAYS=7

# --- Google OIDC ---
GOOGLE_CLIENT_ID=123456789-xxx.apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=GOCSPX-xxxxxxxxxxxxxxxxxxxx

# --- Microsoft OIDC ---
MICROSOFT_CLIENT_ID=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
MICROSOFT_CLIENT_SECRET=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx

# --- Token TTL ---
ACCOUNT_TOKEN_PASSWORD_RESET_TTL_MINUTES=60
ACCOUNT_TOKEN_PASSWORD_SETUP_TTL_MINUTES=1440
ACCOUNT_TOKEN_ADMIN_INVITE_TTL_HOURS=72

# --- Rate Limiting ---
RATE_LIMIT_LOGIN_PER_MINUTE=5
RATE_LIMIT_REGISTER_PER_15MIN=3
RATE_LIMIT_FORGOT_PASSWORD_PER_15MIN=3

# --- Feature Flags ---
FEATURE_SOCIAL_LOGIN=true
FEATURE_SOCIAL_GOOGLE_ENABLED=true
FEATURE_SOCIAL_MICROSOFT_ENABLED=true
FEATURE_AUDIT_LOGGING=true
FEATURE_RATE_LIMITING=true
FEATURE_IP_TRACKING=false

# --- Base URL per link email ---
APP_BASE_URL=https://localhost:5001

# --- Admin Seed ---
DEFAULT_ADMIN_EMAIL=admin@filmapi.local
DEFAULT_ADMIN_PASSWORD=<password-sicura>
```

### 14.2 Chiavi API e Segreti

**Google OAuth 2.0**: creare le credenziali su [Google Cloud Console](https://console.cloud.google.com/):
- Crea progetto
- Abilita "Google Identity" API
- Crea "OAuth 2.0 Client ID" di tipo "Web application"
- Authorized redirect URI: `https://{host}/auth/external/callback`

**Microsoft Entra ID**: creare la registrazione su [Azure Portal](https://portal.azure.com/):
- App Registration → New Registration
- Supported account types: "Accounts in any organizational directory and personal Microsoft accounts"
- Redirect URI: `https://{host}/auth/external/callback`

---

## 15. Best Practices e Note di Sicurezza

### 15.1 Principi Fondamentali

1. **Mai fidarsi del client**: tutte le validazioni di sicurezza avvengono lato backend. Il frontend fa solo UX.

2. **Token single-use**: i token di reset password, setup e invito possono essere usati UNA SOLA volta. Dopo l'uso, `ConsumedAtUtc` viene impostato e il token non e piu valido.

3. **Hash, mai in chiaro**: i token email non vengono salvati nel DB. Si salva solo `SHA256(token_raw)`. Il token in chiaro esiste solo nella email inviata.

4. **Anti-enumerazione**: gli endpoint che coinvolgono email (forgot password) rispondono sempre allo stesso modo, sia che l'email esista o meno.

5. **Difesa in profondita**: anche se un Access Token viene rubato:
   - Scade in 15 minuti (finestra di danno limitata)
   - Il Refresh Token serve per ottenerne di nuovi (rubare solo l'Access Token non basta per l'accesso persistente)
   - `AuthVersion` permette di invalidare tutto istantaneamente

### 15.2 Cosa NON Fare MAI

| ❌ Da evitare | ✅ Da fare invece |
|---------------|-------------------|
| Salvare token in chiaro nei log | Loggare solo ID token, mai il valore |
| Confrontare `PasswordHash == null` senza null-check | Usare `utente.LocalCredentialsEnabled` per verificare se ha password |
| Permettere `returnUrl` esterni | Validare con whitelist `IsValidReturnUrl()` |
| Rispondere con messaggi diversi per email trovata/non trovata | Messaggio identico, tempo di risposta costante |
| Salvare `JWT_SECRET_KEY` in codice | Usare variabili d'ambiente o secrets manager |
| Usare lo stesso `AuthVersion` per tutte le operazioni | Incrementare solo per eventi di sicurezza |

### 15.3 Checklist di Sicurezza Pre-Deploy

- [ ] `JWT_SECRET_KEY` e di almeno 64 caratteri e NON e hardcoded
- [ ] `PasswordHash` e nullable e tutte le query gestiscono il caso null
- [ ] Rate limiting e attivo su tutti gli endpoint `/auth/*`
- [ ] CSP header configurato e testato
- [ ] `IsValidReturnUrl()` blocca URL assoluti esterni
- [ ] I token email sono single-use (`ConsumedAtUtc` verificato)
- [ ] Gli exchange code OIDC sono anti-replay (`ExternalAuthExchangeCode`)
- [ ] PowerUser/Admin non possono usare social login
- [ ] Social-only non possono essere promossi senza password
- [ ] L'ultimo admin non puo essere degradato o eliminato
- [ ] HTTPS e attivo in produzione
- [ ] `.env` e in `.gitignore` e non contiene segreti di produzione
