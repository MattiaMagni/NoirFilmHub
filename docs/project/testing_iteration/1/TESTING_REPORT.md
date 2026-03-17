# Testing Report - FilmAPI

## 1. Stato Attuale

La configurazione di testing backend e' stata stabilizzata. Il progetto include sia test di integrazione che test unitari.

- Progetto test separato: `tests/FilmAPI.Tests.csproj`
- Infrastruttura: `WebApplicationFactory<Program>` + EF Core InMemory
- Struttura test: `tests/Unit/` + `tests/Integration/`
- Esecuzione: `dotnet test tests/FilmAPI.Tests.csproj`
- Risultato corrente: **66 test totali, 66 passati, 0 falliti**

---

## 2. Problemi Tecnici Risolti

### 2.1 Conflitto tra progetto principale e progetto test

**Problema**
Il progetto principale intercettava file di test in compilazione, causando errori su usings e riferimenti xUnit.

**Fix applicato**
In `FilmAPI.csproj` e' stato aggiunto:

```xml
<ItemGroup>
  <Compile Remove="tests\**\*.cs" />
</ItemGroup>
```

---

### 2.2 Accessibilita' della classe Program per i test

**Problema**
`WebApplicationFactory<Program>` richiede un tipo accessibile nel progetto API Minimal.

**Fix applicato**
In `Program.cs` e' stata dichiarata la classe parziale:

```csharp
public partial class Program;
```

---

### 2.3 Autodetect DB e stabilita' test

**Problema**
`ServerVersion.AutoDetect(connectionString)` e' corretto in runtime reale, ma nei test puo' introdurre dipendenza da connessione esterna.

**Fix applicato**
In `Program.cs` e' stata introdotta configurazione a variabili ambiente:

- `DB_USE_AUTODETECT` (`true`/`false`)
- `DB_SERVER_VERSION` (es. `10.11.0-mariadb`)

Comportamento:
- produzione/dev: autodetect attivo di default
- test: autodetect disattivato via env var impostate nella factory

---

### 2.4 Override provider EF Core nei test

**Problema**
Era necessario sostituire il database di produzione (MySQL) con un database isolato per i test.

**Fix applicato**
In `tests/Integration/CustomWebApplicationFactory.cs`:
- rimozione registrazioni precedenti di `FilmDbContext`
- registrazione `FilmDbContext` con `UseInMemoryDatabase`

---

### 2.5 Servizi con Dependency Injection

**Problema**
Per testare la logica di business in isolamento, era necessario un layer di servizi.

**Fix applicato**
Sono state create le classi Service con interfacce:
- `IRegistaService` + `RegistaService`
- `IFilmService` + `FilmService`
- `ICinemaService` + `CinemaService`
- `IProiezioneService` + `ProiezioneService`

Gli endpoint sono stati aggiornati per usare i servizi tramite DI.

---

## 3. Configurazione Ambiente Aggiornata

Aggiornati entrambi i file:

- `.env`
- `.env.example`

con le chiavi:

- `DB_USE_AUTODETECT=true`
- `DB_SERVER_VERSION=10.11.0-mariadb`

Questo mantiene compatibilita' con container `mariadb:lts` e permette fallback esplicito senza perdere l'autodetect in ambiente normale.

---

## 4. Copertura Implementata (TestSpecification)

Test implementati in `tests/ApiIntegrationTests.cs` con naming tracciabile per ID specifica.

### 4.1 Registi

- R1, R2, R3, R4, R5, R6, R7, R8, R9

### 4.2 Films

- F1, F2, F3, F4, F5, F6, F7, F8

### 4.3 Cinemas

- C1, C2, C3, C4, C5

### 4.4 Proiezioni

- P1, P2, P3, P4, P5, P6, P7, P8

### 4.5 Integrati

- E1, E2, E3

Nota: e' presente un test aggiuntivo preesistente per endpoint relazionale regista/film, mantenuto nella suite.

---

## 5. Risultato Esecuzione

Comando eseguito:

```bash
dotnet test tests/FilmAPI.Tests.csproj
```

Esito:

- Totale: 66
- Passati: 66
- Falliti: 0

---

## 6. Copertura Test Implementati

### 6.1 Test Unitari (33 test)

#### RegistaService (12 test)
- U-R1: GetAllAsync - Lista vuota
- U-R2: GetAllAsync - Con dati
- U-R3: GetByIdAsync - Esiste
- U-R4: GetByIdAsync - Non esiste
- U-R5: CreateAsync - Dati validi
- U-R6: CreateAsync - Dati invalidi
- U-R7: UpdateAsync - Esiste
- U-R8: UpdateAsync - Non esiste
- U-R9: DeleteAsync - Esiste
- U-R10: DeleteAsync - Non esiste
- U-R11: GetFilmsByRegistaIdAsync - Con film
- U-R12: GetFilmsByRegistaIdAsync - Senza film

#### FilmService (9 test)
- U-F1: GetAllAsync - Lista vuota
- U-F2: GetAllAsync - Con dati
- U-F3: GetByIdAsync - Esiste
- U-F4: GetByIdAsync - Non esiste
- U-F5: CreateAsync - Dati validi
- U-F6: CreateAsync - Regista non esiste
- U-F7: UpdateAsync - Dati validi
- U-F8: UpdateAsync - Regista non esiste
- U-F10: DeleteAsync - Esiste

#### CinemaService (5 test)
- U-C1: GetAllAsync - Lista vuota
- U-C2: CreateAsync - Dati validi
- U-C3: GetByIdAsync - Esiste
- U-C4: UpdateAsync - Dati validi
- U-C5: DeleteAsync - Esiste

#### ProiezioneService (7 test)
- U-P1: GetAllAsync - Lista vuota
- U-P2: CreateAsync - Dati validi
- U-P3: CreateAsync - Cinema non esiste
- U-P4: CreateAsync - Film non esiste
- U-P5: CreateAsync - Violazione unique
- U-P6: UpdateAsync - Dati validi
- U-P7: DeleteAsync - Esiste

### 6.2 Test di Integrazione (33 test)

#### Registi
- R1, R2, R3, R4, R5, R6, R7, R8, R9

#### Films
- F1, F2, F3, F4, F5, F6, F7, F8

#### Cinemas
- C1, C2, C3, C4, C5

#### Proiezioni
- P1, P2, P3, P4, P5, P6, P7, P8

#### Integrati
- E1, E2, E3

---

## 7. File Coinvolti

- `FilmAPI.csproj`
- `Program.cs`
- `.env.example`
- `tests/FilmAPI.Tests.csproj`
- `tests/CustomWebApplicationFactory.cs`
- `tests/ApiIntegrationTests.cs`

---

## 8. Conclusione

La pipeline di test e' stata ripristinata e resa affidabile. La suite copre l'intera specifica endpoint indicata in `TestSpecification.md` (con un test aggiuntivo) e risulta completamente verde.

*Report aggiornato il 16/03/2026*
