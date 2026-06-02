# Noir Film Hub (FilmAPI) — Documentazione Completa e Dettagliata

> Versione completa e tecnica. Per una versione di studio piu semplice, consulta `Documentazione_Studio.md`.

---

## 1. Panoramica Generale

**Noir Film Hub** e una piattaforma web full-stack per la gestione completa di un circuito cinematografico multiplex. Il sistema copre tutte le fasi del lifecycle cinematografico: dal catalogo film alla programmazione, dalla vendita biglietti alla validazione, dal merchandising ai pagamenti, fino all'amministrazione completa.

L'applicazione e composta da due componenti .NET 9 che comunicano via HTTP REST:

| Componente | Tecnologia | Porta | Ruolo |
|------------|-----------|-------|-------|
| **FilmAPI** | ASP.NET Core Minimal API + EF Core + MariaDB | `5000` | Backend API RESTful |
| **FilmFrontend** | ASP.NET Core Static Server + HTML/CSS/JS | `5001` | Frontend lato client |

Il frontend non accede mai direttamente al database: ogni operazione passa per le API REST del backend, che implementa autenticazione JWT, autorizzazione basata su ruoli e validazione dei dati.

---

## 2. Stack Tecnologico — Dettaglio per Componente

### 2.1 Backend (FilmAPI)

| Tecnologia | Versione | Utilizzo nell'app |
|-----------|---------|-------------------|
| **ASP.NET Core Minimal API** | 9.0 | Framework web: definizione degli endpoint in `Program.cs` e `Endpoints/*.cs` |
| **Entity Framework Core** | 9.0.11 | ORM per mappatura modello dati su MariaDB, migrazioni automatiche |
| **Pomelo.EntityFrameworkCore.MySql** | 9.0.0 | Provider EF Core per MySQL/MariaDB |
| **MariaDB** | 10.11 (Docker) | Database relazionale per tutti i dati persistenti |
| **BCrypt.Net** | 4.0.3 | Hashing password con algoritmo BCrypt |
| **JWT Bearer** | 9.0.11 | Autenticazione stateless con Access Token (15 min) + Refresh Token (7 giorni) |
| **Stripe.net** | 49.0.0 | Integrazione pagamenti con Stripe Hosted Checkout |
| **QuestPDF** | 2024.7.1 (Community) | Generazione PDF biglietti con layout programmatico |
| **ZXing.Net** | 0.16.10 | Generazione barcode Code128 e QR code per biglietti |
| **SkiaSharp** | 3.116.1 | Rendering immagini (barcode/QR in formato PNG) |
| **MailKit** | 4.11.0 | Invio email (biglietti, reset password, inviti) |
| **DotNetEnv** | 3.0.0 | Caricamento variabili d'ambiente da file `.env` |

**Come vengono utilizzate:**

- **EF Core** gestisce tutte le operazioni CRUD tramite `FilmDbContext`, con relazioni definite fluent in `OnModelCreating`. Esempio: la relazione Film-Regista con cascata:

```csharp
// Data/FilmDbContext.cs — Configurazione relazione
modelBuilder.Entity<Film>()
    .HasOne(f => f.Regista)
    .WithMany(r => r.Films)
    .HasForeignKey(f => f.RegistaId)
    .OnDelete(DeleteBehavior.Cascade);
```

- **BCrypt** viene usato in `PasswordService` per l'hashing delle password durante la registrazione e la verifica durante il login. Inoltre valida la robustezza della password (min 8 caratteri, 1 maiuscola, 1 minuscola, 1 numero, 1 speciale).

- **JWT** viene generato in `JwtTokenService.GenerateAccessToken()`, che inserisce claim personalizzati come `auth_version` e `security_stamp` per invalidare token globalmente se l'utente cambia password o viene disabilitato:

```csharp
// Services/JwtTokenService.cs — Claim nel token JWT
var claims = new List<Claim>
{
    new(JwtRegisteredClaimNames.Sub, utente.Id.ToString()),
    new(JwtRegisteredClaimNames.Email, utente.Email),
    new(ClaimTypes.Role, utente.Ruolo),
    new("auth_version", utente.AuthVersion.ToString()),
    new("security_stamp", utente.SecurityStamp)
};
```

