# Specifica dei Test - FilmAPI

## 1. Panoramica

Questo documento definisce la specifica completa dei test per l'API FilmAPI. L'obiettivo è validare che tutti gli endpoint CRUD funzionino correttamente e che i vincoli di business siano rispettati.

### 1.1 Ambiente di Test
- **Framework**: xUnit
- **Tipo test**: Test Unitari + Test di Integrazione
- **Target**: ASP.NET Core Web API Minimal API

### 1.2 Tipologie di Test

Questo progetto prevede **due tipologie di test** complementari:

| Tipologia | Descrizione | Focus |
|-----------|-------------|-------|
| **Unit Tests** | Testano la logica di business in isolamento | Service classes, validazioni |
| **Integration Tests** | Testano gli endpoint HTTP end-to-end | API REST, serializzazione JSON |

```
┌─────────────────────────────────────────────────────────────────┐
│                    PYRAMIDE DEI TEST                             │
│                                                                  │
│                    ▲                                             │
│                   ╱ ╲          E2E Tests (~1-5)                 │
│                  ╱   ╲                                           │
│                 ╱     ╲     Integration Tests (~20-40)          │
│                ╱       ╲                                         │
│               ╱─────────╲    Unit Tests (~50-100)               │
│              ╱           ╲                                        │
│             ╱             ╲                                      │
│            ▼               ▼                                     │
│     ┌──────────┐    ┌──────────────┐                           │
│     │  Unit    │    │ Integration  │                           │
│     │ (Service)│    │  (HTTP API)  │                           │
│     └──────────┘    └──────────────┘                           │
└─────────────────────────────────────────────────────────────────┘
```

---

## 2. Test Unitari - Service Layer

### 2.1 Regole per Unit Tests

I test unitari devono:
- Usare **Moq** per simulare le dipendenze
- Usare **EF Core InMemory** o **SQLite** per il database di test
- Testare **solo la logica del service**, non gli endpoint HTTP
- Essere **veloci** (< 100ms per test)

### 2.2 Test per RegistaService

| ID | Descrizione | Tipo |
|----|-------------|------|
| U-R1 | GetAllAsync - Lista vuota | Unit |
| U-R2 | GetAllAsync - Con dati | Unit |
| U-R3 | GetByIdAsync - Esiste | Unit |
| U-R4 | GetByIdAsync - Non esiste | Unit |
| U-R5 | CreateAsync - Dati validi | Unit |
| U-R6 | CreateAsync - Dati invalidi | Unit |
| U-R7 | UpdateAsync - Esiste | Unit |
| U-R8 | UpdateAsync - Non esiste | Unit |
| U-R9 | DeleteAsync - Esiste | Unit |
| U-R10 | DeleteAsync - Non esiste | Unit |
| U-R11 | GetFilmsByRegistaIdAsync - Con film | Unit |
| U-R12 | GetFilmsByRegistaIdAsync - Senza film | Unit |

### 2.3 Test per FilmService

| ID | Descrizione | Tipo |
|----|-------------|------|
| U-F1 | GetAllAsync - Lista vuota | Unit |
| U-F2 | GetAllAsync - Con dati | Unit |
| U-F3 | GetByIdAsync - Esiste | Unit |
| U-F4 | GetByIdAsync - Non esiste | Unit |
| U-F5 | CreateAsync - Dati validi | Unit |
| U-F6 | CreateAsync - Regista non esiste | Unit |
| U-F7 | UpdateAsync - Dati validi | Unit |
| U-F8 | UpdateAsync - Regista non esiste | Unit |
| U-F9 | DeleteAsync - Con proiezioni (fk restrict) | Unit |
| U-F10 | DeleteAsync - Senza proiezioni | Unit |

### 2.4 Test per CinemaService

| ID | Descrizione | Tipo |
|----|-------------|------|
| U-C1 | GetAllAsync - Lista vuota | Unit |
| U-C2 | CreateAsync - Dati validi | Unit |
| U-C3 | GetByIdAsync - Esiste | Unit |
| U-C4 | UpdateAsync - Dati validi | Unit |
| U-C5 | DeleteAsync - Esiste | Unit |

### 2.5 Test per ProiezioneService

