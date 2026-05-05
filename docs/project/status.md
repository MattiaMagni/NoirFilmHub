# Project Status

Ultimo aggiornamento: 2026-04-27

## Iterazione 4 - Stato sintetico

Completato:
- Programmazione pubblica con tab, ricerca, filtro categoria e preferenza cinema.
- Scheda film pubblica con calendario show e bottoni orario multi-sala (anche duplicati per tipologia).
- Funnel posti (`acquista.html`) con seat lock TTL e prevenzione race condition.
- Pagamento Stripe Hosted Checkout (redirect esterno) con webhook di finalizzazione ticket.
- Validazione biglietti con vincolo cinema operatore e blocco doppia validazione.
- Ricariche credito con lookup utente per email/id e audit operatore.
- Integrazione TMDB (sync manuale + job notturno).

Completato in questo aggiornamento:
- Aggiunto endpoint `GET /programmazione/shows` per programma per giorno/film/cinema.
- Aggiunta retrocompatibilita su query `my-cinemas.html?IdCinema=` oltre a `idCinema`.
- Ripristinato endpoint `POST /pagamenti/conferma` come alias Stripe-only (redirect hosted).

Da completare (backlog Iterazione 4):
- Emissione PDF ticket completa (1 pagina per biglietto) con barcode Code128 + QR embedded.
- Invio email post-acquisto con allegato PDF ticket.
- Scanner barcode/QR nativo in `validazione-biglietti.html` (al momento supporto manuale + prefill da URL).
- Hardening tecnico: rate limit endpoint sensibili e test E2E automatici completi dei flussi.