- **QuestPDF** genera il PDF del biglietto in `TicketPdfService.GenerateOrderPdf()`, creando una pagina A4 per ogni posto con barcode Code128 e QR code:

```csharp
// Services/TicketPdfService.cs — Generazione barcode e QR
var barcodeBytes = BuildPngBarcode(booking.CodiceAcquisto, BarcodeFormat.CODE_128, 720, 180, 4);
var qrUrl = $"{validateBaseUrl}/tickets/validate/{booking.CodiceAcquisto}";
var qrBytes = BuildPngBarcode(qrUrl, BarcodeFormat.QR_CODE, 320, 320, 1);
```

- **Stripe** gestisce il pagamento tramite Hosted Checkout Session, dove l'utente viene reindirizzato alla pagina di pagamento Stripe e poi rimandato al frontend con l'esito.

- **MailKit** invia email HTML con allegato PDF per i biglietti e link di reset password. Include retry policy per gestire temporanei failure SMTP.

### 2.2 Frontend (FilmFrontend)

| Tecnologia | Utilizzo |
|-----------|----------|
| **HTML5** | Struttura pagine (oltre 20 pagine) |
| **CSS3 con Custom Properties** | Design system (variabili `--bg`, `--primary`, ecc.), dark/light theme |
| **Vanilla JavaScript (ES6+)** | Dynamic rendering, Fetch API verso backend, gestione token |
| **Font Inter + Poppins** | Inter per il body, Poppins per i titoli |

Il frontend e una Single Page Application "leggera": ogni pagina e un file HTML autonomo che carica componenti condivisi (navbar, footer) tramite `template-loader.js`. La comunicazione API e centralizzata in `api-client.js`, che gestisce:

- Aggiunta automatica header `Authorization: Bearer {token}` ad ogni richiesta
- Auto-refresh del token quando l'Access Token scade (intercetta 401, esegue `POST /auth/refresh`, riprova)
- Gestione errori e rate-limiting (429)

### 2.3 Infrastruttura

| Componente | Dettaglio |
|-----------|----------|
| **Docker Compose** | MariaDB 10.11 in container con volumi persistenti |
| **Migrazioni automatiche** | `db.Database.Migrate()` al via dell'app |
| **Seed automatico** | Dati iniziali (admin, registi, film, cinema, sale, proiezioni, prodotti, coupon) |
| **Variabili d'ambiente** | File `.env` caricato con DotNetEnv per DB, JWT, Stripe, TMDB, SMTP |

---

## 3. Modello Dati — Entita e Relazioni

### 3.1 Diagramma Entita-Relazione (semplificato)

```
Regista (1) ────── (*) Film (*) ────── (*) Categoria
                         │                  [FilmCategoria join]
                    (*)
                Proiezione
                    │           ─────── (*) Prenotazione (*) ── (1) Utente
              (1)  │                                    │
            Sala   │                              (1) Cart
              │     │                                    │
         (1) │     │                              (*) CartItem
         Cinema    │                              (*) InventoryReservation
                                                                │
                                                          Product (*)─(*) ProductVariant

GiftCardTemplate ── GiftCard ── GiftCardTransaction
Coupon ── CouponUsage
SeatLock (per anti-race-condition)
UserExternalLogin, AccountActionToken, ExternalAuthState, UserSecurityAuditLog
NotificationSubscription
```

### 3.2 Dettaglio entita principali con esempi dal codice

#### Regista

```csharp
// Model/Regista.cs (implied from seed data)
new Regista { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "UK" }
```

#### Film — Multimedialita e integrazione TMDB

Ogni film puo avere copertina, backdrop e trailer.linkati via URL, e si sincronizza con TMDB per arricchire i metadati:

```csharp
// Model/Film.cs
public class Film
{
    public int Id { get; set; }
    public string Titolo { get; set; } = null!;
    public string TitoloOriginale { get; set; } = string.Empty;
    public int RegistaId { get; set; }
    public int Durata { get; set; }                          // durata in minuti
    public string? CopertinaPath { get; set; }               // URL immagine copertina
    public string? BackdropPath { get; set; }                // URL immagine sfondo
    public string? FilmatoPath { get; set; }                 // URL trailer YouTube
    public string DescrizioneLunga { get; set; } = string.Empty;
    public string CastPrincipale { get; set; } = string.Empty;
    public int? TmdbMovieId { get; set; }                    // ID The Movie Database
    public string TmdbSyncStato { get; set; } = "NotSynced"; // NotSynced, Synced, Seeded, Error
    public DateTime? UltimaSyncTmdbUtc { get; set; }
    public ICollection<FilmCategoria> FilmCategorie { get; set; } = new List<FilmCategoria>();
    public ICollection<Proiezione> Proiezioni { get; set; } = new List<Proiezione>();
}
```

#### Cinema — Multi-sala con geolocalizzazione

```csharp
// Model/Cinema.cs
public class Cinema
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public string Citta { get; set; } = null!;
    public string Indirizzo { get; set; } = null!;
    public double? Latitudine { get; set; }      // per ricerca "nearby"
    public double? Longitudine { get; set; }
    public string CodiceLocale { get; set; }      // codice SIAE per stampa biglietto
    public bool Attivo { get; set; } = true;
    public ICollection<Sala> Sale { get; set; } = new List<Sala>();
}
```

#### Sala — Mappa posti JSON generata algoritmicamente

La mappa posti e generata dal metodo `BuildSeatMapJson` in `Program.cs`, che crea una matrice fila-per-fila con un corridoio centrale:

```csharp
// Program.cs — Generazione mappa posti
static string BuildSeatMapJson(int rows, int cols, int aisleWidth)
{
    var safeRows = Math.Clamp(rows, 1, 26);
    var safeCols = Math.Clamp(cols, 4, 50);
    var safeAisle = Math.Clamp(aisleWidth, 0, 4);
    var centerStart = safeAisle > 0 ? Math.Max(0, (safeCols / 2) - (safeAisle / 2)) : -1;
    var centerEnd = safeAisle > 0 ? Math.Min(safeCols - 1, centerStart + safeAisle - 1) : -1;
    var seats = new List<string>(safeRows * safeCols);
    for (var r = 0; r < safeRows; r++)
    {
        var rowCode = ((char)('A' + r)).ToString();
        for (var c = 0; c < safeCols; c++)
        {
            if (safeAisle > 0 && c >= centerStart && c <= centerEnd) continue;
            seats.Add($"{rowCode}{c + 1}");
        }
    }
    return JsonSerializer.Serialize(new { rows = safeRows, cols = safeCols, seats });
}
```

Ad esempio, una sala 11 file x 18 posti con corridoio largo 2 produce posti come `A1, A2, ..., A8, A11, ..., A18, B1, ...` (i posti 9 e 10 sono il corridoio).

#### Utente — Sicurezza avanzata

```csharp
// Model/Utente.cs — Campi di sicurezza
public class Utente
{
    public string? PasswordHash { get; set; }             // BCrypt hash
    public string Ruolo { get; set; } = RuoloUtente.Utente; // "admin", "power_user", "utente"
    public int AuthVersion { get; set; } = 1;             // incrementato a ogni cambio password
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime? PasswordChangedAtUtc { get; set; }   // per invalidare token emessi prima
    public decimal CreditoPiattaforma { get; set; }        // saldo spendibile
    public int FailedLoginAttempts { get; set; }          // per lockout progressivo
    public DateTime? LockoutEndUtc { get; set; }           // lockout temporaneo
    public bool IsDisabled { get; set; }                   // account disabilitato dall'admin
}
```

#### Prenotazione — Tracking completo

