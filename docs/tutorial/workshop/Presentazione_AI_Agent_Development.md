# Programmare con l'AI Agent — Il caso NoirFilmHub

> Presentazione 10-20 minuti | Autore: OpenCode + Mattia Magni

---

## Diapositiva 1 — Titolo

**"Programmare con l'AI Agent: il caso NoirFilmHub"**

Sottotitolo: *Come un assistente AI ha guidato lo sviluppo di una piattaforma cinema completa — dal modello dati al deploy su Azure*

---

## Diapositiva 2 — Cos'è NoirFilmHub

Piattaforma completa per multisala cinematografica:

- **Programmazione film** pubblica con filtri, ricerca, calendario show
- **Prenotazione posti** con mappa interattiva e seat lock anti-race-condition
- **Pagamenti Stripe** (singolo e carrello) + credito piattaforma
- **Biglietti PDF** con QR code e barcode Code128
- **Validazione biglietti** al tornello con vincolo cinema operatore
- **Shop e-commerce**: gift card, merchandising (felpe, t-shirt), cibo (popcorn)
- **Ritiro articoli** con QR scanner integrato (fotocamera)
- **Login sociale** Google e Microsoft OIDC multi-tenant
- **Admin dashboard** con CRUD film/registi/cinema/sale/proiezioni
- **Email transazionali** per biglietti, gift card, conferme ordine
- **Containerizzazione** Docker + docker-compose per sviluppo locale
- **Deploy produzione** su Azure Container Apps con dominio `noirfilmhub.it`

**Stack tecnologico**: .NET 9 Minimal API + Vanilla JS + EF Core + MariaDB + Stripe + TMDB + Docker + Azure

---

## Diapositiva 3 — Il progetto in numeri

| Metrica | Valore |
|---------|--------|
| File `.cs` backend | **113** |
| File frontend | **70** (38 JS + 31 HTML + 1 CSS) |
| Commit totali | **60** |
| Iterazioni di sviluppo documentate | **6** |
| Migrazioni database EF Core | **15+** |
| Endpoint API | **80+** |
| Container Docker | **3** (API, frontend, seeder) |
| Tempo totale stimato | ~3-4 mesi (part-time, 1 sviluppatore) |

---

## Diapositiva 4 — Le 6 iterazioni di sviluppo

| Iterazione | Cosa è stato costruito |
|-----------|------------------------|
| **Iterazione 1** | Setup progetto .NET 9 Minimal API, modello dati base (Regista, Film, Cinema, Proiezione), CRUD endpoint REST, Swagger, migrazioni EF Core + MariaDB |
| **Iterazione 2** | Frontend (server file statici), pagine CRUD admin, componenti navbar/footer, API client JS, layout responsive |
| **Iterazione 3** | Autenticazione JWT con access/refresh token, RBAC (Admin, PowerUser, Utente), categorie film (many-to-many), sistema prenotazioni, frontend login/register/profile |
| **Iterazione 4** | Programmazione pubblica con filtri e ricerca, scheda film con calendario, acquisto posti con seat lock, pagamento Stripe Hosted Checkout, validazione biglietti, integrazione TMDB |
| **Iterazione 5** | Social login Google e Microsoft OIDC, password management, JWT hardening, audit di sicurezza, servizio email con template HTML, frontend admin avanzato |
| **Iterazione 5.x** | Shop e-commerce (gift card, merchandising, cibo), pricing per categoria posto (Platea/Galleria/VIP/Access), carrello misto, validazione ritiri con scanner QR, hardening sicurezza (CSP, XSS) |
| **Iterazione 6** | Containerizzazione Docker (3 Dockerfile multistage), FilmApiSeeder (import 3 film TMDB), docker-compose completo, deploy Azure Container Apps, dominio `noirfilmhub.it` + TLS |

---

## Diapositiva 5 — Come lavora un AI Agent

Il flusso di lavoro tipico con l'AI Agent:

```
1. UTENTE      "Voglio aggiungere l'email di conferma per il ritiro
                merchandising con QR code"
                  ↓
2. AGENTE       ESPLORA il codice (legge 15+ file in parallelo)
                  ↓
3. AGENTE       CAPISCE l'architettura (modelli, pattern, convenzioni)
                  ↓
4. AGENTE       PROPONE un piano dettagliato con file da creare/modificare
                  ↓
5. UTENTE       APPROVA o chiede modifiche al piano
                  ↓
6. AGENTE       IMPLEMENTA (scrive codice, build, fixa errori, test)
                  ↓
7. UTENTE       TESTA l'applicazione live
                  ↓
8. RIPETI       per ogni nuova feature o bug fix
```

