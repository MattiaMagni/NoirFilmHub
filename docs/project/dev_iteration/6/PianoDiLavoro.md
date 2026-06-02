# Piano di Lavoro - Iterazione 6

Autore: OpenCode

## Obiettivo

Raggiungere due milestone operative per NoirFilmHub:

1. **Containerizzazione completa gestita via Docker Compose**: ogni componente (database, backend, frontend, seeder) viene definito con Dockerfile multistage e orchestrato da `docker-compose.yml`. Da un clone pulito del repository, il comando `docker-compose up -d` deve avviare l'intera applicazione funzionante con dati seed realistici (3 film famosi da TMDB), account admin preconfigurato, servizi email e autenticazione con provider esterni già impostati.

2. **Deployment su Azure Container Apps (ACA)**: portare l'architettura containerizzata su Azure Container Apps seguendo l'approccio documentato nella guida `educationalgames/aca/index.md`, adattato all'architettura specifica di NoirFilmHub.

## Decisioni Guida della Iterazione 6

- La containerizzazione è il prerequisito per ACA: il docker-compose locale deve funzionare perfettamente prima di toccare Azure.
- Il seeder va estratto dal `Program.cs` del backend in un progetto console separato (`FilmApiSeeder`), eseguibile come container one-shot.
- I 3 film seed devono essere recuperati da TMDB: **Inception** (2010, TMDB 27205), **Interstellar** (2014, TMDB 157336), **The Dark Knight** (2008, TMDB 155).
- Il database MariaDB deve essere fresco a ogni `docker-compose up`.
- I segreti di produzione NON vanno committati. Il file `.env.docker` conterrà placeholder.
- L'architettura ACA: 1 Container App per MariaDB (internal ingress), 1 per FilmAPI (external), 1 per FilmFrontend (external), seeder come Container App Job.

## Stato Avanzamento Fasi

| Fase | Stato |
| --- | --- |
| FASE 0 - Preparazione e analisi | Da fare |
| FASE 1 - Estrazione FilmApiSeeder | Da fare |
| FASE 2 - Dockerfile e docker-compose | Da fare |
| FASE 3 - Verifica containerizzazione locale | Da fare |
| FASE 4 - Push ACR e preparazione Azure | Da fare |
| FASE 5 - Deployment ACA: infrastruttura | Da fare |
| FASE 6 - Deployment ACA: container apps | Da fare |
| FASE 7 - Configurazione dominio, DNS, TLS | Da fare |
| FASE 8 - Configurazione email e OAuth su ACA | Da fare |
| FASE 9 - Verifica e troubleshooting ACA | Da fare |

---

## FASE 0 - Preparazione e analisi

### Scopo

Mappare tutte le dipendenze e configurazioni necessarie prima di scrivere codice.

### Attività

- Catalogare tutte le variabili d'ambiente usate dall'app (da `.env` e `.env.example`)
- Identificare le porte esposte da ogni servizio (API: 5000, Frontend: 5001, MariaDB: 3306)
- Mappare le dipendenze tra servizi (Frontend → API, API → MariaDB, Seeder → MariaDB)
- Identificare i volumi Docker necessari (mariadb_data)
- Verificare quali file/directory escludere dal build context (`.dockerignore`)
- Definire le immagini base: `mcr.microsoft.com/dotnet/sdk:9.0` per build, `mcr.microsoft.com/dotnet/aspnet:9.0` per runtime, `mariadb:10.11` per DB

### Criteri di accettazione

- Documento `FASE0_AnalisiPreparazione.md` creato con mappatura completa
- Tabella variabili d'ambiente con: nome, default Docker, obbligatoria (S/N), sensibile (S/N)
- Schema architetturale dei container con porte e dipendenze

### Variabili d'ambiente da mappare