```csharp
// Model/Prenotazione.cs
public class Prenotazione
{
    public string PostiSelezionati { get; set; } = string.Empty; // es. "A3,A4,A5"
    public decimal TotalePrezzo { get; set; }
    public decimal ImportoCartaUsato { get; set; }              // pagamento misto
    public string CodiceAcquisto { get; set; } = string.Empty;   // formato: NFH-YYYYMMDDHHmmss-RANDOM
    public bool Validato { get; set; }                          // true dopo validazione al cinema
    public DateTime? ValidatoAtUtc { get; set; }
    public int? ValidatoDaUtenteId { get; set; }                // chi ha validato
    public int? CinemaValidazioneId { get; set; }               // dove e stato validato
    public string Stato { get; set; } = "Confermata";           // PendingStripe, Confermata, Annullata
}
```

#### SeatLock — Anti race-condition

Quando due utenti selezionano lo stesso posto contemporaneamente, il sistema usa un lock temporaneo con scadenza (TTL 8-10 minuti):

```csharp
// Model/SeatLock.cs
public class SeatLock
{
    public int ProiezioneId { get; set; }
    public int UtenteId { get; set; }
    public string PostoCodice { get; set; } = string.Empty; // es. "C7"
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }                // TTL 8-10 minuti
}
```

Il vincolo unique `(ProiezioneId, PostoCodice)` nel database impedisce doppie prenotazioni. Il `CleanupHostedService` si occupa di eliminare i lock scaduti periodicamente.

#### Carrello e Shop — E-commerce completo

```csharp
// Model/Cart.cs
public class Cart
{
    public string CartType { get; set; } = "Mixed";  // "Tickets", "Shop", "Mixed"
    public string Stato { get; set; } = "Active";    // Active, Abandoned, Converted
    public decimal Subtotale { get; set; }
    public decimal ScontoCoupon { get; set; }         // sconto applicato dal coupon
    public decimal Totale { get; set; }
    public string? GiftCardCode { get; set; }          // gift card applicata
    public decimal ImportoGiftCard { get; set; }       // importo coperto dalla gift card
    public DateTime ExpiresAtUtc { get; set; }        // scadenza carrello
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
}
```

```csharp
// Model/Product.cs — Prodotto shop
public class Product
{
    public string Sku { get; set; } = string.Empty;     // es. "NFH-TSH-M"
    public string Nome { get; set; } = string.Empty;    // es. "T-Shirt Noir Film Hub"
    public string Categoria { get; set; } = "Gadget";   // Food, Abbigliamento, Accessori, Gadget
    public decimal PrezzoBase { get; set; }
    public ICollection<ProductVariant> Varianti { get; set; } = new List<ProductVariant>();
}
```

```csharp
// Model/Coupon.cs — Sistema coupon avanzato
public class Coupon
{
    public string Codice { get; set; } = string.Empty;    // es. "NFH-BENVENUTO"
    public string TipoSconto { get; set; } = "Fisso";     // "Fisso" o "Percentuale"
    public decimal ValoreSconto { get; set; }              // es. 10.00 (10 EUR o 10%)
    public string TipoTarget { get; set; } = "Carrello";  // "Carrello", "Cinema", "Category"
    public int? TargetId { get; set; }                     // ID del target specifico
    public decimal? MinImportoCarrello { get; set; }       // importo minimo per attivare
    public int MaxUtilizzi { get; set; }                   // numero massimo totale
    public int MaxPerUtente { get; set; } = 1;             // numero massimo per utente
    public bool Stackable { get; set; }                   // cumulabile con altri coupon
}
```

---

## 4. Autenticazione e Sicurezza — Approfondimento

### 4.1 Flusso JWT completo

Il sistema di autenticazione e basato su JSON Web Token con tre livelli di sicurezza:

1. **Access Token** (scadenza 15 minuti): contiene ID utente, email, ruolo, `auth_version` e `security_stamp`
2. **Refresh Token** (scadenza 7 giorni): token opaco memorizzato nel database, permette di ottenere un nuovo Access Token senza rifare login
3. **OnTokenValidated middleware** (in `Program.cs`): ad ogni richiesta autenticata verifica:
   - L'utente esista ancora nel database
   - L'utente non sia disabilitato (`IsDisabled == false`)
   - L'`auth_version` nel token corrisponda a quello nel database
   - Se `PasswordChangedAtUtc` e successivo all'emissione del token (`iat` claim), il token viene rifiutato