**Tempo tipico per ciclo**: da 5 minuti (fix semplice) a 1 ora (nuova feature con 10+ file)

---

## Diapositiva 6 — Esempio concreto: Email ritiro con QR code

**Richiesta dell'utente**:
> "Aggiungere una mail per il ritiro del merchandising e del cibo che comprende un codice e un QR code, stile simile a quello del biglietto"

**Cosa ha fatto l'agente**:

1. **Esplorazione** — Letto 16 file in parallelo: modelli, endpoint, email service, PDF generator, frontend scanner
2. **Pianificazione** — Prodotto un piano con 10 file da toccare:
   - Nuovo modello `RitiroOrdine`
   - Nuovo endpoint API `RitiriEndpoints`
   - Estensione `EmailService` con supporto immagini inline
   - Trigger in `PagamentiEndpoints.FinalizeCartOrderAsync`
   - Nuova pagina `validazione-ritiri.html` + `validazione-ritiri.js`
   - Scanner QR integrato (libreria `html5-qrcode`)
   - Voce menu in navbar
3. **Implementazione** — Tutti i file creati/modificati, migrazione EF generata
4. **Verifica** — Build .NET passata, endpoint testati, container Docker funzionanti

**Senza agente**: ricercare manualmente ogni file, capire le dipendenze incrociate, scrivere tutto da zero. Stima: 2-3 giorni.

---

## Diapositiva 7 — Confronto: con AI Agent vs senza

| Attività | Con AI Agent | Senza AI Agent |
|----------|-------------|----------------|
| **Capire codebase sconosciuta** | Minuti (esplorazione automatica di decine di file) | Ore/giorni (lettura manuale, appunti) |
| **Nuovo endpoint API** | 1 comando: agente deduce pattern da endpoint esistenti | Scrivere DTO, endpoint, service, test manualmente |
| **Fix bug runtime** | Agente legge log, individua root cause, propone fix in secondi | Stack Overflow, debugger manuale, trial-error |
| **Deploy produzione** | Agente esegue comandi Azure CLI passo-passo, gestisce errori | Studiare documentazione Azure, infiniti trial-error |
| **Refactoring cross-file** | Agente trova tutte le occorrenze in secondi (grep semantico) | grep testuale, rischio di dimenticare occorrenze |
| **Comprensione contesto** | Agente legge file correlati in parallelo e li mette in relazione | Aprire file uno per uno nell'IDE, tenere a mente |
| **Scrittura documentazione** | Agente genera piani di lavoro dettagliati dalle conversazioni | Scrivere tutto manualmente a posteriori |
| **DevOps (Docker, Azure)** | Agente crea Dockerfile, compose, script deploy, fixa errori build | Ore di studio su Docker Hub, documentazione Azure |

---

## Diapositiva 8 — Bug reali trovati e risolti dall'agente

### Bug 1: `redirect_uri_mismatch` Google OAuth (Iterazione 6)

**Sintomo**: Login Google dava `Errore 400: redirect_uri_mismatch` anche con URI corretti registrati.