| Variabile | Default Docker | Sensibile |
|----------|---------------|-----------|
| `DB_HOST` | `mariadb` | No |
| `DB_PORT` | `3306` | No |
| `DB_NAME` | `film-api-db` | No |
| `DB_USER` | `root` | No |
| `DB_PASSWORD` | `root` (dev) | **Sì** |
| `JWT_SECRET_KEY` | `dev-jwt-secret-min-64-chars...` | **Sì** |
| `JWT_ISSUER` | `FilmAPI` | No |
| `JWT_AUDIENCE` | `FilmFrontend` | No |
| `APP_BASE_URL` | `http://localhost:5001` | No |
| `ASPNETCORE_ENVIRONMENT` | `Development` | No |
| `STRIPE_SECRET_KEY` | `sk_test_...` (dev) | **Sì** |
| `STRIPE_WEBHOOK_SECRET` | `whsec_...` (dev) | **Sì** |
| `TMDB_API_READ_TOKEN` | (token TMDB) | **Sì** |
| `SMTP_HOST` | `smtp.gmail.com` | No |
| `SMTP_PORT` | `587` | No |
| `SMTP_USER` | `...@gmail.com` | **Sì** |
| `SMTP_PASSWORD` | (app password) | **Sì** |
| `SMTP_FROM` | `noreply@noirfilmhub.local` | No |
| `SMTP_FROM_NAME` | `Noir Film Hub` | No |
| `GOOGLE_CLIENT_ID` | (OAuth client) | **Sì** |
| `GOOGLE_CLIENT_SECRET` | (OAuth secret) | **Sì** |
| `MICROSOFT_CLIENT_ID` | (OAuth client) | **Sì** |
| `MICROSOFT_CLIENT_SECRET` | (OAuth secret) | **Sì** |
| `DEFAULT_ADMIN_EMAIL` | `admin@noirfilmhub.local` | No |
| `DEFAULT_ADMIN_PASSWORD` | `Admin123!` | **Sì** |
| `AUTH_ENABLED` | `true` | No |
| `FEATURE_SOCIAL_LOGIN` | `true` | No |

---

## FASE 1 - Estrazione FilmApiSeeder

### Scopo

Separare la logica di seeding dal `Program.cs` del backend in un progetto console autonomo (`FilmApiSeeder`), eseguibile come container one-shot.

### Attività

- Creare il progetto `FilmApiSeeder/FilmApiSeeder.csproj` (console app .NET 9) con riferimento a `FilmAPI.csproj`
- Implementare `Program.cs` del seeder:
  - Connessione DB da variabili d'ambiente
  - `db.Database.Migrate()`
  - Creazione account admin
  - Import 3 film da TMDB via `TmdbImportService`: **Inception** (TMDB 27205), **Interstellar** (TMDB 157336), **The Dark Knight** (TMDB 155)
  - Creazione 2 cinema di esempio (Milano, Roma) con sale (2D, 3D, ISENSE)
  - Generazione proiezioni per 14 giorni
  - Seed coupon/gift card template/products (ripreso da `Program.cs`)
- Rendere il seeding in `FilmAPI/Program.cs` condizionale: `RUN_SEEDER=true` per attivarlo
- Il seeder deve essere idempotente

### Criteri di accettazione

- `dotnet run --project FilmApiSeeder` popola il DB con film, cinema, proiezioni, admin
- Backend si avvia senza seeding quando `RUN_SEEDER` non è `true`
- Seeder idempotente

### Test

| ID | Nome |
|----|------|
| SD1 | Admin user creato |
| SD2 | 3 film TMDB importati |
| SD3 | Cinema e sale creati |
| SD4 | Proiezioni generate |
| SD5 | Seeder idempotente |
| SD6 | API non fa auto-seed |

---

## FASE 2 - Dockerfile e docker-compose

### Scopo

Dockerfile multistage per ogni componente e `docker-compose.yml` completo.

### File da creare/modificare

#### `.dockerignore`
Escludere: `bin/`, `obj/`, `.git/`, `tests/`, `docs/`, `*.user`, `*.suo`, `.env`

#### `Dockerfile.api`
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY FilmAPI.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "FilmAPI.dll"]
```

#### `Dockerfile.frontend`
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY FilmFrontend/FilmFrontend.csproj ./FilmFrontend/
RUN dotnet restore FilmFrontend/FilmFrontend.csproj
COPY FilmFrontend/ ./FilmFrontend/
RUN dotnet publish FilmFrontend/FilmFrontend.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:5001
EXPOSE 5001
ENTRYPOINT ["dotnet", "FilmFrontend.dll"]
```

