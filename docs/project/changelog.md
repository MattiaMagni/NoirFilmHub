# Changelog

## 2026-04-27

### Added
- `GET /programmazione/shows` con filtri `filmId`, `cinemaId`, `day` e output raggruppato per tipologia sala.
- Gestione query compatibile `IdCinema` in `my-cinemas.html` oltre a `idCinema`.
- Alias `POST /pagamenti/conferma` riallineato al flusso Stripe Hosted Checkout.

### Changed
- Documentazione stato progetto aggiornata con gap residui Iterazione 4.

### Verified
- Build `FilmAPI` riuscita.
- Test suite `FilmAPI.Tests`: 62/62 passati.
