# Project Status

Ultimo aggiornamento: 2026-05-10

## Iterazione 5 - Stato sintetico

Completato:
- Evoluzione modello dati Utente con 12 nuovi campi (NormalizedEmail, LocalCredentialsEnabled, AuthVersion, SecurityStamp, IsDisabled, LastLoginAtUtc, LastLoginProvider, EmailVerified, CreatedAtUtc, CreditoPiattaforma, FailedLoginAttempts, LockoutEndUtc) + PasswordHash nullable
- Nuove entita: UserExternalLogin, AccountActionToken, ExternalAuthState, ExternalAuthExchangeCode, UserSecurityAuditLog
- Social login Google OpenID Connect e Microsoft OpenID Connect multi-tenant con callback sicuro e anti-replay
- Linking ibrido account (social + password) con regole di sicurezza (PowerUser/Admin non social, social-only non promuovibili)
- Password management completo: cambio password autenticato, forgot password con anti-enumerazione, reset password con token single-use, setup password per account social-only (autenticato e via email)
- JWT hardening: AuthVersion con invalidazione globale, OnTokenValidated middleware con controlli DB, refresh token revocabili, logout globale e selettivo
- Admin avanzato: listing utenti con filtri/ricerca/paginazione, dettaglio sicurezza, promozione/degradazione con regole, disable/enable, force password reset, invito admin/poweruser
- Servizio email con template HTML per reset, setup, invito, cambio ruolo, cambio password, alert sicurezza + retry policy
- Audit sicurezza: UserSecurityAuditLog con tutti gli eventi di sicurezza tracciati
- CleanupHostedService per pulizia periodica ExternalAuthState, token scaduti
- Migrazione EF Core: Iteration5_IdentityAndSecurity
- Configurazione JWT spostata in .env e centralizzata
- Frontend aggiornato:
  - auth-service.js esteso con social login, password management, admin functions
  - auth-guard.js esteso con requireAdmin
  - api-client.js con gestione HTTP 429 rate limiting
  - login.html e register.html con pulsanti Google/Microsoft
  - social-login-complete.html per callback social
  - recupera-password.html, reimposta-password.html, setup-password.html
  - profile.html con sezione sicurezza, cambio password, revoca sessioni
  - utenti.html riscritto con interfaccia admin avanzata (ricerca, filtri, paginazione, azioni, invito, dettaglio)
  - navbar.html aggiornata con nuovi link
- .env e .env.example aggiornati con tutte le nuove variabili

Da completare (backlog Iterazione 5):
- Rate limiting middleware su endpoint auth
- Brute force protection (delay progressivo, lockout)
- Content Security Policy headers
- Configurazione reale Google/Microsoft OIDC (client ID/secret)
- Suite test completa (unit, integration, E2E, security)
- Documentazione API Swagger aggiornata

## Iterazione 4 - Stato sintetico

Completato:
- Programmazione pubblica con tab, ricerca, filtro categoria e preferenza cinema.
- Scheda film pubblica con calendario show e bottoni orario multi-sala.
- Funnel posti (acquista.html) con seat lock TTL e prevenzione race condition.
- Pagamento Stripe Hosted Checkout con webhook di finalizzazione ticket.
- Validazione biglietti con vincolo cinema operatore e blocco doppia validazione.
- Ricariche credito con lookup utente per email/id e audit operatore.
- Integrazione TMDB (sync manuale + job notturno).

Da completare (backlog Iterazione 4):
- Emissione PDF ticket completa con barcode Code128 + QR embedded.
- Invio email post-acquisto con allegato PDF ticket.
- Scanner barcode/QR nativo in validazione-biglietti.html.
- Rate limit endpoint sensibili e test E2E automatici.
