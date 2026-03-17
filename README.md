# FilmAPI

Backend Minimal API ASP.NET per gestire Registi, Films, Cinemas e Proiezioni (MariaDB).

Quick status
- Project: `FilmAPI` (net9)
- DB: MariaDB in `docker-compose.yml` (uses `.env`)
- Provider: Pomelo.EntityFrameworkCore.MySql 9.0.0
- Migrations: `Migrations/InitialCreate` generated and applied
- App entry: `Program.cs` (auto-migrate at startup)

Getting started
1. Copy `.env.example` to `.env` and adjust values if needed.
2. Start MariaDB with Docker:
```bash
docker compose up -d
```
3. Restore & build (from project root):
```bash
dotnet restore
dotnet build
```
4. Generate/apply migrations (already done):
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```
5. Run the app:
```bash
dotnet run
```

Access API
- Swagger UI (dev): `http://localhost:5000/swagger`
- Root health: `http://localhost:5000/`

Notes about proxies
- If you hit a proxy (Squid) when using `curl` for `localhost`, bypass it with `--noproxy`:
```bash
curl --noproxy localhost http://localhost:5000/registi
```
Or set `NO_PROXY` / `no_proxy` environment variable to include `localhost,127.0.0.1`.

Features
- Film entity has optional fields: `CopertinaPath`, `FilmatoPath`
- Default cover image path via `DEFAULT_COVER_IMAGE_PATH` env variable
- Automatic fallback to default cover if not provided

Endpoints (CRUD groups)
- `/registi` (GET, GET {id}, POST, PUT {id}, DELETE {id}) + `/registi/{id}/films` (GET, POST)
- `/films` (GET, GET {id}, POST, PUT {id}, DELETE {id})
- `/cinemas` (GET, GET {id}, POST, PUT {id}, DELETE {id})
- `/proiezioni` (GET, GET {id}, POST, PUT {id}, DELETE {id})

DB schema/migrations
- Migration files: `Migrations/20260316105055_InitialCreate.cs` and snapshot.
- Tables created: `Registi`, `Films` (with CopertinaPath, FilmatoPath), `Cinemas`, `Proiezioni`, `__EFMigrationsHistory`.

What was done
- Updated to use Pomelo.EntityFrameworkCore.MySql 9.0.0
- Added CopertinaPath and FilmatoPath to Film entity
- Added DEFAULT_COVER_IMAGE_PATH in .env
- Updated Film endpoints to handle default cover path
- Created and applied migration

Files of interest
- `Program.cs`, `Data/FilmDbContext.cs`, `Migrations/`, `Endpoints/`, `.env`, `.env.example`.