| ID | Descrizione | Tipo |
|----|-------------|------|
| U-P1 | GetAllAsync - Lista vuota | Unit |
| U-P2 | CreateAsync - Dati validi | Unit |
| U-P3 | CreateAsync - Cinema non esiste | Unit |
| U-P4 | CreateAsync - Film non esiste | Unit |
| U-P5 | CreateAsync - Violazione unique | Unit |
| U-P6 | UpdateAsync - Dati validi | Unit |
| U-P7 | DeleteAsync - Esiste | Unit |

---

## 3. Test di Integrazione - API Endpoints

### 3.1 Regole per Integration Tests

I test di integrazione devono:
- Usare **WebApplicationFactory** per creare l'app in-memory
- Usare **HttpClient** per chiamare gli endpoint
- Usare **SQLite in-memory** per il database di test
- Testare il **flusso completo** HTTP → Service → Database
- Verificare **codici HTTP** e **serializzazione JSON**

### 3.2 Test per Entità `Regista`

#### R1: GET /registi - Lista vuota iniziale
- **Input**: Nessuno
- **Output atteso**: Lista vuota (200 OK, `[]`)
- **Criterio successo**: Risposta 200 con array JSON vuoto

#### R2: POST /registi - Creazione valida
- **Input**:
  ```json
  {
    "nome": "Christopher",
    "cognome": "Nolan",
    "nazionalita": "Britannica"
  }
  ```
- **Output atteso**: 201 Created con oggetto creato
- **Criterio successo**: HTTP 201, Id > 0, dati corretti

#### R3: GET /registi/{id} - Recupero esistente
- **Precondizione**: Regista creato con R2
- **Output atteso**: 200 OK con dati regista
- **Criterio successo**: HTTP 200, dati corrispondono all'input

#### R4: GET /registi/{id} - Recupero inesistente
- **Input**: ID non esistente (es. 99999)
- **Output atteso**: 404 Not Found
- **Criterio successo**: HTTP 404

#### R5: PUT /registi/{id} - Aggiornamento esistente
- **Input**:
  ```json
  {
    "nome": "Christopher",
    "cognnome": "Nolan",
    "nazionalita": "Statunitense"
  }
  ```
- **Output atteso**: 200 OK con dati aggiornati
- **Criterio successo**: HTTP 200, nazionalita aggiornata

#### R6: PUT /registi/{id} - Aggiornamento inesistente
- **Input**: ID non esistente
- **Output atteso**: 404 Not Found
- **Criterio successo**: HTTP 404

#### R7: DELETE /registi/{id} - Eliminazione esistente
- **Precondizione**: Regista esistente
- **Output atteso**: 204 No Content
- **Criterio successo**: HTTP 204, entity eliminata

#### R8: DELETE /registi/{id} - Eliminazione inesistente
- **Input**: ID non esistente
- **Output atteso**: 404 Not Found
- **Criterio successo**: HTTP 404

#### R9: POST /registi - Dati mancanti
- **Input**:
  ```json
  {
    "nome": "Christopher"
  }
  ```
- **Output atteso**: 400 Bad Request
- **Criterio successo**: HTTP 400

---

## 4. Test per Entità `Film`

### F1: GET /films - Lista vuota
- **Input**: Nessuno
- **Output atteso**: 200 OK, `[]`
- **Criterio successo**: Lista vuota

### F2: POST /films - Creazione valida
- **Precondizione**: Regista esistente
- **Input**:
  ```json
  {
    "titolo": "Inception",
    "dataProduzione": "2010-07-16",
    "registaId": 1,
    "durata": 148,
    "copertinaPath": "/media/inception.jpg",
    "filmatoPath": "/media/inception.mp4"
  }
  ```
- **Output atteso**: 201 Created
- **Criterio successo**: HTTP 201, dati corretti

### F3: POST /films - Copertina default
- **Precondizione**: Regista esistente
- **Input**:
  ```json
  {
    "titolo": "Interstellar",
    "dataProduzione": "2014-11-07",
    "registaId": 1,
    "durata": 169
  }
  ```
- **Output atteso**: 201 Created
- **Criterio successo**: `copertinaPath` valorizzato con default

### F4: POST /films - FK inesistente
- **Input**: `registaId` non esistente
- **Output atteso**: 400 Bad Request
- **Criterio successo**: HTTP 400

### F5: GET /films/{id} - Recupero esistente
- **Precondizione**: Film creato
- **Output atteso**: 200 OK
- **Criterio successo**: Dati corretti

### F6: PUT /films/{id} - Aggiornamento
- **Input**: Titolo modificato
- **Output atteso**: 200 OK
- **Criterio successo**: Titolo aggiornato