Esempio di validazione nel middleware:

```csharp
// Program.cs — OnTokenValidated
if (utente.AuthVersion.ToString() != authVersionClaim)
{
    context.Fail("Token invalidato - sessione scaduta");
    return;
}
if (utente.PasswordChangedAtUtc.HasValue)
{
    var iat = DateTimeOffset.FromUnixTimeSeconds(long.Parse(iatClaim)).UtcDateTime;
    if (utente.PasswordChangedAtUtc.Value > iat)
    {
        context.Fail("Password cambiata - token invalidato");
        return;
    }
}
```

### 4.2 Social Login (Google e Microsoft OIDC)

Il flusso di social login e stato progettato con sicurezza anti-replay:

1. Il frontend richiede `GET /auth/external/{provider}` (Google o Microsoft)
2. Il backend genera un `ExternalAuthState` con un `ExternalAuthExchangeCode` (anti-replay), poi redirecta al provider OIDC
3. Il provider callback su `GET /auth/external/callback`
4. Il backend scambia il codice OIDC per i claim del provider, verifica l'anti-replay code, e:
   - Se l'utente esiste gia: collega il provider all'account esistente
   - Se l'utente non esiste: crea un nuovo account con role `utente`

Regole di sicurezza:
- Un account social-only non puo essere promosso a PowerUser/Admin (manca verifica email locale)
- Se e l'unico metodo di accesso, non si puo scollegare il provider social
- PowerUser/Admin devono avere password locale abilitata

### 4.3 Audit Trail

Ogni evento di sicurezza viene tracciato in `UserSecurityAuditLog`:

```csharp
// Model/UserSecurityAuditLog.cs (implied)
public class UserSecurityAuditLog
{
    public int? UtenteId { get; set; }
    public string EventType { get; set; }   // LoginSuccess, LoginFailed, PasswordChanged, AccountDisabled, ecc.
    public string? Provider { get; set; }   // "Local", "Google", "Microsoft"
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Details { get; set; }   // JSON con dettagli aggiuntivi
    public DateTime CreatedAtUtc { get; set; }
}
```

Eventi tracciati: login riuscito/fallito, registrazione, cambio/reset/setup password, social login collegato/scollegato, disabilitazione/abilitazione account, cambio ruolo, invito utente, logout globale.

---

## 5. Flusso di Acquisto Biglietti — End-to-End

Il flusso completo di acquisto segue questi passaggi:

```
1. Esplorazione          2. Selezione Show           3. Mappa Posti
programmazione.html  →   scheda-film.html        →   acquista.html
 Film, filtri,          Calendario date,            Mappa interattiva,
 tab, cinema            orario per sala             stati: libero/bloccato/occupato

4. Lock Posti           5. Pagamento                6. Conferma
POST /checkout/locks → pagamento.html           → Stripe Checkout
 Blocca per 8-10 min    Carta/Credito/Misto        Redirect con esito

7. Emissione             8. Validazione
PDF biglietto         → validazione-biglietti.html
 Email + barcode/QR     operatore scannerizza e vidima
```

### 5.1 Selezione posti e mappa interattiva

L'endpoint `GET /checkout/seats/{proiezioneId}` restituisce lo stato di ogni posto:

```csharp
// Endpoints/CheckoutEndpoints.cs — Logica mappa posti
var sold = ExpandSeats(soldRows);                       // posti gia venduti
var lockedByOthers = locks.Where(l => l.UtenteId != userId)
    .Select(l => l.PostoCodice).ToHashSet();            // posti bloccati da altri
var myLocks = locks.Where(l => l.UtenteId == userId)
    .Select(l => l.PostoCodice).ToHashSet();             // miei posti bloccati
var vipSeats = SeatPricingUtils.GetVipSeats(...);       // posti con supplemento VIP
```

Il frontend renderizza ogni posto con un colore diverso: libero (verde), bloccato da altri (rosso), occupato (grigio), mio (blu), VIP (dorato).

### 5.2 Pagamento — Tre modalita

