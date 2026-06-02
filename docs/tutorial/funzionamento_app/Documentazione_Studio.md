# Noir Film Hub (FilmAPI) — Guida di Studio

> Versione semplificata e fluida, pensata per lo studio e la comprensione rapida. Per tutti i dettagli tecnici, consulta `Documentazione_Completa.md`.

---

## Cos'e Noir Film Hub?

Noir Film Hub e una piattaforma web che permette di gestire un cinema dalla A alla Z: dal catalogo dei film alla vendita dei biglietti, dal merchandising ai pagamenti, fino all'amministrazione completa. Immagina di essere il gestore di un cinema multiplex: questa app ti permette di fare tutto cio che serve per far funzionare il cinema, e permette ai clienti di comprare biglietti e prodotti dal telefono o dal computer.

---

## Le Funzionalita Principal — Spiegate Semplici

### 1. Catalogo Film e Programmazione

Il cuore dell'app e il catalogo film. Ogni film ha un titolo, una durata, un regista, una descrizione, un poster (copertina) e un trailer. I film sono organizzati in categorie (Azione, Commedia, Horror, ecc.) e possono essere arricchiti automaticamente con dati da **TMDB** (The Movie Database), un database online di film che fornisce copertine, trame e cast in automatico.

I film vengono proiettati in **proiezioni** (spettacoli): una proiezione combina un film, una sala, una data e un orario. Il frontend mostra la programmazione in diverse viste: film in evidenza, film in uscita, e calendario per data.

**Esempio reale:** Quando aggiungi il film "Dune: Parte Due", il sistema recupera automaticamente la copertina e il trailer da TMDB. Poi crei una proiezione: "Dune: Parte Due, Sala ISENSE, Sabato 17 Maggio alle 21:00, prezzo base 12.90 EUR".

### 2. Cinema Multi-Sala con Mappa Posti

Ogni cinema puo avere piu sale, ognuna con una tipologia (ISENSE, XL, 3D, 2D) e un prezzo diverso. La cosa interessante e che ogni sala ha una **mappa posti** generata automaticamente:

```csharp
// Quando si crea una sala con 11 file e 18 posti, il sistema genera
// posti come A1, A2, ... A8, [corridoio], A11, ... A18, B1, ...
// Il corridoio e gestito automaticamente dal parametro aisleWidth
```

Quando un utente vuole comprare un biglietto, vede una mappa interattiva della sala con i posti colorati: verde (libero), rosso (bloccato da qualcun altro), grigio (gia venduto), blu (selezionato da me). Le ultime file possono essere contrassegnate come **VIP** con un supplemento prezzo.

### 3. Anti Race-Condition: Il Problema dei Posti Condivisi

Che succede se due persone cliccano sullo stesso posto quasi contemporaneamente? L'app usa un sistema di **lock temporanei**:

1. Quando selezioni un posto, il backend crea un `SeatLock` con scadenza di 8-10 minuti
2. Gli altri utenti vedono il posto come "bloccato" e non possono selezionarlo
3. Se non completi l'acquisto entro il tempo limite, il lock scade e il posto torna libero
4. Un servizio in background (`CleanupHostedService`) pulisce regolarmente i lock scaduti

Questo e un problema reale che qualsiasi sito di biglietti deve risolvere: pensa a quando prenoti un volo o un posto al cinema su siti come Ticketmaster.

### 4. Autenticazione e Sicurezza

Il sistema usa i **JWT (JSON Web Token)** per l'autenticazione. Ecco come funziona in parole semplici:

- Quando fai login, il server ti d un **access token** (scade in 15 minuti) e un **refresh token** (scade in 7 giorni)
- Ad ogni richiesta al server, il frontend include l'access token nell'header
- Quando l'access token scade, il frontend usa il refresh token per ottenerne uno nuovo senza rifare login

Ci sono anche misure di sicurezza avanzate:

- **AuthVersion**: se cambi password, il tuo `AuthVersion` nel database viene incrementato. Tutti i token emessi prima del cambio diventano invalidi (perche il claim `auth_version` nel token non corrisponde piu)
- **Lockout progressivo**: dopo troppi tentativi di login falliti, l'account viene temporaneamente bloccato
- **Audit trail**: ogni evento di sicurezza (login, cambio password, disabilitazione account) viene registrato in una tabella di log

