# FASE 0 - Analisi Preparazione Containerizzazione

Autore: OpenCode

## Mappatura dipendenze tra servizi

```
filmfrontend ──(HTTP)──> filmapi ──(MySQL)──> mariadb
                              ^
filmapiseeder ──(MySQL)──────┘
```

| Servizio | Porta | Dipende da | Tipo dipendenza |
|----------|-------|------------|-----------------|
| `mariadb` | 3306 | - | - |
| `filmapi` | 5000 | mariadb | DB connection (MySQL) |
| `filmfrontend` | 5001 | filmapi | HTTP API calls |
| `filmapiseeder` | - | mariadb | DB connection (MySQL) |

## Variabili d'ambiente

| Variabile | Default Docker | Obbligatoria | Sensibile |
|----------|---------------|-------------|-----------|
| `DB_HOST` | `mariadb` | Sì | No |
| `DB_PORT` | `3306` | No | No |
| `DB_NAME` | `film-api-db` | Sì | No |
| `DB_USER` | `root` | No | No |
| `DB_PASSWORD` | `root` | Sì | **Sì** |
| `DB_PROVIDER` | `MySql` | No | No |
| `DB_SERVER_VERSION` | `10.11.0-mariadb` | No | No |
| `DB_USE_AUTODETECT` | `false` | No | No |
| `JWT_SECRET_KEY` | `change-me-64-chars-min...` | Sì | **Sì** |
| `JWT_ISSUER` | `FilmAPI` | No | No |
| `JWT_AUDIENCE` | `FilmFrontend` | No | No |
| `JWT_ACCESS_TOKEN_EXPIRY_MINUTES` | `15` | No | No |
| `JWT_REFRESH_TOKEN_EXPIRY_DAYS` | `7` | No | No |
| `APP_BASE_URL` | `http://localhost:5001` | Sì | No |
| `ASPNETCORE_ENVIRONMENT` | `Development` | No | No |
| `ASPNETCORE_URLS` | non usato (hardcoded in Program.cs) | No | No |
| `STRIPE_SECRET_KEY` | `sk_test_...` | Sì | **Sì** |
| `STRIPE_WEBHOOK_SECRET` | `whsec_...` | No | **Sì** |
| `TMDB_API_READ_TOKEN` | (da .env) | Sì | **Sì** |
| `TMDB_BASE_URL` | `https://api.themoviedb.org/3` | No | No |
| `TMDB_LANGUAGE` | `it-IT` | No | No |
| `TMDB_REGION` | `IT` | No | No |
| `SMTP_HOST` | `smtp.gmail.com` | Sì | No |
| `SMTP_PORT` | `587` | No | No |
| `SMTP_USER` | (email Gmail) | Sì | **Sì** |
| `SMTP_PASSWORD` | (app password) | Sì | **Sì** |
| `SMTP_FROM` | `noreply@noirfilmhub.local` | Sì | No |
| `SMTP_FROM_NAME` | `Noir Film Hub` | No | No |
| `GOOGLE_CLIENT_ID` | (OAuth) | No | **Sì** |
| `GOOGLE_CLIENT_SECRET` | (OAuth) | No | **Sì** |
| `MICROSOFT_CLIENT_ID` | (OAuth) | No | **Sì** |
| `MICROSOFT_CLIENT_SECRET` | (OAuth) | No | **Sì** |
| `DEFAULT_ADMIN_EMAIL` | `admin@noirfilmhub.local` | Sì | No |
| `DEFAULT_ADMIN_PASSWORD` | `Admin123!` | Sì | **Sì** |
| `AUTH_ENABLED` | `true` | No | No |
| `RESET_USERS` | `false` | No | No |
| `RESEED_PROIEZIONI` | `false` | No | No |
| `RUN_SEEDER` | `false` (API) / `true` (seeder container) | No | No |
| `FEATURE_SOCIAL_LOGIN` | `true` | No | No |
| `FEATURE_SOCIAL_GOOGLE_ENABLED` | `true` | No | No |
| `FEATURE_SOCIAL_MICROSOFT_ENABLED` | `true` | No | No |

## Immagini base

| Componente | Build | Runtime |
|-----------|-------|---------|
| FilmAPI | `mcr.microsoft.com/dotnet/sdk:9.0` | `mcr.microsoft.com/dotnet/aspnet:9.0` |
| FilmFrontend | `mcr.microsoft.com/dotnet/sdk:9.0` | `mcr.microsoft.com/dotnet/aspnet:9.0` |
| FilmApiSeeder | `mcr.microsoft.com/dotnet/sdk:9.0` | `mcr.microsoft.com/dotnet/aspnet:9.0` |
| MariaDB | - | `mariadb:10.11` |

## Schema architettura container

```
                    ┌──────────────────────────┐
                    │    noirnet (bridge)       │
                    │                           │
  Porta 5001 ──────>│  filmfrontend             │
                    │       │                   │
                    │       │ HTTP              │
                    │       v                   │
  Porta 5000 ──────>│  filmapi                  │
                    │       │                   │
                    │       │ MySQL (3306)      │
                    │       v                   │
  Porta 3306 ──────>│  mariadb                 │
                    │       ^                   │
                    │       │ MySQL (3306)      │
                    │  filmapiseeder (one-shot) │
                    └──────────────────────────┘
                    Volume: mariadb_data:/var/lib/mysql
```

## Decisioni tecniche

1. **Runtime API usa `aspnet:9.0`**, non `runtime:9.0`, perché FilmAPI è un progetto ASP.NET Core (usa `Microsoft.NET.Sdk.Web`)
2. **Frontend è anch'esso ASP.NET Core** (`FilmFrontend.csproj`), quindi stesso runtime
3. **Connection string costruita dinamicamente** in `Program.cs` da variabili d'ambiente flat, non da connection string preformata
4. **Il seeder applica migrazioni EF** (`db.Database.Migrate()`), poi popola i dati
5. **L'API NON applica migrazioni** all'avvio quando `RUN_SEEDER=false` (le migrazioni sono già state applicate dal seeder)
6. **MariaDB healthcheck**: `mysqladmin ping -h localhost --silent`
7. **Depends_on con condition: service_healthy** garantisce che il DB sia pronto prima di avviare API e seeder
8. **Volumi**: solo `mariadb_data` per persistenza DB. Nessun volume per upload (immagini film sono URL TMDB)