#### `Dockerfile.seeder`
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY FilmApiSeeder/FilmApiSeeder.csproj ./FilmApiSeeder/
COPY FilmAPI/FilmAPI.csproj ./FilmAPI/
RUN dotnet restore FilmApiSeeder/FilmApiSeeder.csproj
COPY . .
RUN dotnet publish FilmApiSeeder/FilmApiSeeder.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "FilmApiSeeder.dll"]
```

#### `docker-compose.yml` (riscritto)
Servizi: `mariadb` (10.11, volume, healthcheck), `filmapi` (build, env, depends_on healthy), `filmfrontend` (build, depends_on filmapi), `filmapiseeder` (build, depends_on healthy, restart: no, profile: seed). Network: `noirnet` (bridge). Volume: `mariadb_data`.

#### `.env.docker`
Tutte le variabili con placeholder `change-me` per i segreti.

### Criteri di accettazione

- `docker-compose build` OK per tutti i servizi
- `docker-compose --profile seed up -d` avvia tutto
- `curl http://localhost:5000/` → `"FilmAPI running"`
- `curl http://localhost:5001/` → HTML homepage
- DB contiene i 3 film TMDB e admin
- Dati persistenti tra down/up

### Test

| ID | Nome |
|----|------|
| DC1 | Tutti i servizi Up |
| DC2 | MariaDB healthy entro 30s |
| DC3 | API risponde |
| DC4 | Frontend risponde |
| DC5 | Seeder ha popolato dati |
| DC6 | API interroga DB |
| DC7 | Dati persistono |
| DC8 | Seeder idempotente |

---

## FASE 3 - Verifica containerizzazione locale

### Scopo

Test approfonditi sull'ambiente Docker Compose: tutti i flussi core funzionano.

### Attività

- Test fumo su tutte le pagine
- Test flusso auth: registrazione, login, refresh, logout
- Test flusso acquisto biglietto: programmazione → scheda → posto → Stripe test mode
- Test flusso shop: gift card / merchandise → checkout
- Test area personale: biglietti, gift card, cronologia ordini
- Test validazione biglietti e ritiri con scanner QR
- Test invio email (se SMTP configurato)
- Log puliti dopo 5 minuti di uptime

### Criteri di accettazione

- 15+ smoke test passano
- Flussi core end-to-end funzionanti
- Nessun errore nei log

---

## FASE 4 - Push ACR e preparazione Azure

### Scopo

Creare ACR, buildare e pushare le immagini.

### Attività

```bash
az group create --name noirfilmhub-rg --location italynorth
az acr create --resource-group noirfilmhub-rg --name noirfilmhubacr --sku Basic --admin-enabled true
az acr login --name noirfilmhubacr

docker build -f Dockerfile.api -t noirfilmhubacr.azurecr.io/filmapi:latest .
docker build -f Dockerfile.frontend -t noirfilmhubacr.azurecr.io/filmfrontend:latest .
docker build -f Dockerfile.seeder -t noirfilmhubacr.azurecr.io/filmapiseeder:latest .

docker push noirfilmhubacr.azurecr.io/filmapi:latest
docker push noirfilmhubacr.azurecr.io/filmfrontend:latest
docker push noirfilmhubacr.azurecr.io/filmapiseeder:latest
```

### Criteri di accettazione

- 3 immagini in ACR con tag `latest`

---

## FASE 5 - Deployment ACA: infrastruttura

### Attività

- Log Analytics Workspace: `noirfilmhub-logs`
- ACA Environment: `noirfilmhub-env`
- Storage Account: `noirfilmhubstor` + Azure Files share `mariadb-data` (quota 5GB)
- Segreti ACA: `mariadb-root-password`, `jwt-secret-key`, `stripe-secret-key`, `stripe-webhook-secret`, `tmdb-api-read-token`, `smtp-password`, `google-client-id/secret`, `microsoft-client-id/secret`, `admin-password`

---

## FASE 6 - Deployment ACA: container apps

### Attività

- **MariaDB** (`mariadb-server`): image `mariadb:10.11`, internal ingress, volume Azure Files, 0-1 repliche
- **FilmAPI** (`filmapi`): image da ACR, external ingress, port 5000, 0-3 repliche, tutte le env vars + secretrefs
- **FilmFrontend** (`filmfrontend`): image da ACR, external ingress, port 5001, 0-3 repliche
- **Seeder Job** (`filmapiseeder-job`): Container App Job, trigger Manual, esecuzione one-shot dopo deploy
- **CORS**: estendere policy backend per accettare dominio frontend ACA
- **API_BASE_URL**: iniettare nel frontend l'URL dell'API ACA

### Criteri di accettazione

- 3 container app + 1 job visibili
- Homepage frontend accessibile via HTTPS
- API risponde con dati seed
- Login admin funzionante

### Test

