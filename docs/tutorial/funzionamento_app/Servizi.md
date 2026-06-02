# Servizi del Backend

Panoramica dei 14 servizi implementati in `Services/`, ordinati per area funzionale.

---

## Autenticazione e Identità

### `AuthService` (623 righe)

Il servizio centrale di autenticazione. Gestisce l'intero ciclo di vita degli utenti:

| Funzione | Descrizione |
|---|---|
| `RegisterAsync` | Registrazione nuovo utente con validazione password e normalizzazione email |
| `LoginAsync` | Login con credenziali locali, lockout dopo 10 tentativi falliti, alert dopo 5 |
| `RefreshAsync` | Rinnovo access token tramite refresh token salvato su DB |
| `LogoutAsync` | Logout singolo dispositivo o globale (incrementa `AuthVersion`) |
| `ChangePasswordAsync` | Cambio password con invalidazione di tutte le sessioni esistenti |
| `ForgotPasswordAsync` | Genera token SHA256 monouso e invia email di recupero |
| `ResetPasswordAsync` | Consuma il token e imposta la nuova password, sblocca l'account |
| `RequestPasswordSetupAsync` | Per account social-only: invia link per impostare una password locale |
| `SetupPasswordAsync` | Attiva le credenziali locali per un account social-only |
| `InviteUserAsync` | Crea utenza disabilitata e invia email di invito con link setup |
| `GetUsersAsync` | Lista utenti paginata con filtri (ricerca, ruolo, stato, ordinamento) |
| `GetUserDetailAsync` | Dettaglio utente con log di sicurezza recenti |
| `ChangeUserRoleAsync` | Promozione/degradazione ruolo con protezione ultimo admin |
| `DisableUserAsync` / `EnableUserAsync` | Disabilitazione/riabilitazione account |
| `ForcePasswordResetAsync` | Forza il reset password per un utente |
| `DeleteUserAsync` | Eliminazione utente con audit log dei dati |
| `RevokeAllSessionsAsync` | Revoca tutte le sessioni attive di un utente |

---

### `PasswordService` (45 righe)

Servizio stateless per hashing e validazione password:

- `HashPassword` — bcrypt (via BCrypt.Net)
- `VerifyPassword` — verifica hash bcrypt
- `IsStrongPassword` — valida: >=8 caratteri, 1 maiuscola, 1 minuscola, 1 numero, 1 carattere speciale

---

### `JwtTokenService` (69 righe)

Generazione token JWT:

- `GenerateAccessToken` — JWT firmato HMAC-SHA256 con claims: `sub`, `email`, `role`, `ruolo`, `auth_version`, `security_stamp`. Scadenza configurabile (default 15 min)
- `GenerateRefreshToken` — stringa casuale Base64 di 64 byte
- `GetRefreshExpiryUtc` — calcola scadenza refresh token (default 7 giorni)

---

### `SocialAuthService` (335 righe)

Login sociale OAuth2 per Google e Microsoft:

- `InitiateAsync` — genera URL di autorizzazione OAuth2 e salva lo stato su DB
- `HandleCallbackAsync` — scambia il `code` con il provider, decodifica l'`id_token` JWT, cerca/crea l'utente
- `LinkExternalLoginAsync` — collega un provider esterno a un account esistente
- `UnlinkExternalLoginAsync` — scollega un provider (impedito se è l'unico metodo di accesso)
- `GetExternalLoginsAsync` — elenco provider collegati per un utente
- Filtro dominio Microsoft: accesso consentito solo da domini configurati in `MICROSOFT_ALLOWED_DOMAINS`
- Admin e PowerUser non possono usare social login
- Nuovi utenti social ricevono email per impostare una password

---

### `TestAuthHandler` (28 righe)

Handler di autenticazione fittizio per ambienti di test/sviluppo. Quando `AUTH_ENABLED=false`, tutte le richieste sono autenticate come Admin (ID 9999). Sostituisce la validazione JWT reale.

---

## Carrello e Acquisti

### `CartService` (289 righe)

Gestione carrello acquisti con supporto guest e utenti autenticati:

- `GetOrCreateCartAsync` — recupera o crea carrello attivo per utente/guest. Ripristina carrelli in stato "Checkout" se il pagamento non è in corso
- `RecalculateAsync` — ricalcola subtotale, sconto coupon, importo gift card e totale
- `RemoveExpiredTicketItemsAsync` — rimuove biglietti con seat lock scaduti, aggiorna la quantità nei metadati JSON
- `MergeGuestCartAsync` — unisce il carrello guest nel carrello utente dopo il login
- `ApplyCouponAsync` — applica un coupon con validazione: data, utilizzi, target (film/cinema), importo minimo, quantità minima, stacking
- `RemoveCouponAsync` — rimuove il coupon e ricalcola
- `CalculateDiscount` — calcola sconto percentuale (con massimale) o fisso

---

### `SeatPricingUtils` (121 righe)

Utility statica per la logica di pricing dei posti a sedere:

- `GetVipSeats` — identifica i posti VIP in una sala: fascia centrale (35%-75% delle file), laterali al corridoio centrale
- `CalculateTotal` — calcola il totale: `prezzoBase * numeroPosti + supplementoVIP * postiVIP`
- `TryParseSeatCode` — decodifica codici posto tipo "A14" in coordinate riga/colonna
- Il corridoio centrale è largo 2 colonne; i posti VIP sono le colonne adiacenti (±18% della larghezza sala)

---

## Email e Comunicazioni

### `EmailService` (265 righe)

Invio email transazionali via SMTP (MailKit):

| Metodo | Contesto |
|---|---|
| `SendPasswordResetEmail` | Link recupero password (scade 1h) |
| `SendPasswordSetupEmail` | Link impostazione password (scade 24h) |
| `SendAdminInviteEmail` | Invito amministratore (scade 72h) |
| `SendRoleChangedEmail` | Notifica cambio ruolo |
| `SendPasswordChangedEmail` | Conferma cambio password |
| `SendSecurityAlertEmail` | Alert tentativi sospetti |
| `SendGiftCardEmail` | Notifica ricezione gift card |
| `SendGiftCardBalanceEmail` | Notifica saldo residuo gift card |
| `SendOrderConfirmationEmail` | Riepilogo ordine completato |
| `SendCouponRedeemEmail` | Notifica riscatto coupon |
| `SendCancellationRefundEmail` | Rimborso con codice gift card |

Tutte le email usano template HTML con header CineBase e fallback plain-text.

---

### `TicketEmailService` (64 righe)

Invio email con PDF biglietti in allegato. Usa `System.Net.Mail.SmtpClient` (invece di MailKit) per inviare allegati PDF. Supporta solo STARTTLS su porta 587; la porta 465 viene rifiutata.

---

### `TicketPdfService` (92 righe)

Generazione PDF biglietti con QuestPDF:

- Un biglietto per ogni posto prenotato, in formato A4
- Include: titolo film, data/ora, sala, posto, cinema, codice locale, prezzo, codice acquisto
- Genera codice a barre (Code128) del codice acquisto
- Genera QR code con URL di validazione (`/tickets/validate/{codice}`)
- Usa ZXing per barcode e SkiaSharp per il rendering PNG

---

## Integrazione TMDB

### `TmdbService` (587 righe)

Integrazione con l'API di The Movie Database:

- `GetLatestReleasesAsync` — ultime uscite in Italia (ultimo anno), ordinate per data. Filtra film senza poster
- `SearchMoviesByTitleAsync` — ricerca per titolo su TMDB
- `ImportMoviesAsync` — importa selezionati nel catalogo: recupera dettagli, crea Film, risolve/crea il regista, assegna categoria di default
- `SyncFilmAsync` — sincronizza un singolo film: cerca TMDB ID, aggiorna descrizione, cast, trailer, poster, regista
- `SyncMissingAsync` — sincronizza in batch tutti i film con dati mancanti
- `SearchMovieIdAsync` — cerca TMDB ID per titolo e anno
- `ResolveDirectorIdAsync` — cerca o crea il regista dai dati TMDB (split nome/cognome)
- `AssignDefaultCategoryIfNeededAsync` — assegna categoria "Cinema" se il film non ne ha
- `BuildCast` — estrae i primi 8 attori dal cast TMDB
- `ResolveTrailer` — cerca trailer YouTube ufficiale, fallback a qualsiasi video YouTube

---

### `TmdbSyncHostedService` (62 righe)

Background service che esegue una sincronizzazione notturna:

- Controlla il flag `TMDB_SYNC_ENABLED`
- Calcola il delay fino all'ora configurata (`TMDB_SYNC_HOUR`, default 03:00)
- Esegue `SyncMissingAsync` su tutti i film con dati incompleti
- Logga successi e fallimenti

---

## Manutenzione e Sicurezza

### `SecurityAuditService` (57 righe)

Registrazione eventi di sicurezza:

- `LogEventAsync` — salva evento con utente, tipo, IP, user agent e dettagli
- `GetRecentLogsAsync` — recupera gli ultimi N eventi per un utente
- `CleanupOldLogsAsync` — elimina log vecchi: 90 giorni per eventi standard, 365 per eventi critici (cambio ruolo, eliminazione, disabilitazione)

---

### `CleanupHostedService` (105 righe)

Servizio hosted che esegue pulizie periodiche ogni 60 secondi:

- Rimuove seat lock scaduti dai carrelli attivi
- Elimina cart item senza lock attivi
- Scade carrelli vuoti o oltre TTL (7 giorni)
- Elimina seat lock orfani, inventory reservation scadute, external auth state scaduti
- Rimuove token azione consumati (1h) o scaduti (1 giorno)
- Best-effort: non lancia eccezioni verso l'esterno