Puoi anche fare login con **Google o Microsoft** tramite OIDC (OpenID Connect). Il sistema gestisce il linking: puoi collegare un account Google al tuo account normale, e scollegarlo quando vuoi (ma non se e l'unico modo per accedere).

### 5. Pagamenti con Stripe

I pagamenti avvengono tramite **Stripe Hosted Checkout**: l'utente viene reindirizzato alla pagina sicura di Stripe per inserire i dati della carta, poi Stripe notifica il backend tramite webhook. Ci sono tre modalita:

1. **Solo carta**: paghi tutto con la carta
2. **Solo credito**: se hai credito sulla piattaforma (es. da una gift card o una ricarica), lo usi e non passi da Stripe
3. **Misto**: usi parte del credito e il resto con la carta

Dopo il pagamento, la prenotazione passa da `PendingStripe` a `Confermata`.

### 6. Biglietti PDF con Barcode e QR

Una volta confermato il pagamento, il sistema genera un **PDF biglietto** con:

- Titolo del film, data, ora, sala, numero del posto
- Nome e indirizzo del cinema
- **Barcode Code128** del codice acquisto (es. `NFH-20260517210030-A7X2`)
- **QR code** con un URL per la validazione (es. `https://miosito.com/tickets/validate/NFH-...`)

Il PDF viene inviato via email all'utente e puo anche essere scaricato dall'area personale.

### 7. Validazione al Cinema

Arrivato al cinema, il cliente mostra il biglietto (stampato o sul telefono). L'addetto alla porta usa la pagina `validazione-biglietti.html` per:

- Scannerizzare il barcode o il QR code
- Oppure digitare il codice manualmente
- Il sistema verifica che il biglietto sia valido, non gia usato, e che corrisponda al cinema dove si trov a l'addetto

Se tutto e ok, il biglietto viene marcato come `Validato = true` con registrazione di chi lo ha validato, quando e dove.

### 8. Shop, Carrello e Coupon

L'app include un mini **e-commerce** per vendere merchandise (felpe, t-shirt, tazze, popcorn) e gift card:

- **Prodotti** con varianti (taglie S/M/L/XL per abbigliamento, capacita per contenitori)
- **Carrello** che si autogestisce (scade dopo un periodo, rilascia le riserve di magazzino)
- **Coupon** con regole flessibili: sconto fisso o percentuale, importo minimo, limite utilizzi, target (tutto il carrello, un cinema specifico, unaCategoria), cumulabilita
- **Gift Card** con codice univoco, saldo residuo tracciato, e transazioni di utilizzo

Esempio di coupon dal seed:

```
NFH-BENVENUTO: 10% di sconto su tutto, minimo 15 EUR, una volta per utente
NFH-LISSN20: 20% di sconto sul cinema di Lissone, minimo 20 EUR, una volta per utente
NFH-FLASH5: 5 EUR di sconto fisso, minimo 10 EUR, scade tra 7 giorni
```

---

## Le Tecnologie — Cosa Fa Ciascuna

| Tecnologia | Cosa fa nell'app | Dove si vede nel codice |
|-----------|-----------------|------------------------|
| **ASP.NET Core Minimal API** | Framework web che riceve le richieste HTTP e restituisce risposte JSON | `Program.cs` e `Endpoints/*.cs` |
| **Entity Framework Core** | Traduce le classi C# in tabelle del database e viceversa | `Data/FilmDbContext.cs` e `Model/*.cs` |
| **MariaDB** | Database relazionale dove vengono salvati tutti i dati (utenti, film, prenotazioni, ecc.) | Docker container, migrato automaticamente al via |
| **JWT Bearer** | Sistema di autenticazione: l'utente riceve un "biglietto digitale" firmato che dimostra chi e | `Services/JwtTokenService.cs` |
| **BCrypt** | Algoritmo per nascondere le password nel database (non si salvano mai le password in chiaro) | `Services/PasswordService.cs` |
| **Stripe** | Servizio esterno per accettare pagamenti con carta di credito | `Endpoints/PagamentiEndpoints.cs` |
| **QuestPDF** | Libreria per creare PDF con codice C# (non serve un template HTML) | `Services/TicketPdfService.cs` |
| **ZXing.Net + SkiaSharp** | Genera barcode Code128 e QR code come immagini PNG da mettere nel PDF | `Services/TicketPdfService.cs` |
| **MailKit** | Invia email (biglietti PDF, reset password, inviti) | `Services/EmailService.cs` e `Services/TicketEmailService.cs` |
| **TMDB API** | Servizio esterno che fornisce dati sui film (poster, trama, cast, trailer) | `Services/TmdbService.cs` |
| **Docker** | Fa girare MariaDB in un container isolato, facilmente replicabile | `docker-compose.yml` |
| **HTML/CSS/JS** | Le pagine web che l'utente vede nel browser | `FilmFrontend/wwwroot/*.html` e `FilmFrontend/wwwroot/js/*.js` |

---

## Come Tutto si Collega — Un Esempio Pratico

Seguiamo il percorso completo di un utente che vuole comprare un biglietto:

**1. Apri l'app** → Il browser carica `programmazione.html` che fa una richiesta a `GET /programmazione/films` per ottenere la lista dei film in programmazione.

**2. Scegli un film** → Clicchi su "Dune: Parte Due" e il frontend chiama `GET /programmazione/films/{id}` che restituisce il film con tutti i dettagli, le categorie e il calendario degli show per i prossimi 30 giorni.

**3. Scegli un orario** → Clicchi su "SALA 1 ISENSE - Sabato 21:00" e vai ad `acquista.html` che chiama `GET /checkout/seats/{proiezioneId}`. Il backend restituisce la mappa della sala con i posti venduti, bloccati e liberi.

**4. Selezioni i posti** → Clicchi su A3 e A4. Il frontend chiama `POST /checkout/locks` che crea due `SeatLock` nel database, impedendo ad altri di prenderli per 8-10 minuti.

**5. Vai al pagamento** → Il frontend ti mostra il totale (es. 25.80 EUR per 2 posti ISENSE) e ti fa scegliere: carta, credito o misto. Scegli carta e il backend crea una `Stripe Checkout Session`.

**6. Paghi su Stripe** → Stripe processa il pagamento e notifica il backend tramite webhook. La prenotazione passa a `Confermata`.

**7. Ricevi il biglietto** → Il backend genera un PDF con QuestPDF (barcode + QR), lo allega ad un'email con MailKit, e lo invia. Il PDF contiene il codice `NFH-20260517210030-A7X2`.

**8. Al cinema** → L'addetto scannerizza il QR code, che apre la pagina di validazione. Il backend verifica che il biglietto sia valido, non gia usato, e che corrisponda al cinema giusto. Il biglietto viene marcato come `Validato`.

---

## I Tre Ruoli degli Utenti

| Cosa puoi fare | Visitatore | Utente | PowerUser / Admin |
|---------------|-----------|--------|-------------------|
| Vedere programmazione | Si | Si | Si |
| Comprare biglietti | No | Si | Si |
| Scaricare PDF biglietti | No | Si | Si |
| Gestire film e proiezioni | No | No | Si |
| Validare biglietti al cinema | No | No | Si |
| Gestire utenti e cinema | No | No | Solo Admin |

Le API verificano il ruolo dell'utente per ogni richiesta protetta. Ad esempio, `POST /films` richiede il ruolo `admin` o `power_user`, mentre `GET /films` e accessibile a tutti.

---

## Avvio Rapido

```bash
# 1. Copia il file di configurazione
cp .env.example .env

# 2. Avvia il database
docker compose up -d

# 3. Avvia il backend (sulla porta 5000)
dotnet run

# 4. In un altro terminale, avvia il frontend (sulla porta 5001)
cd FilmFrontend && dotnet run

# 5. Apri il browser
# Frontend: http://localhost:5001
# API docs: http://localhost:5000/swagger
```

Al primo avvio, il sistema crea automaticamente:
- L'utente admin (email: `admin@filmapi.local`, password: `Admin123!`)
- Alcuni registi di esempio (Villeneuve, Nolan, Gerwig)
- Due cinema con 4 sale ciascuno
- Proiezioni per i prossimi 7 giorni
- Prodotti dello shop e coupon di esempio

---

## Schema Semplificato del Database

```
Regista ─── Film ─── FilmCategoria ─── Categoria
                │
            Proiezione ──── Sala ──── Cinema
                │
          Prenotazione ──── Utente
                │
            SeatLock (temporaneo)

Utente ──── Cart ──── CartItem ──── Product/Variant
   │
   ├── GiftCard ──── GiftCardTransaction
   ├── UserExternalLogin (Google/Microsoft)
   └── AccountActionToken (reset password, inviti)

Product ──── ProductVariant (taglie)
Coupon ──── CouponUsage
```

---

## Domande Frequenti per lo Studio

**D: Come fa il sistema a impedire che due persone comprino lo stesso posto?**
R: Usa i `SeatLock`. Quando selezioni un posto, viene creato un record nel database con un timestamp di scadenza. Un vincolo unique su `(ProiezioneId, PostoCodice)` impedisce che due lock vengano creati per lo stesso posto. Se il lock scade, viene eliminato automaticamente.

**D: Cosa succede se cambio password mentre ho una sessione attiva?**
R: Il campo `AuthVersion` dell'utente viene incrementato. Al prossimo controllo (nel middleware `OnTokenValidated`), il claim `auth_version` nel tuo vecchio token non corrispondera piu al valore nel database, e il token verra rifiutato con errore 401.

**D: Come mai il frontend non accede mai direttamente al database?**
R: Per sicurezza. Il database e accessibile solo dal backend, che verifica l'autenticazione e l'autorizzazione di ogni richiesta. Il frontend comunica solo tramite API REST, che sono il punto di controllo centralizzato.

**D: Come funziona la sincronizzazione con TMDB?**
R: Il servizio `TmdbService` chiama le API di TMDB per cercare film per titolo e scaricare metadati (poster, trama, cast, trailer). Un job in background (`TmdbSyncHostedService`) esegue questa sincronizzazione ogni notte. Il token TMDB e salvato solo nel `.env` lato server, mai esposto al frontend.

**D: I coupon possono essere combinati tra loro?**
R: Dipende dal campo `Stackable`. Se un coupon ha `Stackable = true`, puo essere combinato con altri coupon stackable. Se e `false`, non puo essere combinato. Il sistema verifica anche il limite di utilizzi totali e per utente.

---

*Ultimo aggiornamento: maggio 2026 — Iterazioni 1-5.*