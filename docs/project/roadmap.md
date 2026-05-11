# Roadmap

## Iterazione 1 - Setup Progetto
- [x] Progetto ASP.NET Core Minimal API (FilmAPI)
- [x] Modello dati base (Regista, Film, Cinema, Proiezione)
- [x] CRUD completo via endpoint REST
- [x] Migrazioni EF Core + MariaDB
- [x] Swagger / OpenAPI

## Iterazione 2 - Frontend
- [x] Progetto FilmFrontend (static files server)
- [x] Pagine CRUD (registi, films, cinemas, proiezioni)
- [x] Componenti navbar/footer, API client JS
- [x] Layout responsive

## Iterazione 3 - Autenticazione JWT e RBAC
- [x] Modello Utente con ruoli (Admin, PowerUser, Utente)
- [x] Autenticazione JWT con access/refresh token
- [x] Protezione endpoint con RBAC
- [x] Categorie film (many-to-many)
- [x] Sistema prenotazioni
- [x] Frontend login/register/profile/utenti

## Iterazione 4 - Programmazione, Ticketing e Pagamenti
- [x] Cinema territoriali multi-sala
- [x] Programmazione pubblica con filtri, tab, ricerca
- [x] Scheda film con show per giorno e tipologia sala
- [x] Acquisto posti con seat lock e anti-race-condition
- [x] Pagamento Stripe + credito piattaforma
- [ ] PDF ticket con barcode/QR
- [x] Validazione biglietti
- [x] Integrazione TMDB

## Iterazione 5 - Identity & Security Enterprise-Grade
- [x] Estensione modello Utente (AuthVersion, IsDisabled, SecurityStamp, etc.)
- [x] Social login Google OIDC
- [x] Social login Microsoft OIDC multi-tenant
- [x] Account ibridi social + password
- [x] Password management (change, forgot, reset, setup)
- [x] JWT hardening (AuthVersion, OnTokenValidated, invalidazione)
- [x] Admin avanzato (filtri, ricerca, invite, disable/enable)
- [x] Servizio email con template HTML
- [x] Audit sicurezza (UserSecurityAuditLog)
- [x] Frontend aggiornato (social-login, pagine password, utenti admin)
- [ ] Rate limiting e brute force protection
- [ ] Suite test completa