**Root cause** (scoperta dall'agente): In Azure Container Apps, TLS viene terminato all'edge e il container riceve la richiesta via HTTP. `Request.Scheme` restituiva `http://` invece di `https://`, causando mismatch con Google.

**Fix**: Sostituire `$"{httpContext.Request.Scheme}://{httpContext.Request.Host}"` con `Environment.GetEnvironmentVariable("APP_BASE_URL")`.

**File modificato**: `AuthEndpoints.cs` (2 righe)

---

### Bug 2: 404 intermittente su `/pagamenti/esito` (Iterazione 5.x)

**Sintomo**: Dopo il pagamento Stripe, a volte l'endpoint esito restituiva `404 Ordine non trovato`.

**Root cause** (scoperta dall'agente): In `CartService.cs`, una finestra di 30 secondi azzerava `StripeSessionId` se il pagamento su Stripe impiegava troppo tempo.

**Fix**: Finestra estesa da 30 secondi a 15 minuti + aggiunto fallback lookup senza `UtenteId` per gestire disallineamenti di sessione.

**File modificati**: `CartService.cs`, `PagamentiEndpoints.cs`

---

### Bug 3: QR code puntava al dominio API invece del frontend (Iterazione 6)

**Sintomo**: QR code nei biglietti PDF e nelle email di ritiro puntavano a `filmapi.noirfilmhub.it` (dominio API) invece che `www.noirfilmhub.it` (frontend).

**Root cause** (scoperta dall'agente): 6 occorrenze in `PagamentiEndpoints.cs` usavano `APP_BASE_URL` (che punta all'API) per costruire URL che dovrebbero puntare al frontend (pagine HTML, validazione).

**Fix**: Introdotto `FRONTEND_BASE_URL` con fallback a `APP_BASE_URL`. Modificate tutte e 6 le occorrenze.

**File modificato**: `PagamentiEndpoints.cs`

---

### Bug 4: Docker build crashava (Iterazione 6)

**Sintomo**: `dotnet publish` falliva con `Requested SDK version: 9.0.306` non trovata, ma l'immagine Docker aveva SDK 9.0.314.

**Root cause** (scoperta dall'agente): Il file `global.json` nella root del progetto pinnava la versione SDK. Veniva copiato nel container e causava mismatch.

**Fix**: Aggiunto `global.json` a `.dockerignore` + `dotnet publish` con `--no-restore`.

**File modificati**: `.dockerignore`, tutti e 3 i `Dockerfile.*`

---

## Diapositiva 9 — Pregi dell'AI Agent

| Pregio | Descrizione |
|--------|-------------|
| **Velocità esplorativa** | Legge 20+ file in parallelo in pochi secondi, costruendo una mappa mentale del progetto |
| **Memoria perfetta** | Non dimentica mai un file, una dipendenza o una convenzione già vista |
| **Pattern recognition** | Identifica pattern architetturali (es. struttura endpoint) e li replica fedelmente |
| **Full-stack naturale** | Passa da backend C# a frontend JS a CSS a Docker senza attriti di contesto |
| **DevOps integrato** | Dockerfile, docker-compose, Azure CLI, configurazione DNS — tutto nello stesso flusso |
| **Documentazione automatica** | Genera piani di lavoro dettagliati, analisi, guide passo-passo |
| **Debugging assistito** | Legge log di errore, traccia lo stack, identifica la root cause, propone il fix |
| **Disponibilità 24/7** | Non si stanca, non ha orari, risponde sempre |
| **Multi-linguaggio** | Stesso agente lavora su C#, JavaScript, CSS, SQL, YAML, Docker, Azure CLI |

---

## Diapositiva 10 — Difetti e limiti

| Difetto | Descrizione |
|---------|-------------|
| **Dipendenza dal contesto** | Se il codice è disorganizzato o incoerente, l'agente fatica a orientarsi |
| **Allucinazioni** | A volte inventa metodi, classi o API che non esistono (specie in librerie poco conosciute) |
| **Overconfidence** | Può proporre soluzioni iper-complesse quando ne basterebbe una banale |
| **Costo computazionale** | Ogni sessione consuma migliaia di token (costo API + impatto ambientale) |
| **Sensibilità al prompt** | Una richiesta ambigua produce risultati scadenti; richiede precisione |
| **Non sostituisce l'umano** | Decisioni architetturali, sicurezza, UX e priorità restano responsabilità dello sviluppatore |
| **Qualità del codice** | Il codice generato può essere funzionale ma non idiomatico o non ottimizzato |
| **Mancanza di giudizio** | Non sa dire "questa feature non andrebbe fatta" — esegue ciò che gli viene chiesto |
| **Limiti di contesto** | Sessioni molto lunghe possono saturare la finestra di contesto, perdendo dettagli iniziali |

---

## Diapositiva 11 — Cosa NON può fare (oggi)

- **Decisioni architetturali autonome**: l'agente propone, ma non può decidere se usare MySQL vs PostgreSQL, REST vs GraphQL, ecc.
- **Testare visivamente l'UI**: non vede lo schermo, non può valutare se un pulsante è allineato o un colore è gradevole
- **Gestire segreti e credenziali**: può usarli se forniti, ma non dovrebbe mai generarli o memorizzarli
- **Sostituire code review umana**: il codice generato va sempre rivisto
- **Comprendere il business domain**: sa cos'è un "biglietto" ma non le regole di business specifiche (es. rimborso 50%, scadenza gift card)

---

## Diapositiva 12 — Best practice per usare AI Agent

1. **Dai contesto preciso**: "Segui lo stesso pattern di `PrenotazioniEndpoints.cs`" funziona meglio di "crea un endpoint"
2. **Fai pianificare prima di eseguire**: "Fammi un piano dettagliato, poi eseguo" — evita refactoring costosi
3. **Piccole iterazioni**: Un task alla volta, verifica dopo ogni step (build + test)
4. **Verifica sempre**: Build .NET, test endpoint, curl — non fidarti mai ciecamente
5. **Mantieni il controllo**: L'agente propone, tu decidi. Sei tu il responsabile del codice
6. **Documenta con l'agente**: Fai generare il piano di lavoro e i commenti dall'agente stesso
7. **Correggi prompt, non codice**: Se l'agente sbaglia, non correggere a mano — spiega meglio cosa vuoi e fallo rigenerare
8. **Tieni traccia delle decisioni**: I piani di lavoro (`PianoDiLavoro.md`) documentano perché è stata presa ogni decisione
9. **Usa i test come guardrail**: Se esistono test automatici, l'agente li rispetta e li estende

---

## Diapositiva 13 — Demo consigliata (se c'è tempo)

Scegli UNA di queste demo (5 minuti ciascuna):

**Opzione A — Live coding**:
1. Aprire OpenCode / Claude Code / simile
2. Chiedere: "Aggiungi un campo `Note` opzionale al modello `Regista` e mostralo nel frontend"
3. Mostrare come l'agente esplora, pianifica, modifica i file
4. Build, refresh browser, verificare il risultato

**Opzione B — Code walkthrough**:
1. Aprire il commit `f3787fe` (fix `redirect_uri_mismatch`)
2. Mostrare il diff: 4 righe modificate in 1 file
3. Spiegare il bug, la root cause, e come l'agente l'ha trovato
4. Enfatizzare: stesso bug avrebbe richiesto ore di debug manuale

---

## Diapositiva 14 — Il futuro

- **Agenti specializzati**: backend agent, frontend agent, DevOps agent, security agent che collaborano
- **Agenti proattivi**: monitorano il repo, aprono issue, propongono refactoring
- **Test generation automatica**: l'agente scrive unit test, integration test, E2E test mentre scrivi il codice
- **Code review automatica**: l'agente rivede le PR e suggerisce miglioramenti prima del merge
- **Self-healing**: l'agente monitora i log di produzione e propone fix per gli errori

---

## Diapositiva 15 — Conclusione

> **"L'AI Agent non sostituisce lo sviluppatore. È un acceleratore che elimina il lavoro meccanico — cercare file, ricordare sintassi, scrivere boilerplate, debuggare — e lascia all'umano le decisioni che contano: architettura, UX, sicurezza, priorità."**

**Risultato concreto**: NoirFilmHub è passato da zero a produzione su Azure in 6 iterazioni, con un unico sviluppatore e un AI Agent come assistente.

**Cosa portarsi a casa**:
- L'AI Agent è un **moltiplicatore di produttività**, non un sostituto
- Funziona meglio con **progetti ben strutturati** e **richieste precise**
- Il **pensiero critico** dello sviluppatore resta insostituibile
- **Imparare a promptare** è una skill fondamentale quanto programmare

---

## Fonti

- Repository: `https://github.com/MattiaMagni/NoirFilmHub`
- Piani di lavoro: `docs/project/dev_iteration/`
- Documentazione tecnica: `docs/tutorial/`
- Tool utilizzato: OpenCode (basato su Claude)

---

## Note per l'esposizione orale

- **Tempo totale**: 15-20 minuti con demo, 10 minuti senza
- **Stile consigliato**: mostrare esempi concreti dal repository (aprire commit, diff, log)
- **Coinvolgere il pubblico**: "Quanto ci avreste messo voi a fare questa modifica?"
- **Demo migliore**: il fix del `redirect_uri_mismatch` — è un bug reale, complesso (ACA + OAuth + HTTP/HTTPS), risolto in 3 messaggi dall'agente
- **Slide opzionale**: screenshot della timeline commit su GitHub per mostrare la densità di sviluppo