| ID | Nome |
|----|------|
| AC1 | MariaDB healthy |
| AC2 | API risponde |
| AC3 | Frontend serve homepage |
| AC4 | Endpoint films ha dati seed |
| AC5 | Admin login OK |
| AC6 | Frontend chiama API correttamente |
| AC7 | Dati sopravvivono a restart |

---

## FASE 7 - Configurazione dominio, DNS e TLS

- Dominio personalizzato su entrambe le Container App (frontend + API)
- Record DNS: CNAME → FQDN ACA, TXT → validazione
- Certificato TLS gestito da ACA (rinnovo automatico)
- Aggiornare `APP_BASE_URL` e redirect URI OAuth

---

## FASE 8 - Configurazione email e OAuth su ACA

- **SMTP**: App Password Google → segreto ACA → test invio
- **Google OAuth**: redirect URI `https://<dominio>/signin-google`
- **Microsoft Entra ID**: redirect URI `https://<dominio>/signin-microsoft`

---

## FASE 9 - Verifica e troubleshooting ACA

- Log: `az containerapp logs show --name filmapi --follow`
- Test end-to-end completo
- Script `deploy-aca.sh` riproducibile
- Documento `DEPLOYMENT.md` con tutti i passi

---

## Riepilogo file

### Da creare (11)

| # | File |
|---|------|
| 1 | `FilmApiSeeder/FilmApiSeeder.csproj` |
| 2 | `FilmApiSeeder/Program.cs` |
| 3 | `Dockerfile.api` |
| 4 | `Dockerfile.frontend` |
| 5 | `Dockerfile.seeder` |
| 6 | `.dockerignore` |
| 7 | `.env.docker` |
| 8 | `docker-compose.yml` (riscritto) |
| 9 | `deploy-aca.sh` |
| 10 | `docs/project/dev_iteration/6/FASE0_AnalisiPreparazione.md` |
| 11 | `docs/project/dev_iteration/6/DEPLOYMENT.md` |

### Da modificare (3)

| # | File | Modifica |
|---|------|----------|
| 12 | `FilmAPI/Program.cs` | Seeding condizionale (`RUN_SEEDER`) |
| 13 | `.env.example` | Aggiungere variabili Docker |
| 14 | `FilmFrontend/wwwroot/js/api-config.js` | Supportare override `API_BASE_URL` per ACA |

## Architettura Docker Compose

```
localhost:5001         localhost:5000         localhost:3306
     │                      │                      │
 ┌───┴──────┐         ┌────┴────┐          ┌─────┴─────┐
 │frontend  │ ──────> │ filmapi │ ──────>  │  mariadb  │
 │(.NET 9)  │         │(.NET 9) │          │  (10.11)  │
 └──────────┘         └─────────┘          └─────┬─────┘
                                                  │
                                            ┌─────┴──────┐
                                            │  seeder    │
                                            │ (one-shot) │
                                            └────────────┘
Network: noirnet (bridge)    Volume: mariadb_data
```

## Architettura Azure Container Apps

```
Internet
   │
   ├── https://<frontend-domain> ──> filmfrontend (ext, 0-3 reps, :5001)
   │                                       │
   └── https://<api-domain> ──────> filmapi (ext, 0-3 reps, :5000)
                                           │
                                     mariadb-server (internal, 0-1 reps, :3306)
                                           │
                                     Azure Files (mariadb-data)

ACA Environment: noirfilmhub-env
ACR: noirfilmhubacr.azurecr.io
Storage: noirfilmhubstor
Region: italynorth
```

## Note tecniche

- **Connection string**: costruita in `Program.cs` da variabili flat (`DB_HOST=mariadb` in Docker, `DB_HOST=mariadb-server` in ACA)
- **CORS**: estendere per accettare dominio frontend ACA; aggiungere `CORS_ALLOWED_ORIGINS` env var
- **Stripe webhook**: in produzione punta a `https://filmapi.<domain>/pagamenti/stripe/webhook`
- **`API_BASE_URL` frontend**: `api-config.js` supporta `window.__API_BASE_URL__`; iniettare valore via script inline nel `index.html` all'avvio container
- **Dimensioni immagini**: ~200MB ciascuna (filmapi, frontend, seeder), ~400MB mariadb
- **Ottimizzazioni**: `.dockerignore`, caching layer (csproj → restore → sorgenti → publish), valutare `noble-chiseled` per runtime più piccolo