1. **Solo carta**: l'importo totale va a Stripe tramite Hosted Checkout Session
2. **Solo credito**: se il `CreditoPiattaforma` dell'utente copre l'intero importo, il pagamento e immediato senza Stripe
3. **Misto**: il credito copre parte dell'importo, la differenza va a Stripe. Entrambi gli importi sono registrati nella prenotazione

Dopo il pagamento, Stripe notifica il backend tramite webhook (`POST /pagamenti/stripe/webhook`), che aggiorna lo stato della prenotazione da `PendingStripe` a `Confermata`.

### 5.3 Emissione biglietto PDF

Il servizio `TicketPdfService` genera undocumento PDF con QuestPDF. Per ogni posto prenotato, una pagina A4 include:

- Intestazione "NOIR FILM HUB - BIGLIETTO ELETTRONICO"
- Titolo film, data/ora, sala, posto, tipo evento
- Nome e codice locale del cinema, indirizzo
- Breakdown del prezzo (base + supplemento sala)
- Barcode Code128 del `CodiceAcquisto`
- QR code con URL di validazione

---

## 6. Sistema Shop e Carrello

### 6.1 Prodotti e varianti

I prodotti dello shop sono organizzati per categoria (Food, Abbigliamento, Accessori, Gadget) con varianti (taglie, capacita):

```csharp
// Esempio dal seed (Program.cs)
new Product { Sku = "NFH-TSH-M", Nome = "T-Shirt Noir Film Hub",
              Categoria = "Abbigliamento", PrezzoBase = 19.90m };
// Varianti: S, M, L, XL con sku "NFH-TSH-M-S", "NFH-TSH-M-M", ecc.
```

Il sistema gestisce riserva inventario tramite `InventoryReservation`: quando un prodotto viene aggiunto al carrello, la quantita viene riservata e decrementata dallo stock disponibile. Se il carrello scade, le riserve vengono rilasciate.

### 6.2 Gift Card

Le gift card hanno un template (10, 20, 30, 50 EUR) e generano un codice univoco al momento dell'acquisto. Il saldo residuo e tracciato tramite `GiftCardTransaction`:

```csharp
// Model/GiftCard.cs
public class GiftCard
{
    public decimal ImportoIniziale { get; set; }
    public decimal SaldoResiduo { get; set; }        // decrementato ad ogni utilizzo
    public string Stato { get; set; } = "Active";     // Active, Redeemed, Expired
    public ICollection<GiftCardTransaction> Transazioni { get; set; }
}
```

### 6.3 Coupon sistema

Il sistema coupon supporta diverse configurazioni:

| Parametro | Valori | Esempio |
|-----------|--------|---------|
| `TipoSconto` | `"Fisso"` o `"Percentuale"` | 5 EUR fisso, oppure 10% di sconto |
| `TipoTarget` | `"Carrello"`, `"Cinema"`, `"Category"` | Sconto su tutto, su un cinema specifico, o su una categoria |
| `MinImportoCarrello` | decimale nullable | Richiede un minimo di acquisto |
| `MaxUtilizzi` | intero | Massimo utilizzi totali |
| `MaxPerUtente` | intero | Massimo utilizzi per singolo utente |
| `Stackable` | booleano | Cumulabile con altri coupon |

---

## 7. Integrazione TMDB

Il `TmdbService` si integra con The Movie Database API v3 per:

- **Sync singolo**: arricchisce un film locale con dati TMDB (trama, cast, poster, trailer)
- **Sync batch**: sincronizza tutti i film con `TmdbSyncStato != "Synced"`
- **Ricerca live**: cerca film per titolo su TMDB
- **Import**: importa film da TMDB direttamente nel database locale
- **Job notturno**: `TmdbSyncHostedService` esegue una sincronizzazione automatica ogni notte all'orario configurato

Il token API TMDB e memorizzato solo lato server nella variabile `TMDB_API_READ_TOKEN` e non viene mai esposto al frontend.

---

## 8. Servizi di Background

### 8.1 CleanupHostedService