### F7: PUT /films/{id} - FK inesistente
- **Input**: `registaId` non esistente
- **Output atteso**: 400 Bad Request
- **Criterio successo**: HTTP 400

### F8: DELETE /films/{id} - Eliminazione
- **Precondizione**: Film esistente
- **Output atteso**: 204 No Content
- **Criterio successo**: HTTP 204

---

## 5. Test per Entità `Cinema`

### C1: GET /cinemas - Lista vuota
- **Output atteso**: 200 OK, `[]`

### C2: POST /cinemas - Creazione valida
- **Input**:
  ```json
  {
    "nome": "Cinema Odeon",
    "indirizzo": "Via Roma 10",
    "citta": "Milano"
  }
  ```
- **Output atteso**: 201 Created

### C3: GET /cinemas/{id} - Recupero
- **Output atteso**: 200 OK

### C4: PUT /cinemas/{id} - Aggiornamento
- **Output atteso**: 200 OK

### C5: DELETE /cinemas/{id} - Eliminazione
- **Output atteso**: 204 No Content

---

## 6. Test per Entità `Proiezione`

### P1: GET /proiezioni - Lista vuota
- **Output atteso**: 200 OK, `[]`

### P2: POST /proiezioni - Creazione valida
- **Precondizioni**: Film e Cinema esistenti
- **Input**:
  ```json
  {
    "cinemaId": 1,
    "filmId": 1,
    "data": "2024-12-25",
    "ora": "20:00"
  }
  ```
- **Output atteso**: 201 Created

### P3: POST /proiezioni - FK Cinema inesistente
- **Input**: `cinemaId` non esistente
- **Output atteso**: 400 Bad Request

### P4: POST /proiezioni - FK Film inesistente
- **Input**: `filmId` non esistente
- **Output atteso**: 400 Bad Request

### P5: POST /proiezioni - Violazione vincolo UNIQUE
- **Precondizione**: Proiezione esistente
- **Input**: Stessi valori di CinemaId, FilmId, Data, Ora
- **Output atteso**: 409 Conflict
- **Criterio successo**: HTTP 409

### P6: GET /proiezioni/{id} - Recupero
- **Output atteso**: 200 OK

### P7: PUT /proiezioni/{id} - Aggiornamento
- **Output atteso**: 200 OK

### P8: DELETE /proiezioni/{id} - Eliminazione
- **Output atteso**: 204 No Content

---

## 7. Test Integrati

### E1: Cascade Delete - Regista
- **Setup**: Crea Regista → Crea Film associato
- **Azione**: Elimina Regista
- **Verifica**: Film gestito secondo configurazione EF Core (DeleteBehavior.Restrict)

### E2: Cascade Delete - Film
- **Setup**: Crea Film → Crea Proiezione associata
- **Azione**: Elimina Film
- **Verifica**: Proiezione gestita secondo configurazione EF Core (DeleteBehavior.Restrict)

### E3: Full CRUD Flow
- **Setup**: Crea Regista → Film → Cinema → Proiezione
- **Verifica**: Lettura di tutte le entità
- **Cleanup**: Eliminazione in ordine inverso

---

## 8. Criteri di Accettazione

1. Tutti i test devono passare (100% success rate)
2. Codici HTTP corretti per ogni scenario
3. Validazione dati in input
4. Gestione corretta delle relazioni FK
5. Vincolo UNIQUE su Proiezione rispettato

---

## 9. Note Tecniche

### 9.1 Struttura Progetto Test

```
tests/
├── FilmAPI.Tests.csproj
├── Unit/                           # Test unitari
│   ├── RegistaServiceTests.cs
│   ├── FilmServiceTests.cs
│   ├── CinemaServiceTests.cs
│   └── ProiezioneServiceTests.cs
└── Integration/                    # Test di integrazione
    ├── CustomWebApplicationFactory.cs
    ├── RegistaEndpointsTests.cs
    ├── FilmEndpointsTests.cs
    ├── CinemaEndpointsTests.cs
    └── ProiezioneEndpointsTests.cs
```

### 9.2 Database per Test

- **Unit Tests**: EF Core InMemory o SQLite in-memory
- **Integration Tests**: SQLite in-memory (per supportare vincoli relazionali)
- Ogni test è indipendente (setup/teardown)
- HttpClient ottenuto da WebApplicationFactory
