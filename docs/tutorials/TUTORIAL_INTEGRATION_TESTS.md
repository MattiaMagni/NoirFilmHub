# Guida ai Test di Integrazione - ASP.NET Core Minimal APIs

**Autore:** Claude AI Assistant
**Data:** 10 Marzo 2026
**Progetto di Riferimento:** FilmAPI
**Framework:** xUnit 2.9.2 + Microsoft.AspNetCore.Mvc.Testing 9.0.11
**Linguaggio:** C# / .NET 9.0

---

## Indice
1. [Introduzione ai Test di Integrazione](#1-introduzione-ai-test-di-integrazione)
2. [Test di Integrazione vs Test Unitari](#2-test-di-integrazione-vs-test-unitari)
3. [Setup dell'Ambiente di Integration Testing](#3-setup-dellambiente-di-integration-testing)
4. [WebApplicationFactory - Il Cuore degli Integration Test](#4-webapplicationfactory--il-cuore-degli-integration-test)
5. [HttpClient per Testare API](#5-httpclient-per-testare-api)
6. [Database InMemory per Integration Test](#6-database-inmemory-per-integration-test)
7. [Esempi Pratici dal Progetto FilmAPI](#7-esempi-pratici-dal-progetto-FilmAPI)
8. [Best Practices](#8-best-practices)

---

## 1. Introduzione ai Test di Integrazione

### 1.1 Cosa sono i Test di Integrazione?

I **test di integrazione** verificano che **più componenti** funzionino correttamente **insieme**. A differenza dei test unitari che testano un singolo metodo in isolamento, i test di integrazione testano le **interazioni** tra:

- Un endpoint HTTP e il service layer
- Il service layer e il database
- Più services che lavorano insieme
- L'intera richiesta HTTP → Service → Database → Response

```
┌──────────────────────────────────────────────────────────────────────┐
│                    TEST DI INTEGRAZIONE                             │
│                                                                      │
│  ┌────────────────┐        ┌──────────────┐        ┌─────────────┐ │
│  │   HTTP Client  │ ────>  │ API Endpoint │ ────>  │    Service  │ │
│  │                │        │              │        │             │ │
│  └────────────────┘        └──────────────┘        └──────┬──────┘ │
│                                                            │         │
│                                                            ▼         │
│                                                    ┌─────────────┐ │
│                                                    │   Database   │ │
│                                                    │  (InMemory)  │ │
│                                                    └─────────────┘ │
│                                                              ▲     │
└──────────────────────────────────────────────────────────────┼─────┘
                                                               │
                                                         ══════╩══════
                                                          TEST
                                                          CODE
```

### 1.2 Perché scrivere Test di Integrazione?

| Vantaggio | Descrizione |
|-----------|-------------|
| **Verifica integrità dell'API** | Testa che gli endpoint HTTP funzionino correttamente |
| **Test serializzazione JSON** | Verifica che request/response JSON siano corretti |
| **Valida codici HTTP** | Assicura che vengano restituiti i codici HTTP corretti (200, 404, 400, ecc.) |
| **Test flussi completi** | Verifica che un'intera richiesta HTTP funzioni end-to-end |
| **Preventivi** | Trova problemi di integrazione prima che vadano in produzione |

### 1.3 Cosa NON sono i Test di Integrazione

```
┌────────────────────────────────────────────────────────────────────┐
│                    TIPOLOGIA DI TEST                              │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │  UNIT TEST                                                 │    │
│  │  • Testa singole classi/metodi                            │    │
│  │  • Usa mock per isolare il codice                         │    │
│  │  • Molto veloci (< 10ms)                                  │    │
│  │  • Non usa database reali                                 │    │
│  └──────────────────────────────────────────────────────────┘    │
│                          ↑                                       │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │  INTEGRATION TEST (Quello che facciamo noi)              │    │
│  │  • Testa interazioni tra componenti                      │    │
│  │  • Usa componenti reali (non mock)                       │    │
│  │  • Più lenti (~100-500ms)                                │    │
│  │  • Usa database in memoria                               │    │
│  └──────────────────────────────────────────────────────────┘    │
│                          ↑                                       │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │  END-TO-END TEST (E2E)                                   │    │
│  │  • Testa l'intera applicazione                           │    │
│  │  • Usa database reali, servizi esterni                   │    │
│  │  • Molto lenti (> 1 secondo)                             │    │
│  │  • Spesso usa browser reali (Selenium)                   │    │
│  └──────────────────────────────────────────────────────────┘    │
└────────────────────────────────────────────────────────────────────┘
```

---

## 2. Test di Integrazione vs Test Unitari

### 2.2 Confronto Diretto

| Aspetto | Unit Test | Integration Test |
|---------|-----------|-------------------|
| **Cosa testa** | Singolo metodo/classe | Interazioni tra componenti |
| **Dipendenze** | Mock (simulati) | Reali (in-memory) |
| **Database** | InMemory per singolo test | InMemory per tutti i test |
| **HTTP** | Mai usato | Sempre usato |
| **Velocità** | Molto veloce (~10ms) | Più lento (~100-500ms) |
| **Affidabilità** | Molto affidabile | Meno affidabile (più punti di fallimento) |
| **Manutenzione** | Facile | Più complessa |

### 2.3 Quando usare quali?

```csharp
// ═════════════════════════════════════════════════════════════════
// UNIT TEST: Testa la logica del servizio
// ═════════════════════════════════════════════════════════════════
[Fact]
public async Task CreateAsync_WithValidData_CreatesRegista()
{
    var service = new RegistaService(mockContext, mockLogger);
    var result = await service.CreateAsync(dto);
    result.Should().NotBeNull();
}

// ═════════════════════════════════════════════════════════════════
// INTEGRATION TEST: Testa l'intera richiesta HTTP
// ═════════════════════════════════════════════════════════════════
[Fact]
public async Task CreateRegista_WithValidData_ReturnsCreated()
{
    var response = await client.PostAsJsonAsync("/registi", dto);
    response.StatusCode.Should().Be(HttpStatusCode.Created);
    var result = await response.Content.ReadFromJsonAsync<RegistaDTO>();
    result.Should().NotBeNull();
}
```

---

## 3. Setup dell'Ambiente di Integration Testing

### 3.1 Dipendenze NuGet Necessarie

```xml
<ItemGroup>
  <!-- Framework di testing -->
  <PackageReference Include="xunit" Version="2.9.2" />

  <!-- Test integrati ASP.NET Core -->
  <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.11" />

  <!-- Database in memoria -->
  <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="9.0.11" />

  <!-- Assertion leggibili -->
  <PackageReference Include="FluentAssertions" Version="8.8.0" />
</ItemGroup>
```

### 3.2 Struttura del Progetto

```
FilmAPI.Tests/
├── Integration/                    # Test di integrazione
│   ├── RegistaEndpointsTests.cs   # Test per endpoint Registi
│   └── ProiezioneEndpointsTests.cs
├── Unit/                           # Test unitari
│   ├── RegistaServiceTests.cs
│   └── FilmServiceTests.cs
└── FilmAPI.Tests.csproj
```

### 3.3 Accesso alla classe Program

Perché i test di integrazione possano creare un'istanza dell'applicazione, la classe `Program` deve essere accessibile al progetto di test:

```csharp
// Program.cs - Alla fine del file
public partial class Program { }
```

```csharp
// FilmAPI.Tests.csproj - Riferimento al progetto principale
<ItemGroup>
  <ProjectReference Include="..\FilmAPI\FilmAPI.csproj" />
  <InternalsVisibleTo Include="FilmAPI.Tests" />
</ItemGroup>
```

---

## 4. WebApplicationFactory - Il Cuore degli Integration Test

### 4.1 Cos'è WebApplicationFactory?

`WebApplicationFactory<TEntryPoint>` è una classe fornita da **Microsoft.AspNetCore.Mvc.Testing** che crea un'istanza dell'applicazione ASP.NET Core configurata per il testing.

**Cosa fa per noi?**
- ✅ Crea un'istanza completa dell'applicazione HTTP
- ✅ Configura un TestServer (non usa porte reali)
- ✅ Permette di configurare services specifici per il test
- ✅ Crea HttpClient configurato per comunicare con il server di test

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class RegistaEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RegistaEndpointsTests(WebApplicationFactory<Program> factory)
    {
        // La factory viene iniettata automaticamente da xUnit
        _factory = factory;
    }
}
```

### 4.2 IClassFixture - Condividere la Factory

`IClassFixture<T>` è un'interfaccia xUnit che permette di condividere un'istanza di una classe tra tutti i test di una classe di test.

```csharp
//                    ══════════════════════════════════════
//                    IClassFixture in Azione
//                    ══════════════════════════════════════
//
//  ┌────────────────────────────────────────────────────┐
//  │  RegistaEndpointsTests                          │
//  │                                                  │
//  │  • Una sola WebApplicationFactory creata        │
//  │  • Tutti i test nella classe la condividono      │
//  │  • I test possono essere eseguiti in parallelo  │
//  │                                                  │
//  │  [Fact] Test1() ──┐                             │
//  │  [Fact] Test2() ──┼──> Condividono _factory     │
//  │  [Fact] Test3() ──┘                             │
//  └────────────────────────────────────────────────────┘
```

### 4.3 Configurare Services Specifici per i Test

Spesso vogliamo sostituire il database reale con un database in memoria:

```csharp
private HttpClient CreateClient()
{
    return _factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureServices(services =>
        {
            // 1. Trova la configurazione del DbContext esistente
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<FilmAPI.Data.FilmDbContext>));

            // 2. Rimuovi la configurazione originale
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // 3. Aggiungi il database in memoria
            services.AddDbContext<FilmAPI.Data.FilmDbContext>(options =>
            {
                // GUID unico = database isolato per ogni test
                options.UseInMemoryDatabase(Guid.NewGuid().ToString());
            });
        });
    }).CreateClient();
}
```

**Visualizzazione del Processo:**

```
┌─────────────────────────────────────────────────────────────────┐
│              WebApplicationFactory                              │
│                                                                  │
│  ┌────────────────┐     ┌─────────────────┐                   │
│  │ Configurazione │ ──> │  Rimuovi DB      │                   │
│  │    Originale   │     │     Reale        │                   │
│  └────────────────┘     └─────────┬───────┘                   │
│                                   │                             │
│                                   ▼                             │
│  ┌─────────────────────────────────────────────────────┐      │
│  │         Aggiungi InMemory Database                  │      │
│  │  • Ogni test ha il suo database isolato            │      │
│  │  • Database pulito per ogni test                   │      │
│  └─────────────────────────────────────────────────────┘      │
│                           │                                  │
│                           ▼                                  │
│  ┌─────────────────────────────────────────────────────┐      │
│  │              Crea HttpClient                         │      │
│  │  • Configurato per comunicare con TestServer       │      │
│  │  • Non usa porte reali                              │      │
│  └─────────────────────────────────────────────────────┘      │
└─────────────────────────────────────────────────────────────────┘
```

---

## 5. HttpClient per Testare API

### 5.1 Il Testing con HttpClient

Negli integration test, usiamo `HttpClient` per fare richieste HTTP verso la nostra API:

```csharp
[Fact]
public async Task GetRegisti_ReturnsEmptyList_WhenNoRegistiExist()
{
    // ARRANGE: Ottieni un HTTP client configurato
    using var client = CreateClient();

    // ACT: Fai una richiesta HTTP GET
    var response = await client.GetAsync("/registi");

    // ASSERT: Verifica risposta HTTP
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var registi = await response.Content.ReadFromJsonAsync<IEnumerable<RegistaDTO>>();
    registi.Should().BeEmpty();
}
```

### 5.2 Metodi HTTP Comuni

| Metodo | Extension Method | Descrizione |
|--------|------------------|-------------|
| `GET` | `client.GetAsync(url)` | Ottieni risorse |
| `POST` | `client.PostAsJsonAsync(url, data)` | Crea nuova risorsa |
| `PUT` | `client.PutAsJsonAsync(url, data)` | Aggiorna risorsa |
| `DELETE` | `client.DeleteAsync(url)` | Elimina risorsa |

### 5.3 Verificare Codici HTTP

```csharp
using System.Net; // Per HttpStatusCode

// 200 OK
response.StatusCode.Should().Be(HttpStatusCode.OK);

// 201 Created
response.StatusCode.Should().Be(HttpStatusCode.Created);

// 204 No Content
response.StatusCode.Should().Be(HttpStatusCode.NoContent);

// 400 Bad Request
response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

// 404 Not Found
response.StatusCode.Should().Be(HttpStatusCode.NotFound);
```

### 5.4 Leggere il Response Body

```csharp
// Leggere come JSON tipizzato
var result = await response.Content.ReadFromJsonAsync<RegistaDTO>();
result.Should().NotBeNull();
result!.Cognome.Should().Be("Rossi");

// Leggere come collezione
var registi = await response.Content.ReadFromJsonAsync<IEnumerable<RegistaDTO>>();
registi.Should().HaveCountGreaterThan(0);
```

---

## 6. Database InMemory per Integration Test

### 6.1 Perché Usare InMemory Database?

| Aspetto | Database Reale | InMemory Database |
|---------|----------------|-------------------|
| **Velocità** | Lento (I/O disco o rete) | Veloce (tutto in RAM) |
| **Setup** | Complesso (installazione, configurazione) | Semplice (nessuna installazione) |
| **Isolamento** | Difficile (condiviso tra test) | Semplice (ogni test ha il suo) |
| **Affidabilità** | Più fedele alla produzione | Meno fedele (alcune feature non supportate) |
| **Uso ideale** | Test E2E o smoke tests | Test di integrazione |

### 6.2 Creare Database Isolati

```csharp
// GUID univoco = database isolato per ogni test
options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString());
```

**Perché usare GUID?**
- ✅ Garantisce isolamento completo tra test
- ✅ Non serve cleanup manuale
- ✅ Test possono girare in parallelo
- ✅ Nessun effetto collaterale tra test

```csharp
// Esempio di isolamento
[Test1] CreateDatabase("guid-1") // Usa database "guid-1"
[Test2] CreateDatabase("guid-2") // Usa database "guid-2"
[Test3] CreateDatabase("guid-3") // Usa database "guid-3"
// Ogni test ha il suo database pulito!
```

---

## 7. Esempi Pratici dal Progetto FilmAPI

### 7.1 Struttura Completa di una Classe di Test

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FilmAPI.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FilmAPI.Tests.Integration;

public class RegistaEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    // Factory condivisa tra tutti i test
    private readonly WebApplicationFactory<Program> _factory;

    // Constructor: xUnit inietta la factory automaticamente
    public RegistaEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    // Helper method: crea un client con database in memoria
    private HttpClient CreateClient()
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Rimuovi DbContext configurato nel Program.cs
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<FilmAPI.Data.FilmDbContext>));

                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Aggiungi database in memoria con GUID unico
                services.AddDbContext<FilmAPI.Data.FilmDbContext>(options =>
                {
                    options.UseInMemoryDatabase(Guid.NewGuid().ToString());
                });
            });
        }).CreateClient();
    }

    // ═════════════════════════════════════════════════════════════════
    // TEST CASE 1: GET /registi - Lista vuota
    // ═════════════════════════════════════════════════════════════════
    [Fact]
    public async Task GetRegisti_ReturnsEmptyList_WhenNoRegistiExist()
    {
        // ARRANGE: Crea HTTP client
        using var client = CreateClient();

        // ACT: Chiama l'endpoint GET /registi
        var response = await client.GetAsync("/registi");

        // ASSERT: Verifica status code e body
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var registi = await response.Content.ReadFromJsonAsync<IEnumerable<RegistaDTO>>();
        registi.Should().BeEmpty();
    }

    // ═════════════════════════════════════════════════════════════════
    // TEST CASE 2: POST /registi - Crea nuovo regista
    // ═════════════════════════════════════════════════════════════════
    [Fact]
    public async Task CreateRegista_WithValidData_ReturnsCreated()
    {
        // ARRANGE
        using var client = CreateClient();
        var dto = new CreateRegistaDTO("Mario", "Rossi", "Italiana");

        // ACT: POST con JSON body
        var response = await client.PostAsJsonAsync("/registi", dto);

        // ASSERT: Verifica 201 Created
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // ASSERT: Verifica response body
        var result = await response.Content.ReadFromJsonAsync<RegistaDTO>();
        result.Should().NotBeNull();
        result!.Cognome.Should().Be("Rossi");

        // ASSERT: Verifica Location header
        response.Headers.Location.Should().NotBeNull();
    }

    // ═════════════════════════════════════════════════════════════════
    // TEST CASE 3: GET /registi/{id} - Trova regista esistente
    // ═════════════════════════════════════════════════════════════════
    [Fact]
    public async Task GetRegistaById_WhenRegistaExists_ReturnsRegista()
    {
        // ARRANGE: Prima crea un regista
        using var client = CreateClient();
        var createDto = new CreateRegistaDTO("Mario", "Rossi", "Italiana");
        var createResponse = await client.PostAsJsonAsync("/registi", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<RegistaDTO>();

        // ACT: Chiama GET /registi/{id}
        var response = await client.GetAsync($"/registi/{created!.Id}");

        // ASSERT
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RegistaDTO>();
        result.Should().BeEquivalentTo(created); // Confronta tutte le proprietà
    }

    // ═════════════════════════════════════════════════════════════════
    // TEST CASE 4: GET /registi/{id} - Regista non trovato
    // ═════════════════════════════════════════════════════════════════
    [Fact]
    public async Task GetRegistaById_WhenRegistaNotFound_ReturnsNotFound()
    {
        // ARRANGE
        using var client = CreateClient();

        // ACT: Chiama con ID inesistente
        var response = await client.GetAsync("/registi/999");

        // ASSERT: Dovrebbe restituire 404 Not Found
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ═════════════════════════════════════════════════════════════════
    // TEST CASE 5: PUT /registi/{id} - Aggiorna regista
    // ═════════════════════════════════════════════════════════════════
    [Fact]
    public async Task UpdateRegista_WhenRegistaExists_ReturnsOk()
    {
        // ARRANGE: Crea un regista
        using var client = CreateClient();
        var createDto = new CreateRegistaDTO("Mario", "Rossi", "Italiana");
        var createResponse = await client.PostAsJsonAsync("/registi", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<RegistaDTO>();

        var updateDto = new UpdateRegistaDTO("Marco", "Verdi", "Italiana");

        // ACT: PUT per aggiornare
        var response = await client.PutAsJsonAsync($"/registi/{created!.Id}", updateDto);

        // ASSERT
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RegistaDTO>();
        result.Should().NotBeNull();
        result!.Cognome.Should().Be("Verdi");
        result.Nome.Should().Be("Marco");
    }

    // ═════════════════════════════════════════════════════════════════
    // TEST CASE 6: DELETE /registi/{id} - Elimina regista
    // ═════════════════════════════════════════════════════════════════
    [Fact]
    public async Task DeleteRegista_WhenRegistaExists_ReturnsNoContent()
    {
        // ARRANGE: Crea un regista
        using var client = CreateClient();
        var createDto = new CreateRegistaDTO("Mario", "Rossi", "Italiana");
        var createResponse = await client.PostAsJsonAsync("/registi", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<RegistaDTO>();

        // ACT: DELETE per eliminare
        var response = await client.DeleteAsync($"/registi/{created!.Id}");

        // ASSERT: 204 No Content (nessun body)
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // ASSERT: Verifica che sia stato davvero eliminato
        var getResponse = await client.GetAsync($"/registi/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ═════════════════════════════════════════════════════════════════
    // TEST CASE 7: GET /registi/{id}/films - Film di un regista
    // ═════════════════════════════════════════════════════════════════
    [Fact]
    public async Task GetFilmsByRegista_WhenRegistaHasNoFilms_ReturnsEmptyList()
    {
        // ARRANGE: Crea un regista (senza film)
        using var client = CreateClient();
        var createDto = new CreateRegistaDTO("Mario", "Rossi", "Italiana");
        var createResponse = await client.PostAsJsonAsync("/registi", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<RegistaDTO>();

        // ACT: Chiama GET /registi/{id}/films
        var response = await client.GetAsync($"/registi/{created!.Id}/films");

        // ASSERT
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var films = await response.Content.ReadFromJsonAsync<IEnumerable<FilmDTO>>();
        films.Should().BeEmpty();
    }
}
```

### 7.2 Flusso Completo di un Test di Integrazione

```
┌─────────────────────────────────────────────────────────────────┐
│             FLUSSO COMPLETO DI UN INTEGRATION TEST              │
└─────────────────────────────────────────────────────────────────┘

    1. ARRANGE - Preparazione
    ┌────────────────────────────────────────────────────────────┐
    │ using var client = CreateClient();                         │
    │ │                                                          │
    │ └─> Crea HttpClient con database in memoria isolato       │
    └────────────────────────────────────────────────────────────┘
                           │
                           ▼
    2. PREPARAZIONE DATI (se necessario)
    ┌────────────────────────────────────────────────────────────┐
    │ var createResponse = await client.PostAsJsonAsync(...);     │
    │ var created = await createResponse.Content...              │
    └────────────────────────────────────────────────────────────┘
                           │
                           ▼
    3. ACT - Esecuzione
    ┌────────────────────────────────────────────────────────────┐
    │ var response = await client.GetAsync("/registi/1");        │
    │ └─> Fai richiesta HTTP verso l'API                         │
    └────────────────────────────────────────────────────────────┘
                           │
                           ▼
    4. ASSERT - Verifica Status Code
    ┌────────────────────────────────────────────────────────────┐
    │ response.StatusCode.Should().Be(HttpStatusCode.OK);        │
    │ └─> Verifica che il codice HTTP sia quello atteso        │
    └────────────────────────────────────────────────────────────┘
                           │
                           ▼
    5. ASSERT - Verifica Response Body
    ┌────────────────────────────────────────────────────────────┐
    │ var result = await response.Content.ReadFromJsonAsync<>();│
    │ result.Should().NotBeNull();                               │
    │ └─> Verifica che i dati restituiti siano corretti        │
    └────────────────────────────────────────────────────────────┘
```

---

## 8. Best Practices

### 8.1 Organizzazione dei Test

```csharp
public class RegistaEndpointsTests
{
    // Gruppo 1: GET endpoints
    [Fact]
    public async Task GetAll_ReturnsList() { }

    [Fact]
    public async Task GetById_WhenExists_ReturnsRegista() { }

    [Fact]
    public async Task GetById_WhenNotFound_Returns404() { }

    // Gruppo 2: POST endpoint
    [Fact]
    public async Task Create_WithValidData_Returns201() { }

    [Fact]
    public async Task Create_WithInvalidData_Returns400() { }

    // Gruppo 3: PUT endpoint
    [Fact]
    public async Task Update_WhenExists_Returns200() { }

    [Fact]
    public async Task Update_WhenNotFound_Returns404() { }

    // Gruppo 4: DELETE endpoint
    [Fact]
    public async Task Delete_WhenExists_Returns204() { }

    [Fact]
    public async Task Delete_WhenNotFound_Returns404() { }

    // Gruppo 5: Nested endpoints
    [Fact]
    public async Task GetFilms_WhenRegistaHasNoFilms_ReturnsEmpty() { }
}
```

### 8.2 Checklist per Ogni Endpoint

Per ogni endpoint API, dovresti testare:

```
☐ GET con risorsa esistente → 200 OK con dati corretti
☐ GET con risorsa inesistente → 404 Not Found
☐ GET con lista vuota → 200 OK con array vuoto
☐ POST con dati validi → 201 Created con Location header
☐ POST con dati invalidi → 400 Bad Request
☐ PUT con risorsa esistente → 200 OK con dati aggiornati
☐ PUT con risorsa inesistente → 404 Not Found
☐ DELETE con risorsa esistente → 204 No Content
☐ DELETE con risorsa inesistente → 404 Not Found
```

### 8.3 Errori Comuni da Evitare

```csharp
// ❌ ERRORE: Non verifica il body della risposta
[Fact]
public async Task Create_ReturnsCreated()
{
    var response = await client.PostAsJsonAsync("/registi", dto);
    response.StatusCode.Should().Be(HttpStatusCode.Created);
    // Manca: verifica che il response sia corretto!
}

// ✅ CORRETTO: Verifica tutto
[Fact]
public async Task Create_ReturnsCreated()
{
    var response = await client.PostAsJsonAsync("/registi", dto);
    response.StatusCode.Should().Be(HttpStatusCode.Created);

    var result = await response.Content.ReadFromJsonAsync<RegistaDTO>();
    result.Should().NotBeNull();
    result!.Cognome.Should().Be("Rossi");
}

// ❌ ERRORE: Hardcoded ID
[Fact]
public async Task GetById_ReturnsRegista()
{
    var response = await client.GetAsync("/registi/1");
    // Se l'ID 1 non esiste, il test fallisce!
}

// ✅ CORRETTO: Crea prima il dato
[Fact]
public async Task GetById_ReturnsRegista()
{
    // Prima crea il regista
    var created = await CreateRegista(client);
    var response = await client.GetAsync($"/registi/{created.Id}");
}
```

### 8.4 Eseguire gli Integration Test

```bash
# Esegui solo test di integrazione
dotnet test --filter "FullyQualifiedName~Integration"

# Esegui con output dettagliato
dotnet test --filter "FullyQualifiedName~Integration" --logger "console;verbosity=detailed"

# Esegui un test specifico
dotnet test --filter "FullyQualifiedName~CreateRegista_WithValidData"
```

---

## Riepilogo

### Concetti Chiave

| Concetto | Descrizione |
|----------|-------------|
| **Integration Test** | Testa interazioni tra componenti dell'applicazione |
| **WebApplicationFactory** | Crea un'istanza dell'app per il testing |
| **IClassFixture** | Condivide la factory tra tutti i test della classe |
| **HttpClient** | Fa richieste HTTP verso l'API |
| **InMemory Database** | Database simulato per velocizzare i test |
| **Arrange-Act-Assert** | Pattern per organizzare i test |

### Prossimi Passi

1. **Scrivi il tuo primo integration test** → Copia un test esistente e modificane l'endpoint
2. **Testa tutti gli endpoint** → Segui la checklist per ogni endpoint
3. **Esegui i test regolarmente** → Ogni volta che modifichi un endpoint
4. **Mantieni i test aggiornati** → Quando cambi API, aggiorna anche i test

### Risorse

- [Microsoft Docs: Integration Tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
- [WebApplicationFactory Documentation](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests#webapplicationfactory)

---

**Documento creato il:** 10 Marzo 2026
**Versione:** 1.0
**Progetto:** FilmAPI - Tutorial Test di Integrazione