Servizio eseguito periodicamente che:
- Elimina `ExternalAuthState` e `ExternalAuthExchangeCode` scaduti
- Elimina `AccountActionToken` scaduti (reset password, inviti)
- Elimina `SeatLock` scaduti (posto non piu bloccato)
- Elimina carrelli `Cart` scaduti (`ExpiresAtUtc < now`)

### 8.2 TmdbSyncHostedService

Esegue la sincronizzazione TMDB all'orario configurato (`TMDB_SYNC_HOUR`, default 03:00), aggiornando copertina, backdrop, trailer, trama e cast per tutti i film che non sono ancora sincronizzati.

---

## 9. API Endpoints — Riepilogo per Gruppo

| Gruppo | Endpoint Base | Ruolo minimo | Funzione |
|--------|-------------|-------------|----------|
| Auth | `/auth/*` | Pubblica/Admin | Registrazione, login, logout, gestione profilo, admin utenti |
| Registi | `/registi/*` | Pubblica/PowerUser | CRUD registi |
| Films | `/films/*` | Pubblica/PowerUser | CRUD film |
| Cinema | `/cinemas/*` | Pubblica/Admin | CRUD cinema, ricerca nearby |
| Sale | `/sale/*` | Pubblica/PowerUser | CRUD sale |
| Categorie | `/categorie/*` | Pubblica/Admin | CRUD categorie |
| Proiezioni | `/proiezioni/*` | Pubblica/PowerUser | CRUD proiezioni |
| Programmazione | `/programmazione/*` | Pubblica | Catalogo pubblico (show, film, calendario) |
| My Cinemas | `/my-cinemas/*` | Pubblica | Ricerca cinema, programmazione giornaliera |
| Checkout | `/checkout/*` | Utente | Mappa posti, lock posti |
| Pagamenti | `/pagamenti/*` | Utente | Stripe checkout, esito pagamento, webhook |
| Prenotazioni | `/prenotazioni/*` | Utente/Admin | CRUD prenotazioni |
| Biglietti | `/tickets/*` | Utente/PowerUser | Dettaglio, validazione, PDF |
| TMDB | `/tmdb/*` | PowerUser | Sync, ricerca, import |
| Shop | `/shop/*` | Pubblica | Prodotti, varianti |
| Cart | `/cart/*` | Utente (facoltativo) | Carrello, aggiunta/rimozione |
| Coupons | `/coupons/*` | Utente | Validazione e applicazione coupon |
| Gift Cards | `/giftcards/*` | Utente | Acquisto e utilizzo gift card |

---

## 10. Flussi per Ruolo

### 10.1 Visitatore anonimo

- Navigare la programmazione
- Vedere dettaglio film con trailer e cast
- Cercare cinema vicini (geolocalizzazione)
- Vedere mappa posti (sola lettura)
- Registrarsi o effettuare login
- Validare biglietto via QR (sola lettura)

### 10.2 Utente autenticato (`utente`)

Tutto il sopra, piu:
- Acquistare biglietti con selezione posti e pagamento
- Scaricare PDF biglietti
- Gestire profilo e cinema preferito
- Cambiare password, collegare/scollegare social login
- Annullare proprie prenotazioni
- Acquistare prodotti nello shop
- Applicare coupon e gift card
- Vedere storico ordini

### 10.3 PowerUser

Tutto il sopra, piu:
- CRUD film, registi, proiezioni, sale
- Validare biglietti al cinema (con vincolo di appartenenza)
- Sincronizzare TMDB
- Gestire programmazione

### 10.4 Admin

Tutto il sopra, piu:
- CRUD cinema e categorie
- Gestione completa utenti (ricerca, filtri, promozione, disabilitazione, eliminazione)
- Invitare nuovi utenti con ruolo specificato
- Vedere tutte le prenotazioni

---

## 11. Configurazione e Deployment

### 11.1 Variabili d'ambiente (.env)

Il file `.env` contiene tutte le configurazioni sensibili:

```env
# Database MariaDB
DB_HOST=localhost
DB_PORT=3306
DB_NAME=film-api-db
DB_USER=root
DB_PASSWORD=root

# Autenticazione
JWT_SECRET_KEY=your-secret-key-min-32-chars    # minimo 32 caratteri
JWT_ISSUER=FilmAPI
JWT_AUDIENCE=FilmFrontend
AUTH_ENABLED=true                               # false per test senza auth

# Stripe (pagamenti)
STRIPE_SECRET_KEY=sk_test_...
STRIPE_WEBHOOK_SECRET=whsec_...
STRIPE_ENABLED=true

# TMDB (arricchimento film)
TMDB_API_READ_TOKEN=eyJhbGciOi...
TMDB_SYNC_ENABLED=true
TMDB_SYNC_HOUR=03                                # sincronizza alle 3 di notte

# Email (SMTP)
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USER=...
SMTP_PASSWORD=...
SMTP_FROM=noreply@filmapi.local

# Google OIDC
GOOGLE_CLIENT_ID=...
GOOGLE_CLIENT_SECRET=...

# Microsoft OIDC
MICROSOFT_CLIENT_ID=...
MICROSOFT_CLIENT_SECRET=...
```

### 11.2 Comandi principali

```bash
# Avvia database
docker compose up -d

# Avvia backend (porta 5000)
dotnet run

# Avvia frontend (porta 5001, dalla cartella FilmFrontend)
cd FilmFrontend && dotnet run

# Esegui test
dotnet test tests/FilmAPI.Tests.csproj

# Reset utenti (cancella tutto e ricrea solo l'admin)
# Impostare RESET_USERS=true nel .env, poi riavviare
```

### 11.3 Test in modalita senza autenticazione

Impostando `AUTH_ENABLED=false` nel `.env`, il backend usa un `TestAuthHandler` che simula un utente autenticato senza richiedere JWT. Questo e utile per testare rapidamente le API senza implementare il flusso di login.

---

## 12. Riepilogo Architetturale

```
┌─────────────────────────────────────────────────────────────────┐
│                        Browser (FilmFrontend)                    │
│  HTML/CSS/JS · Inter + Poppins · Dark/Light theme · Fetch API   │
│                         :5001                                    │
└────────────────────────────┬────────────────────────────────────┘
                             │ HTTP REST (JSON)
                             │ CORS AllowFilmFrontend
┌────────────────────────────▼────────────────────────────────────┐
│                        FilmAPI (Backend)                         │
│  ASP.NET Core 9 Minimal API · EF Core · Swagger                  │
│  Auth: JWT Bearer + Google/Microsoft OIDC                        │
│                         :5000                                    │
│                                                                  │
│  ┌──────────┐ ┌───────────┐ ┌──────────┐ ┌─────────────────┐  │
│  │Endpoints  │ │ Services   │ │ Models   │ │ DTOs              │  │
│  │(18 gruppi)│ │(14 servizi)│ │(27 classi)│ │(34 DTO)          │  │
│  └──────────┘ └───────────┘ └──────────┘ └─────────────────┘  │
│                                                                  │
│  ┌──────────────────── Servizi ──────────────────────┐          │
│  │ AuthService · JwtTokenService · PasswordService     │          │
│  │ SocialAuthService · SecurityAuditService              │          │
│  │ EmailService · TicketPdfService · TicketEmailService │          │
│  │ TmdbService · CartService · SeatPricingUtils         │          │
│  │ CleanupHostedService · TmdbSyncHostedService         │          │
│  └──────────────────────────────────────────────────────┘          │
│                                                                  │
│  Stripe API ─── TMDB API ─── Google OIDC ─── Microsoft OIDC    │
│  (pagamenti)    (film data)   (social login)   (social login)  │
│                                                                  │
│  SMTP (email)    QuestPDF+ZXing (PDF ticket)                     │
└────────────────────────────┬────────────────────────────────────┘
                             │ EF Core + Pomelo.MySql
┌────────────────────────────▼────────────────────────────────────┐
│                        MariaDB 10.11 (Docker)                    │
│                         :3306                                    │
│  25+ tabelle · Migrazioni automatiche · Seed iniziale            │
└─────────────────────────────────────────────────────────────────┘
```

---

*Ultimo aggiornamento: maggio 2026 — copre tutte le funzionalita delle Iterazioni 1-5.*