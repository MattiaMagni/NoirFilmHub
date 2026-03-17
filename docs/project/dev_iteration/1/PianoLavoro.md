# Piano di lavoro - FilmAPI

## 1) Setup progetto
- Creare un progetto ASP.NET Core Web API Minimal API chiamato `FilmAPI`.
- Verificare uso di `.NET 9`.
- Installare pacchetti NuGet richiesti:
  - `Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore` `9.0.11`
  - `Microsoft.AspNetCore.OpenApi` `9.0.11`
  - `NSwag.AspNetCore`
  - `Microsoft.EntityFrameworkCore.Design` `9.0.11`
  - `Pomelo.EntityFrameworkCore.MySql`
  - `DotNetEnv` (per caricamento automatico variabili da `.env`)

## 2) Struttura cartelle
- `Model/` -> entita EF Core
- `Data/` -> `FilmDbContext`
- `DTO/` -> DTO input/output
- `Endpoints/` -> classi endpoint divise per entita

## 3) Modello dati
Entita da implementare:
- `Regista(Id, Nome, Cognome, Nazionalita)`
- `Film(Id, Titolo, DataProduzione, RegistaId, Durata, CopertinaPath?, FilmatoPath?)`
- `Cinema(Id, Nome, Indirizzo, Citta)`
- `Proiezione(Id, CinemaId, FilmId, Data, Ora)`

Relazioni:
- `Regista` 1-N `Film`
- `Film` 1-N `Proiezione`
- `Cinema` 1-N `Proiezione`

Vincoli:
- PK autoincrementale su `Proiezione.Id`
- Vincolo `UNIQUE` su `(CinemaId, FilmId, Data, Ora)`

## 4) DbContext e configurazione EF Core
- Creare `FilmDbContext` in `Data/` con costruttore:
  - `public FilmDbContext(DbContextOptions<FilmDbContext> options) : base(options) { }`
- Configurare con Fluent API:
  - chiavi primarie
  - foreign key
  - unique index per Proiezione
- Configurare MariaDB con Pomelo in `Program.cs` usando `UseMySql(...)`.

## 5) Configurazione .env e secrets
- Creare file `.env.example` con variabili necessarie (senza valori sensibili reali).
- Creare file `.env` locale per sviluppo.
- Caricare automaticamente `.env` all'avvio applicazione (`DotNetEnv.Env.Load()`).
- Usare variabili ambiente per costruire la connessione DB:
  - host, port, db name, user, password.
- Definire in `.env` il path dell'immagine di copertina di default:
  - `DEFAULT_COVER_IMAGE_PATH=/media/defaults/cover-default.jpg`
- Mantenere fallback su `appsettings.json` se necessario.

## 6) Swagger / OpenAPI
- Configurare:
  - `AddOpenApi()`
  - `AddEndpointsApiExplorer()`
  - `AddOpenApiDocument(...)`
- In Development:
  - `UseOpenApi()`
  - `UseSwaggerUi(...)` su path `/swagger`.

## 7) DTO
Creare DTO senza navigation properties:
- `RegistaDTO`
- `FilmDTO` (con `CopertinaPath?` e `FilmatoPath?`)
- `CinemaDTO`
- `ProiezioneDTO`
- `DatiProiezioneDTO` (input create/update proiezioni, se distinto)

## 8) Endpoints con MapGroup (CRUD completo)
Creare classi in `Endpoints/` con extension methods:

- `RegistiEndpoints` -> `app.MapGroup("/registi")`
  - `GET /registi`
  - `GET /registi/{id}`
  - `POST /registi`
  - `PUT /registi/{id}`
  - `DELETE /registi/{id}`

- `FilmsEndpoints` -> `app.MapGroup("/films")`
  - `GET /films`
  - `GET /films/{id}`
  - `POST /films` (gestisce anche `CopertinaPath?` e `FilmatoPath?`)
  - `PUT /films/{id}` (gestisce anche `CopertinaPath?` e `FilmatoPath?`)
  - `DELETE /films/{id}`

- `CinemasEndpoints` -> `app.MapGroup("/cinemas")`
  - `GET /cinemas`
  - `GET /cinemas/{id}`
  - `POST /cinemas`
  - `PUT /cinemas/{id}`
  - `DELETE /cinemas/{id}`

- `ProiezioniEndpoints` -> `app.MapGroup("/proiezioni")`
  - `GET /proiezioni`
  - `GET /proiezioni/{id}`
  - `POST /proiezioni`
  - `PUT /proiezioni/{id}`
  - `DELETE /proiezioni/{id}`

## 9) Validazioni
- Verificare esistenza FK su create/update (`RegistaId`, `FilmId`, `CinemaId`).
- Gestire violazione vincolo unique proiezione.
- Salvare nel DB solo i path dei file media (niente blob binari).
- Se `CopertinaPath` e' nullo/vuoto, impostare automaticamente il path di default (`DEFAULT_COVER_IMAGE_PATH`, con fallback sicuro lato applicazione).
- `FilmatoPath` opzionale.
- Restituire codici HTTP coerenti (`200`, `201`, `204`, `400`, `404`, `409`).

## 10) Migrations e database
- Creare migration iniziale:
  - `dotnet ef migrations add InitialCreate`
- Applicare migration:
  - `dotnet ef database update`
- Includere nella migration le colonne opzionali `CopertinaPath` e `FilmatoPath` per `Film`.
- Se necessario:
  - `dotnet tool install --global dotnet-ef --version 9.0.11`

## 11) Verifica finale
- Avvio applicazione.
- Test completo CRUD via Swagger per tutte le entita.
- Verifica vincolo unique su proiezioni e controlli FK.
