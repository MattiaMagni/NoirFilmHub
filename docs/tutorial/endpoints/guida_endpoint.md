# Guida agli Endpoint — Noir Film Hub (FilmAPI)

> Questa guida spiega in modo semplice tutti gli endpoint dell'API, cosa fanno, chi puo chiamarli e la logica che ci sta dietro.

---

## Indice

1. [Architettura generale](#architettura-generale)
2. [Autenticazione e autorizzazione](#autenticazione-e-autorizzazione)
3. [Auth — `/auth`](#auth)
4. [Film — `/films`](#films)
5. [Registi — `/registi`](#registi)
6. [Cinema — `/cinemas`](#cinemas)
7. [Sale — `/sale`](#sale)
8. [Categorie — `/categorie`](#categorie)
9. [Proiezioni — `/proiezioni`](#proiezioni)
10. [Programmazione — `/programmazione`](#programmazione)
11. [My Cinemas — `/my-cinemas`](#my-cinemas)
12. [Checkout / Posti — `/checkout`](#checkout)
13. [Carrello — `/cart`](#cart)
14. [Pagamenti — `/pagamenti`](#pagamenti)
15. [Biglietti — `/tickets`](#biglietti)
16. [Prenotazioni — `/prenotazioni`](#prenotazioni)
17. [Shop — `/shop`](#shop)
18. [Coupon — `/coupons`](#coupons)
19. [Gift Card — `/giftcards`](#giftcards)
20. [TMDB — `/tmdb`](#tmdb)
21. [Ordini — `/orders`](#ordini)
22. [Servizi in background](#servizi-in-background)

---

## Architettura generale

```
Browser (FilmFrontend, porta 5001)
    │
    ├─► GET  *.html, *.js, *.css   → servito da FilmFrontend (static files)
    │
    └─► GET/POST/PUT/DELETE /xxx   → chiamate API al backend FilmAPI (porta 5000)
                                     │
                                     └─► Endpoints/*.cs (handler HTTP)
                                         └─► Services/*.cs (logica business)
                                             └─► Data/FilmDbContext.cs (database)
```

Ogni chiamata API dal frontend passa da `js/api-client.js` che automaticamente:
- Aggiunge l'header `Authorization: Bearer <token>`
- Se riceve **401** prova un refresh automatico del token
- Se riceve **403** reindirizza alla home

### Tre livelli di accesso

| Ruolo | Cosa puo fare |
|-------|--------------|
| **Anonimo** (pubblico) | Vedere cinema, film, programmazione, categorie |
| **Utente** (autenticato) | Prenotare, gestire carrello, comprare, vedere profilo |
| **Admin / PowerUser** | CRUD su tutte le entita, gestire utenti, validare biglietti |
| **Admin (solo)** | Gestire ruoli utenti, invitare utenti, operazioni sensibili |

---

## Auth

**Prefisso:** `/auth`  
**File:** `Endpoints/AuthEndpoints.cs`  
**Servizi usati:** `AuthService`, `SocialAuthService`, `SecurityAuditService`, `JwtTokenService`, `PasswordService`, `EmailService`

### Endpoint pubblici (senza login)

| Metodo | URL | Cosa fa | Logica dietro |
|--------|-----|---------|--------------|
| `POST` | `/auth/register` | Registra un nuovo utente | Valida email e password (min 8 char, 1 maiuscola, 1 minuscola, 1 numero, 1 speciale). Normalizza email (lowercase). Crea `Utente` con ruolo base `Utente`. |
| `POST` | `/auth/login` | Login con email e password | Cerca utente per email. Verifica che non sia disabilitato/bloccato. Controlla la password con bcrypt. Se fallisce 5+ volte manda alert email, se 10+ blocca per 15 minuti. Genera coppia access token + refresh token JWT. |
| `POST` | `/auth/refresh` | Rinnova token JWT scaduto | Cerca utente tramite refresh token salvato nel DB. Se valido e non scaduto, genera nuovo access token + refresh token. |
| `POST` | `/auth/forgot-password` | Richiede reset password | Cerca utente per email (solo se ha credenziali locali). Genera token random (64 byte), salva l'hash nella tabella `AccountActionTokens`, invia email con link di reset. |
| `POST` | `/auth/reset-password` | Reimposta la password | Verifica che il token esista, non sia scaduto, non ancora usato. Aggiorna password, invalida tutte le sessioni, forza nuovo login. |
| `POST` | `/auth/setup-password` | Imposta prima password (utente social) | Per utenti creati da admin o social login. Stesso flusso del reset ma con token di tipo `PasswordSetup` o `AdminInvite`. |
| `GET` | `/auth/external/{provider}` | Inizia login social (Google/Microsoft) | Genera URL di redirect OAuth verso Google o Microsoft. Salva stato in `ExternalAuthStates` per anti-CSRF. |
| `GET` | `/auth/external/callback` | Callback OAuth dal provider | Scambia il code con il provider, ottiene i claim (email, nome, sub). Applica anti-replay (`ExternalAuthExchangeCode`). Crea o collega utente. Reindirizza al frontend con token in URL hash. |

### Endpoint per utenti autenticati

| Metodo | URL | Cosa fa | Logica dietro |
|--------|-----|---------|--------------|
| `POST` | `/auth/logout` | Logout (anche da tutti i dispositivi) | Cancella refresh token. Se `allDevices=true`, incrementa `AuthVersion` invalidando TUTTI i token esistenti. |
| `GET` | `/auth/me` | Dati del profilo corrente | Restituisce nome, email, ruolo, login esterni collegati. |
| `PUT` | `/auth/me` | Modifica profilo | Aggiorna nome, cognome, telefono. |
| `GET` | `/auth/me/cinema-preferito` | Cinema preferito dell'utente | Usato dagli operatori per sapere a quale cinema sono assegnati. |
| `PUT` | `/auth/me/cinema-preferito` | Imposta cinema preferito | Verifica che il cinema esista prima di assegnarlo. |
| `POST` | `/auth/me/change-password` | Cambia password | Verifica password corrente, applica policy strong, invalida sessioni, manda email di notifica. |
| `POST` | `/auth/me/request-password-setup` | Richiedi setup password | Per utenti social-only: invia email con link per impostare una password locale. |
| `POST` | `/auth/revoke-all-sessions` | Invalida tutte le sessioni | Incrementa `AuthVersion` e cambia `SecurityStamp`. Tutti i token esistenti smettono di funzionare. |
| `GET` | `/auth/me/external-logins` | Provider social collegati | Lista dei login Google/Microsoft collegati all'account. |
| `DELETE` | `/auth/me/external-logins/{id}` | Scollega provider social | Non permette di scollegare se e l'unico metodo di accesso disponibile. |

### Endpoint admin

| Metodo | URL | Ruolo | Cosa fa |
|--------|-----|-------|---------|
| `GET` | `/auth/admin/utenti` | Admin | Lista utenti con filtri (search, ruolo, disabilitato, paginazione, ordinamento) |
| `GET` | `/auth/admin/utenti/{id}` | Admin | Dettaglio utente con log di sicurezza recenti |
| `PUT` | `/auth/admin/utenti/{id}/ruolo` | Admin | Cambia ruolo. Protegge l'ultimo admin dalla degradazione. |
| `PUT` | `/auth/admin/utenti/{id}/disable` | Admin | Disabilita account. Protegge l'ultimo admin. |
| `PUT` | `/auth/admin/utenti/{id}/enable` | Admin | Riabilita account azzerando tentativi falliti. |
| `POST` | `/auth/admin/utenti/{id}/force-password-reset` | Admin | Forza reset password e manda email. |
| `DELETE` | `/auth/admin/utenti/{id}` | Admin | Elimina utente (dopo aver loggato i dati). Protegge l'ultimo admin. |
| `POST` | `/auth/admin/invite` | Admin | Invita nuovo utente via email. Crea utente disabilitato, manda link di setup password. |
| `PUT` | `/auth/admin/utenti/{id}/cinema` | Admin | Assegna cinema a un utente (per operatori). |

### Flusso JWT

1. **Login** → `AuthService` chiama `JwtTokenService.GenerateAccessToken()` che crea un token firmato HMAC-SHA256 contenente: `sub` (ID utente), `email`, `role`, `auth_version`, `security_stamp`. Scade dopo 15 minuti.
2. **Refresh** → `AuthService.RefreshAsync()` cerca il refresh token nel DB (scade dopo 7 giorni), genera nuova coppia.
3. **Validazione** → Ad ogni richiesta, il middleware JWT (`Program.cs:70-135`) verifica firma, scadenza, e controlla che `auth_version` nel token corrisponda a quella nel DB (cosi logout/change password invalidano tutti i token).

---

## Film

**Prefisso:** `/films`  
**File:** `Endpoints/FilmEndpoints.cs`

| Metodo | URL | Ruolo | Cosa fa | Logica |
|--------|-----|-------|---------|--------|
| `GET` | `/films` | Pubblico | Lista tutti i film con categorie | Include `FilmCategorie` e `Categoria` per mostrare i nomi delle categorie. |
| `GET` | `/films/{id}` | Pubblico | Dettaglio di un film | Include categorie. |
| `POST` | `/films` | Admin/PowerUser | Crea nuovo film | Valida: durata > 0, regista esiste, categorie esistono. Associa categorie tramite tabella `FilmCategorie`. |
| `PUT` | `/films/{id}` | Admin/PowerUser | Modifica film | Sostituisce le categorie (rimuove tutte e ri-aggiunge). |
| `DELETE` | `/films/{id}` | Admin/PowerUser | Elimina film | Rimuove anche le associazioni `FilmCategorie` in cascata. |

---

## Registi

**Prefisso:** `/registi`  
**File:** `Endpoints/RegistiEndpoints.cs`

| Metodo | URL | Ruolo | Cosa fa |
|--------|-----|-------|---------|
| `GET` | `/registi` | Pubblico | Lista registi |
| `GET` | `/registi/{id}` | Pubblico | Dettaglio regista |
| `POST` | `/registi` | Admin/PowerUser | Crea regista (nome, cognome, nazionalita) |
| `PUT` | `/registi/{id}` | Admin/PowerUser | Modifica regista |
| `DELETE` | `/registi/{id}` | Admin/PowerUser | Elimina regista |
| `GET` | `/registi/{id}/films` | Pubblico | Film di un regista |
| `POST` | `/registi/{id}/films` | Admin/PowerUser | Crea film per un regista specifico |

---

## Cinema

**Prefisso:** `/cinemas`  
**File:** `Endpoints/CinemaEndpoints.cs`

| Metodo | URL | Ruolo | Cosa fa | Logica |
|--------|-----|-------|---------|--------|
| `GET` | `/cinemas` | Pubblico | Lista cinema (ordinati per citta, nome) | |
| `GET` | `/cinemas/nearby?lat=&lng=` | Pubblico | Cinema vicini a coordinate GPS | Calcola distanza in km con `GeoHelper.DistanceKm()`, ordina per vicinanza. |
| `GET` | `/cinemas/{id}` | Pubblico | Dettaglio cinema | |
| `POST` | `/cinemas` | Admin | Crea cinema | Valida capienza (20-500), campi obbligatori, codice locale univoco. Include lat/lng per geolocalizzazione. |
| `PUT` | `/cinemas/{id}` | Admin | Modifica cinema | Stesse validazioni della creazione. |
| `DELETE` | `/cinemas/{id}` | Admin | Elimina cinema | |

---

## Sale

**Prefisso:** `/sale`  
**File:** `Endpoints/SaleEndpoints.cs`

| Metodo | URL | Ruolo | Cosa fa | Logica |
|--------|-----|-------|---------|--------|
| `GET` | `/sale?cinemaId=` | Pubblico | Lista sale (filtrabili per cinema) | |
| `GET` | `/sale/{id}` | Pubblico | Dettaglio sala | |
| `POST` | `/sale` | Admin/PowerUser | Crea sala | Valida: tipologia (ISENSE/XL/3D/2D), dimensioni file/posti (1-50), numero univoco per cinema. |
| `PUT` | `/sale/{id}` | Admin/PowerUser | Modifica sala | |
| `DELETE` | `/sale/{id}` | Admin/PowerUser | Elimina sala | Blocca se ci sono proiezioni collegate. |

---

## Categorie

**Prefisso:** `/categorie`  
**File:** `Endpoints/CategorieEndpoints.cs`

| Metodo | URL | Ruolo | Cosa fa |
|--------|-----|-------|---------|
| `GET` | `/categorie` | Pubblico | Lista categorie |
| `GET` | `/categorie/{id}` | Pubblico | Dettaglio categoria |
| `GET` | `/categorie/{id}/films` | Pubblico | Film di una categoria |
| `POST` | `/categorie` | Admin | Crea categoria (nome univoco) |
| `PUT` | `/categorie/{id}` | Admin | Modifica categoria |
| `DELETE` | `/categorie/{id}` | Admin | Elimina categoria |

---

## Proiezioni

**Prefisso:** `/proiezioni`  
**File:** `Endpoints/ProiezioniEndpoints.cs`

| Metodo | URL | Ruolo | Cosa fa | Logica |
|--------|-----|-------|---------|--------|
| `GET` | `/proiezioni?filmId=&cinemaId=&day=` | Pubblico | Lista proiezioni con filtri | Include dati sala. Filtri opzionali per film, cinema, giorno. |
| `GET` | `/proiezioni/{id}` | Pubblico | Dettaglio proiezione | |
| `POST` | `/proiezioni` | Admin/PowerUser | Crea proiezione | Valida: film e cinema esistono, prezzo > 0, sala disponibile. **Controlla conflitti orari**: calcola inizio e fine (data + durata film), verifica che la sala non abbia altri show sovrapposti lo stesso giorno. Se nessuna sala specificata, crea automaticamente una sala 2D di default. |
| `PUT` | `/proiezioni/{id}` | Admin/PowerUser | Modifica proiezione | Stesse validazioni. |
| `DELETE` | `/proiezioni/{id}` | Admin/PowerUser | Elimina proiezione | |
| `POST` | `/proiezioni/{id}/cancel` | Admin/PowerUser | Annulla proiezione e rimborsa | Per ogni prenotazione confermata: genera una **Gift Card di rimborso** da usare in futuro, annulla la prenotazione, rilascia i posti. |

---

## Programmazione

**Prefisso:** `/programmazione`  
**File:** `Endpoints/ProgrammazioneEndpoints.cs`

Endpoint pubblici per il frontend utente (visualizzazione palinsesto).

| Metodo | URL | Cosa fa | Logica |
|--------|-----|---------|--------|
| `GET` | `/programmazione/shows?filmId=&cinemaId=&day=` | Proiezioni di un giorno | Raggruppa per tipologia sala (2D, 3D, XL, ISENSE), restituisce orari e prezzi. |
| `GET` | `/programmazione/films?search=&categoria=&cinemaId=` | Film in programmazione | Mostra solo film con proiezioni nei prossimi 30 giorni nel cinema selezionato. Include conteggio show, categorie, regista. |
| `GET` | `/programmazione/films/{filmId}?cinemaId=` | Calendario di un film | Per un film specifico: tutti gli orari raggruppati per data e tipologia sala, nei prossimi 30 giorni. |

---

## My Cinemas

**Prefisso:** `/my-cinemas`  
**File:** `Endpoints/MyCinemasEndpoints.cs`

Endpoint pubblici per la schermata "I nostri cinema".

| Metodo | URL | Cosa fa | Logica |
|--------|-----|---------|--------|
| `GET` | `/my-cinemas/tipologie` | Lista tipologie sala disponibili | `SELECT DISTINCT Tipologia FROM Sale WHERE Attiva` |
| `GET` | `/my-cinemas?citta=&tipologiaSala=&lat=&lng=&raggio=` | Lista cinema con filtri | Filtra per citta, tipologia sala. Se fornite coordinate GPS, calcola distanza e ordina per vicinanza. |
| `GET` | `/my-cinemas/{cinemaId}/programmazione?day=` | Programmazione di un cinema | Restituisce i film in programmazione in un cinema in un giorno specifico, raggruppati con orari per tipologia. Include anche i giorni disponibili nei prossimi 30 giorni. |

---

## Checkout / Posti

**Prefisso:** `/checkout`  
**File:** `Endpoints/CheckoutEndpoints.cs`  
**Servizi usati:** `SeatPricingUtils` (statico)

| Metodo | URL | Ruolo | Cosa fa | Logica |
|--------|-----|-------|---------|--------|
| `GET` | `/checkout/seats/{proiezioneId}` | Pubblico | Mappa posti di una proiezione | Mostra: posti **venduti** (prenotazioni non annullate), posti **bloccati da altri** (SeatLock attivi), posti **bloccati da me**, posti **VIP** (calcolati da `SeatPricingUtils`), prezzo base, supplemento VIP. |
| `POST` | `/checkout/locks` | Utente | Blocca posti (10 minuti) | Verifica che i posti non siano gia venduti o bloccati da altri. Crea/aggiorna `SeatLock` con scadenza a +10 minuti. |
| `DELETE` | `/checkout/locks/{proiezioneId}` | Utente | Rilascia i posti bloccati | Elimina tutti i SeatLock dell'utente per quella proiezione. |

### Sistema di lock dei posti

```
1. Utente seleziona posti sulla mappa → il frontend li colora di giallo
2. POST /checkout/locks → il backend crea SeatLock (scadenza: now + 10 min)
3. Il frontend chiama GET /checkout/seats ogni 30s per aggiornare lo stato
4. Se l'utente NON completa l'acquisto → CleanupHostedService li elimina dopo 10 min
5. Se l'utente completa l'acquisto → il pagamento rimuove i lock e crea la prenotazione
```

---

## Carrello

**Prefisso:** `/cart`  
**File:** `Endpoints/CartEndpoints.cs`  
**Servizi usati:** `CartService`

Il carrello supporta 3 tipi di item: `Ticket` (biglietti), `GiftCard`, `Merchandise`.

| Metodo | URL | Ruolo | Cosa fa | Logica |
|--------|-----|-------|---------|--------|
| `POST` | `/cart` | Pubblico* | Crea o recupera carrello | *Puo essere guest (con header `X-Guest-Token`) o autenticato. `CartService.GetOrCreateCartAsync()`: cerca carrello attivo per utente o guest token. Se era in checkout (<30s fa), lo lascia stare. Altrimenti pulisce ticket con lock scaduti, estende lock attivi di 5 min. Se non esiste, lo crea. |
| `GET` | `/cart/{id}` | Utente | Dettaglio carrello | Carica `CartItems` incluso. |
| `POST` | `/cart/{id}/items` | Utente | Aggiunge item al carrello | Per Ticket: verifica che ci siano SeatLock attivi, li associa al carrello. Per Merchandise: verifica stock variante. Se l'item esiste gia, fa merge (quantita e posti, con controllo duplicati). |
| `PUT` | `/cart/{id}/items/{itemId}` | Utente | Modifica quantita/dettagli item | Se quantita <= 0, rimuove l'item e rilascia i lock. |
| `DELETE` | `/cart/{id}/items/{itemId}` | Utente | Rimuove item dal carrello | Per Ticket: rilascia i SeatLock associati. |
| `POST` | `/cart/{id}/apply-coupon` | Utente | Applica un coupon | `CartService.ApplyCouponAsync()`: verifica data, utilizzi, target (film/cinema/carrello), importo minimo, stacking. Calcola sconto (percentuale con cap o fisso). |
| `DELETE` | `/cart/{id}/coupon` | Utente | Rimuove coupon applicato | Azzera `ScontoCoupon` e ricalcola. |
| `POST` | `/cart/{id}/apply-giftcard` | Utente | Applica una gift card | Verifica: codice valido, non scaduta, appartiene all'utente. Scala il totale. |
| `POST` | `/cart/merge` | Utente | Unisce carrello guest al login | Quando un utente fa login, il suo carrello guest viene fuso con quello autenticato. |

### Flusso del carrello

```
Stato: Active → Checkout → Converted (completato)
                          → Expired (scaduto dopo 7 giorni o senza item)
```

---

## Pagamenti

**Prefisso:** `/pagamenti`  
**File:** `Endpoints/PagamentiEndpoints.cs` (946 righe, il file piu complesso)  
**Servizi usati:** `TicketPdfService`, `TicketEmailService`, `EmailService`, `SeatPricingUtils`

### Pagamento biglietti singoli

| Metodo | URL | Ruolo | Cosa fa |
|--------|-----|-------|---------|
| `POST` | `/pagamenti/conferma` | Utente | Crea pagamento biglietti (vecchio flusso senza carrello) |
| `POST` | `/pagamenti/checkout-session` | Utente | Crea sessione Stripe per biglietti |

### Flusso: validazione → Stripe → conferma → PDF

```
1. POST /pagamenti/checkout-session
   ├── ValidatePurchaseAsync(): verifica posti non venduti e lock validi
   ├── Calcola totale (prezzo base + supplemento VIP con SeatPricingUtils)
   ├── Crea Stripe Checkout Session con line item
   ├── Crea Prenotazione in stato "PendingStripe"
   └── Restituisce URL Stripe a cui reindirizzare l'utente

2. L'utente paga su Stripe

3. Stripe chiama webhook POST /pagamenti/stripe/webhook
   ├── Verifica firma webhook
   ├── Se evento "checkout.session.completed":
   │   ├── FinalizeBookingAsync():
   │   │   ├── Ri-valida che i posti siano ancora disponibili
   │   │   ├── Genera codice acquisto univoco (NFH-YYYYMMDDHHMMSS-XXXXXXXX)
   │   │   ├── Prenotazione → stato "Confermata"
   │   │   ├── Rimuove i SeatLock
   │   │   ├── TicketPdfService.GenerateOrderPdf() → genera PDF biglietto
   │   │   └── TicketEmailService.SendTicketEmailAsync() → invia email con PDF
   │   └── Se fallisce → stato "Fallita"
   └── Risponde 200 OK a Stripe

4. Il frontend chiama GET /pagamenti/esito?session_id=...
   └── Restituisce lo stato della prenotazione (Confermata/Annullata/...)
```

### Pagamento carrello (multi-item)

| Metodo | URL | Ruolo | Cosa fa |
|--------|-----|-------|---------|
| `POST` | `/pagamenti/cart-checkout` | Utente | Checkout dell'intero carrello |
| `GET` | `/pagamenti/esito?session_id=` | Utente | Verifica esito pagamento |

**Logica cart-checkout (`FinalizeCartOrderAsync`):**

```
POST /pagamenti/cart-checkout { cartId }
│
├── Carica carrello con tutti i CartItem
├── Valida che TUTTI i posti dei ticket siano ancora disponibili
├── Calcola subtotale, sconto, gift card → stripeAmount
│
├── Se stripeAmount <= 0 (gift card copre tutto):
│   └── FinalizeCartOrderAsync() SUBITO, no Stripe
│
├── Altrimenti:
│   ├── Applica sconto e gift card proporzionalmente ai line item
│   ├── Crea Stripe Checkout Session (multi line-item)
│   └── Carrello → stato "Checkout"
│
└── Dopo pagamento (webhook o polling):
    └── FinalizeCartOrderAsync():
        ├── Deduce saldo gift card usata
        ├── Per ogni Ticket → crea Prenotazione (Confermata) con codice acquisto
        ├── Per ogni GiftCard → genera codici NFH-GC-XXXXXXXX-XXXX
        ├── Per ogni Merchandise → decrementa stock variante
        ├── Traccia uso coupon (CouponUsages + UtilizziAttuali++)
        ├── Rimuove tutti i SeatLock
        ├── Carrello → stato "Converted"
        └── Invia email:
            ├── Conferma ordine al compratore
            ├── Gift card ai destinatari
            ├── Biglietti PDF per ogni prenotazione
            └── Notifica saldo gift card se usata
```

---

## Biglietti

**Prefisso:** `/tickets`  
**File:** `Endpoints/BigliettiEndpoints.cs`  
**Servizi usati:** `TicketPdfService`

| Metodo | URL | Ruolo | Cosa fa | Logica |
|--------|-----|-------|---------|--------|
| `GET` | `/tickets/{codiceAcquisto}` | Utente | Dettaglio biglietto per codice | Include film, cinema, data, ora, posti. |
| `GET` | `/tickets/validate/{codiceAcquisto}` | Pubblico | Info biglietto per validazione | Stesso di sopra ma accessibile senza login (per scanner QR code). |
| `POST` | `/tickets/{codiceAcquisto}/validate` | Admin/PowerUser | Valida un biglietto | Verifica: biglietto esiste, non e annullato, non gia validato. **Controlla che l'operatore abbia un cinema assegnato** e che corrisponda al cinema della proiezione. Marca `Validato=true` con data e operatore. |
| `GET` | `/tickets/{codiceAcquisto}/pdf` | Utente | Scarica PDF del biglietto | L'utente puo vedere solo i propri biglietti. Admin/PowerUser possono vedere tutti. Genera PDF con `TicketPdfService`. |

---

## Prenotazioni

**Prefisso:** `/prenotazioni`  
**File:** `Endpoints/PrenotazioniEndpoints.cs`  
**Servizi usati:** `EmailService`

| Metodo | URL | Ruolo | Cosa fa | Logica |
|--------|-----|-------|---------|--------|
| `GET` | `/prenotazioni` | Admin | Lista TUTTE le prenotazioni | Include proiezione, film, cinema, sala. |
| `GET` | `/prenotazioni/mie` | Utente | Le MIE prenotazioni | Stessa query ma filtrata per `UtenteId`. |
| `GET` | `/prenotazioni/{id}` | Utente | Dettaglio prenotazione | Utente vede solo le proprie; admin tutte. |
| `POST` | `/prenotazioni` | Utente | Crea prenotazione diretta (senza Stripe) | Verifica capienza cinema (postiDisponibili - postiGiaPrenotati). Crea con stato "Confermata" e codice acquisto. |
| `PUT` | `/prenotazioni/{id}/annulla` | Utente | Annulla prenotazione | **Rimborso 50%** se la prenotazione era Confermata: genera una Gift Card con codice `NFH-RF-XXXXXXXX` del valore rimborsato. Invia email di conferma annullamento con codice gift card. Admin/PowerUser possono annullare qualsiasi prenotazione. |

---

## Shop

**Prefisso:** `/shop`  
**File:** `Endpoints/ShopEndpoints.cs`

| Metodo | URL | Ruolo | Cosa fa | Logica |
|--------|-----|-------|---------|--------|
| `POST` | `/shop/upload-image` | Admin | Upload immagine prodotto | Max 5 MB, formati: JPG, PNG, WEBP, GIF. Salva in `wwwroot/assets/products/`. |
| `GET` | `/shop/products` | Pubblico | Lista prodotti attivi | Include varianti attive con prezzo finale (base + extra). |
| `GET` | `/shop/products/{id}` | Pubblico | Dettaglio prodotto | |
| `GET` | `/shop/giftcard-templates` | Pubblico | Lista template gift card | Importi predefiniti: 10, 20, 30, 50 EUR. |
| `POST` | `/shop/products` | Admin | Crea prodotto | |
| `PUT` | `/shop/products/{id}` | Admin | Modifica prodotto | |
| `DELETE` | `/shop/products/{id}` | Admin | Disattiva prodotto | Soft-delete: imposta `Attivo = false`. |

---

## Coupon

**Prefisso:** `/coupons`  
**File:** `Endpoints/CouponEndpoints.cs`  
**Servizi usati:** `EmailService`

| Metodo | URL | Ruolo | Cosa fa | Logica |
|--------|-----|-------|---------|--------|
| `POST` | `/coupons/validate` | Utente | Verifica se un codice coupon e valido | Controlla: esiste, attivo, nel periodo di validita, utilizzi disponibili, limite per utente. |
| `GET` | `/coupons` | Pubblico | Lista coupon attivi | |
| `POST` | `/coupons/{id}/redeem` | Utente | Riscatta un coupon | Manda email con il codice all'utente. Non consuma il coupon (quello avviene al checkout). |
| `POST` | `/coupons` | Admin | Crea nuovo coupon | Valida: percentuale (1-100) o fisso (>0), date valide. Tipi target: Carrello, Film, Cinema. |
| `PUT` | `/coupons/{id}` | Admin | Modifica coupon | |
| `DELETE` | `/coupons/{id}` | Admin | Disattiva coupon | Soft-delete: `Attivo = false`. |
| `GET` | `/coupons/{id}/usage` | Admin | Storico utilizzi coupon | |

### Tipi di coupon

| Tipo Sconto | Funzionamento |
|-------------|--------------|
| **Percentuale** | Sconto del X% sul subtotale, con `ScontoMassimo` opzionale |
| **Fisso** | Sconto di X euro, non supera il subtotale |

| Tipo Target | Significato |
|-------------|------------|
| **Carrello** | Applicabile a qualsiasi acquisto |
| **Film** | Solo se il carrello contiene biglietti per quel film specifico |
| **Cinema** | Solo se il carrello contiene biglietti per quel cinema specifico |

---

## Gift Card

**Prefisso:** `/giftcards`  
**File:** `Endpoints/GiftCardEndpoints.cs`

| Metodo | URL | Ruolo | Cosa fa | Logica |
|--------|-----|-------|---------|--------|
| `GET` | `/giftcards/mine` | Utente | Le mie gift card | Solo quelle dove `UtenteAcquirenteId == userId`. |
| `GET` | `/giftcards/{codice}/balance` | Pubblico | Saldo di una gift card | Restituisce `{ valid, saldo, message }`. |
| `POST` | `/giftcards/{codice}/redeem` | Utente | Riscatta gift card in credito piattaforma | Deduce importo dal saldo gift card e lo aggiunge a `CreditoPiattaforma` dell'utente. Registra transazione in `GiftCardTransactions`. |
| `GET` | `/giftcards/mine/transactions` | Utente | Storico transazioni gift card | |

---

## TMDB

**Prefisso:** `/tmdb`  
**File:** `Endpoints/TmdbEndpoints.cs`  
**Servizi usati:** `TmdbService` (integrazione con The Movie Database API)

| Metodo | URL | Ruolo | Cosa fa | Logica |
|--------|-----|-------|---------|--------|
| `GET` | `/tmdb/status` | Admin/PowerUser | Verifica configurazione TMDB | Controlla `TMDB_API_READ_TOKEN` configurato. |
| `GET` | `/tmdb/latest?limit=&page=` | Admin/PowerUser | Ultime uscite da TMDB | Chiama `/discover/movie` su TMDB, mostra se gia presenti nel catalogo. |
| `GET` | `/tmdb/search?title=&limit=&page=` | Admin/PowerUser | Cerca film su TMDB | Chiama `/search/movie`. |
| `POST` | `/tmdb/import-latest` | Admin/PowerUser | Importa film selezionati da TMDB | `TmdbService.ImportMoviesAsync()`: per ogni TMDB ID, chiama i dettagli, crea `Film`, importa regista, assegna categoria default. |
| `POST` | `/tmdb/sync/film/{filmId}` | Admin/PowerUser | Sincronizza un film con TMDB | Cerca match per titolo+anno, scarica dettagli, aggiorna descrizione, cast, trailer, copertina. |
| `POST` | `/tmdb/sync/films` | Admin/PowerUser | Sincronizza tutti i film mancanti | `TmdbService.SyncMissingAsync()`: cerca film senza descrizione lunga o cast e li sincronizza. |
| `GET` | `/tmdb/missing` | Admin/PowerUser | Lista film con dati TMDB mancanti | |

---

## Ordini

**Prefisso:** `/orders`  
**File:** `Program.cs:474-561`

| Metodo | URL | Ruolo | Cosa fa | Logica |
|--------|-----|-------|---------|--------|
| `GET` | `/orders/mine` | Utente | Storico ordini dell'utente | Unisce prenotazioni biglietti (`B-{id}`) e ordini shop (`C-{cartId}`), ordinati per data. Mostra tipo, stato, totale, sconto, gift card, righe con descrizione. |

---

## Servizi in background

Questi non sono endpoint, ma servizi che girano in automatico.

### CleanupHostedService

- **Avvio:** 30 secondi dopo il boot dell'app
- **Frequenza:** ogni **60 secondi**
- **Cosa fa:**
  1. Elimina `SeatLock` scaduti (sia con che senza carrello)
  2. Rimuove `CartItem` ticket senza lock attivi rimasti
  3. Marca carrelli vuoti come `Expired`
  4. Scade carrelli oltre i 7 giorni (`ExpiresAtUtc`)
  5. Elimina `InventoryReservation` scaduti
  6. Elimina `ExternalAuthState` scaduti (sessioni social login)
  7. Elimina `AccountActionToken` consumati o scaduti da piu di 24h

### TmdbSyncHostedService

- **Avvio:** al boot dell'app
- **Frequenza:** una volta al giorno all'ora configurata (default **3:00 AM**, variabile `TMDB_SYNC_HOUR`)
- **Cosa fa:** chiama `TmdbService.SyncMissingAsync()` che cerca tutti i film senza descrizione lunga o cast principale e li sincronizza con TMDB

---

## Riepilogo: chi chiama cosa

| Frontend (HTML/JS) | Chiama API | Endpoint |
|---------------------|-----------|----------|
| `cinemas.html` | GET, POST, PUT, DELETE | `/cinemas` |
| `films.html` | GET, POST, PUT, DELETE | `/films`, `/registi`, `/categorie` |
| `registi.html` | GET, POST, PUT, DELETE | `/registi` |
| `sale.html` | GET, POST, PUT, DELETE | `/sale` |
| `categorie.html` | GET, POST, PUT, DELETE | `/categorie` |
| `proiezioni.html` | GET, POST, PUT, DELETE, POST cancel | `/proiezioni` |
| `programmazione.html` | GET | `/programmazione/shows`, `/programmazione/films` |
| `my-cinemas.html` | GET | `/my-cinemas` |
| `scheda-film.html` | GET | `/programmazione/films/{id}` |
| `acquista.html` / `pagamento.html` | GET seats, POST locks, POST checkout | `/checkout`, `/pagamenti` |
| `cart.html` | POST create, GET, POST/PUT/DEL items | `/cart` |
| `shop.html` | GET products, POST cart | `/shop`, `/cart` |
| `esito-pagamento.html` | GET esito | `/pagamenti/esito` |
| `validazione-biglietti.html` | GET validate, POST validate | `/tickets` |
| `proiezioni-pubblico.html` | GET | `/programmazione`, `/checkout/seats` |
| `login.html` / `register.html` | POST | `/auth/login`, `/auth/register` |
| `profile.html` | GET me, GET orders | `/auth/me`, `/orders/mine` |
| `utenti.html` | GET, PUT, DELETE | `/auth/admin/utenti` |
| `tmdb-admin.html` | GET, POST | `/tmdb/*` |
| `dashboard.html` | GET | `/cinemas`, `/proiezioni` (aggregazione lato client) |
| `navbar.js` (ogni pagina) | POST | `/cart` (per badge conteggio) |
| **CleanupHostedService** (auto) | Query dirette DB | `SeatLock`, `Cart`, `ExternalAuthState`, `AccountActionToken` |
| **TmdbSyncHostedService** (auto) | Chiama `TmdbService` | `/discover/movie`, `/search/movie`, `/movie/{id}` su TMDB esterno |
| **Stripe webhook** (esterno) | POST | `/pagamenti/stripe/webhook` |
| `seed_realistic_data.py` | POST, PUT, DELETE | Tutti gli endpoint CRUD (usa `urllib`) |
