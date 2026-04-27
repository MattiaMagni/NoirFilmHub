# Iterazione 4 - Integrazione TMDB (The Movie Database)

Questa guida spiega come usare TMDB nell'iterazione 4 per arricchire i film locali con:
- trailer
- descrizione lunga
- cast e regia
- immagini e metadati di popolarita

Riferimenti ufficiali:
- https://developer.themoviedb.org/docs/getting-started
- https://developer.themoviedb.org/docs/authentication-application

## 1) Quale chiave usare (risposta diretta)

Usa il **TMDB API Read Access Token** (Bearer token), non la chiave nel frontend.

In pratica:
- vai su `https://www.themoviedb.org/settings/api`
- genera/recupera il token di lettura
- salva il valore in variabile ambiente backend:

```env
TMDB_API_READ_TOKEN=eyJhbGciOiJIUzI1NiJ9....
```

Le chiamate backend verso TMDB devono includere:

```http
Authorization: Bearer <TMDB_API_READ_TOKEN>
```

## 2) Configurazione consigliata `.env`

Aggiungi queste variabili in `.env` (e in `.env.example` come template):

```env
TMDB_API_READ_TOKEN=
TMDB_BASE_URL=https://api.themoviedb.org/3
TMDB_LANGUAGE=it-IT
TMDB_FALLBACK_LANGUAGE=en-US
TMDB_REGION=IT
TMDB_SYNC_ENABLED=true
TMDB_SYNC_HOUR=03
```

Note:
- `TMDB_API_READ_TOKEN` e obbligatoria.
- Non inserire mai il token nei file `FilmFrontend/wwwroot/js/*`.

## 3) Flusso tecnico backend

1. Cerca film locale su TMDB:
   - `GET /search/movie?query={titolo}&year={anno}`
2. Recupera dettaglio completo:
   - `GET /movie/{id}?append_to_response=videos,credits,images,release_dates`
3. Mappa i campi nel DB locale.
4. Salva stato sync e timestamp.

## 4) Campi da arricchire

Per ogni film:
- ID e titolo originale TMDB
- overview (descrizione lunga)
- data uscita
- backdrop/poster
- rating e popolarita
- trailer preferito (YouTube, Trailer, official=true)
- cast principale
- crew principale (regia)

## 5) Strategia sync scelta

In iterazione 4 usiamo:
- **sync manuale** (da area admin/power)
- **sync notturna** (job schedulato)

Vantaggi:
- controllo operativo quando necessario
- aggiornamento continuo senza rallentare i CRUD normali

## 6) Endpoint interni consigliati

- `POST /tmdb/sync/film/{filmId}`
  - sincronizza un singolo film
- `POST /tmdb/sync/films`
  - sincronizza batch (es. solo film incompleti)
- `GET /tmdb/sync/status`
  - ultimo run, errori, conteggi

Accesso: Admin e PowerUser.

## 7) Errori comuni e fallback

- Film non trovato con `titolo+anno`:
  - fallback ricerca solo titolo
- Risultati ambigui:
  - marcare film come `NeedsReview` e richiedere conferma manuale
- Trailer non disponibile in `it-IT`:
  - fallback `en-US`
- Rate limit/rete:
  - retry con backoff e logging

## 8) Sicurezza

- Token TMDB solo lato server.
- Nessun endpoint frontend deve conoscere il token.
- Loggare errori senza stampare il token.

## 9) Verifica rapida

Checklist minima dopo setup:

- [ ] variabile `TMDB_API_READ_TOKEN` presente
- [ ] sync manuale di un film completata
- [ ] trailer e overview visibili nel dettaglio film
- [ ] cast/regista persistiti correttamente
- [ ] job notturno pianificato e attivo

---

Sintesi: la "funzione per la chiave API" e usare il **Bearer Read Access Token** in backend tramite variabile ambiente `TMDB_API_READ_TOKEN`, con chiamate server-to-server verso TMDB.
