# Descrizione Progetto - Guida rapida per modifiche UI

Questo file raccoglie le informazioni essenziali per lavorare sull'interfaccia grafica di **Noir Film Hub** senza rompere routing, ruoli o integrazione con le API.

## 1) Architettura del progetto

- **Backend**: `FilmAPI` (ASP.NET Core Minimal API, EF Core, MariaDB)
- **Frontend**: `FilmFrontend` (ASP.NET Core usato come static server)
- **Entry frontend**: `FilmFrontend/Program.cs`
- **Root statico**: `FilmFrontend/wwwroot`

Obiettivo per modifiche grafiche: intervenire principalmente in HTML/CSS/JS del frontend, senza toccare backend/API.

## 2) File chiave UI

### Layout condiviso
- `FilmFrontend/wwwroot/components/navbar.html`
- `FilmFrontend/wwwroot/components/footer.html`
- `FilmFrontend/wwwroot/js/template-loader.js` (carica navbar/footer)
- `FilmFrontend/wwwroot/js/navbar.js` (logica menu, active link, ruoli, logout, comportamento trasparente su home)

### Design system e tema
- `FilmFrontend/wwwroot/css/styles.css` (token colori, tipografia, componenti, responsive)
- `FilmFrontend/wwwroot/js/theme.js` (toggle dark/light globale con `data-theme` su `<html>`)

### Pagine principali
- `FilmFrontend/wwwroot/index.html` (home cinematica + carousel + modal film)
- `FilmFrontend/wwwroot/dashboard.html`
- `FilmFrontend/wwwroot/films.html`
- `FilmFrontend/wwwroot/registi.html`
- `FilmFrontend/wwwroot/cinemas.html`
- `FilmFrontend/wwwroot/proiezioni.html`
- `FilmFrontend/wwwroot/proiezioni-pubblico.html`
- `FilmFrontend/wwwroot/login.html`
- `FilmFrontend/wwwroot/register.html`
- `FilmFrontend/wwwroot/profile.html`
- `FilmFrontend/wwwroot/categorie.html`
- `FilmFrontend/wwwroot/utenti.html`

### Script pagina
- `FilmFrontend/wwwroot/js/home.js`
- `FilmFrontend/wwwroot/js/dashboard.js`
- `FilmFrontend/wwwroot/js/films.js`
- `FilmFrontend/wwwroot/js/registi.js`
- `FilmFrontend/wwwroot/js/cinemas.js`
- `FilmFrontend/wwwroot/js/proiezioni.js`
- `FilmFrontend/wwwroot/js/proiezioni-pubblico.js`
- `FilmFrontend/wwwroot/js/login.js`
- `FilmFrontend/wwwroot/js/register.js`
- `FilmFrontend/wwwroot/js/profile.js`
- `FilmFrontend/wwwroot/js/utenti.js`

## 3) Design system attuale (linee guida operative)

Nel CSS sono presenti token globali in `:root` e `:root[data-theme="light"]`:

- Primary: rosso cinema (`#A4161A`, hover/active dedicati)
- Background e superfici a livelli (`bg`, `surface`, `surface-hover`)
- Tipografia:
  - UI: **Inter**
  - Heading: **Poppins**
- Tema dark/light gestito globalmente da `theme.js`

Regole consigliate per modifiche:
- Non introdurre colori fuori palette se non necessario.
- Usare sempre variabili CSS (`var(--...)`) invece di colori hardcoded.
- Mantenere contrasto elevato in dark mode.
- Evitare componenti "isolati": aggiornare stile in modo riusabile.

## 4) Navbar e ruoli

La navbar e' role-aware:
- Ruoli backend: `admin`, `power_user`, `utente`
- Costanti backend: `Model/RuoloUtente.cs`

Comportamento:
- link pubblici sempre visibili (home/proiezioni)
- area gestione visibile a `admin,power_user`
- alcune voci solo admin (`cinemas`, `categorie`, `utenti`)
- login/register visibili da anonimo
- area personale/logout visibili da autenticato

Attenzione: se cambi classi/data-attributes della navbar, aggiorna anche `navbar.js`.

## 5) Home (stato corrente)

La home usa un layout cinematografico con:
- hero full-width con overlay
- carousel orizzontale film
- quick access panel
- modal dettaglio film su click card

File coinvolti:
- `index.html` (struttura)
- `home.js` (caricamento film, KPI, apertura modal, recupero nome regista da `/registi`)
- `styles.css` (stile hero/carousel/modal)

Asset hero:
- `FilmFrontend/wwwroot/assets/hero-dune.svg`

## 6) API consumate dal frontend (principali)

- `/auth/*` per login/sessione/utente
- `/films`
- `/registi`
- `/cinemas`
- `/proiezioni`
- `/categorie`
- `/utenti`
- `/prenotazioni`

Configurazione base URL:
- `FilmFrontend/wwwroot/js/api-config.js`

Wrapper HTTP comune:
- `FilmFrontend/wwwroot/js/api-client.js`

## 7) Workflow consigliato per modifiche UI

1. Modifica HTML pagina interessata.
2. Aggiorna stile in `styles.css` con classi riusabili.
3. Aggiorna script pagina solo se serve logica interattiva.
4. Mantieni compatibilita con navbar/footer caricati via template.
5. Testa desktop + mobile.
6. Verifica autenticato/anonimo e ruoli (admin/power_user/utente) su navbar e pagine protette.

## 8) Comandi utili

Backend:

```bash
dotnet run
```

Frontend (dalla cartella `FilmFrontend`):

```bash
dotnet run
```

Build frontend:

```bash
dotnet build "FilmFrontend/FilmFrontend.csproj"
```

Nota: se il build fallisce per file lock su `FilmFrontend.exe`, chiudere il processo frontend gia in esecuzione e rilanciare.

## 9) Vincoli da rispettare quando tocchi la UI

- Non cambiare endpoint backend.
- Non rompere i path pagina (`/index.html`, `/films.html`, ...).
- Non rimuovere `bootstrapLayout()` nelle pagine che usano componenti comuni.
- Non eliminare gli id usati dai JS senza aggiornare i relativi script.
- Mantenere il toggle tema funzionante (`#theme-toggle`).

## 10) Riferimenti documentali

- `README.md` (setup rapido)
- `docs/tutorial/GuidaProgetto.md` (spiegazione estesa)
- `docs/project/dev_iteration/1/PianoLavoro.md`
- `docs/project/dev_iteration/2/PianoLavoro.md`
- `docs/project/dev_iteration/3/PianoDiLavoro.md`
