# Linee Guida per Agente AI - Test Backend FilmAPI

## Obiettivo

Queste linee guida servono a far generare a un agente AI la suite di test per FilmAPI (sia **unit tests** che **integration tests**) evitando i problemi di configurazione già incontrati.

---

## 1) Panoramica Tipologie di Test

### Test Unitari vs Test di Integrazione

| Aspetto | Unit Test | Integration Test |
|---------|-----------|-----------------|
| **Cosa testa** | Singolo metodo/classe (service) | Interazioni tra componenti (endpoint HTTP) |
| **Dipendenze** | Mock (Moq) | Reali (WebApplicationFactory) |
| **Database** | InMemory o SQLite | SQLite in-memory |
| **HTTP** | Mai usato | Sempre usato |
| **Velocità** | ~10ms | ~100-500ms |
| **Usa HttpClient** | No | Sì |

### Struttura del Progetto Test

```
tests/
├── FilmAPI.Tests.csproj
├── Unit/                           # Test unitari (service layer)
│   ├── RegistaServiceTests.cs
│   ├── FilmServiceTests.cs
│   └── ...
└── Integration/                    # Test di integrazione (HTTP endpoints)
    ├── CustomWebApplicationFactory.cs
    ├── RegistaEndpointsTests.cs
    └── ...
```

---

## 2) Setup Progetto Test (obbligatorio)

1. Usa un progetto test separato: `tests/FilmAPI.Tests.csproj`.
2. Mantieni `TargetFramework` allineato al progetto API: `net9.0`.
3. Pacchetti richiesti nel progetto test:

```xml
<ItemGroup>
  <!-- Framework di testing -->
  <PackageReference Include="xunit" Version="2.9.2" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />

  <!-- Integrazione ASP.NET Core -->
  <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.11" />
  
  <!-- Database per test (InMemory) -->
  <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="9.0.11" />
  
  <!-- Mocking e assertions -->
  <PackageReference Include="Moq" Version="4.20.72" />
  <PackageReference Include="FluentAssertions" Version="8.8.0" />
</ItemGroup>
```

4. Aggiungi `ProjectReference` al progetto API (`..\FilmAPI.csproj`).

---

## 3) Evitare Contaminazione tra Progetti

Nel progetto API (`FilmAPI.csproj`) escludi la cartella `tests` da tutti gli item rilevanti:

```xml
<ItemGroup>
  <Compile Remove="tests\**\*.cs" />
  <Content Remove="tests\**\*" />
  <None Remove="tests\**\*" />
</ItemGroup>
```

Questo evita errori di compilazione incrociata e warning di copia artifact (`MvcTestingAppManifest.json`).

---

## 4) Requisiti Minimi nel Progetto API

In `Program.cs`:

1. Dichiara in fondo al file:

```csharp
public partial class Program;
```

2. Gestisci versione DB via env var, mantenendo autodetect in runtime reale ma disattivabile nei test:

```csharp
var dbUseAutoDetect = (Environment.GetEnvironmentVariable("DB_USE_AUTODETECT") ?? "true")
    .Equals("true", StringComparison.OrdinalIgnoreCase);
var dbServerVersion = Environment.GetEnvironmentVariable("DB_SERVER_VERSION") ?? "10.11.0-mariadb";

var serverVersion = dbUseAutoDetect
    ? ServerVersion.AutoDetect(connectionString)
    : ServerVersion.Parse(dbServerVersion);
```

---

## 5) Configurazione Ambiente

In `.env` e `.env.example` includi:

```env
DB_USE_AUTODETECT=true
DB_SERVER_VERSION=10.11.0-mariadb
```

Note:
- runtime locale/container `mariadb:lts`: lasciare `DB_USE_AUTODETECT=true`
- test: impostare `DB_USE_AUTODETECT=false` nella test factory

---

## 6) CustomWebApplicationFactory (per Integration Tests)

Nel progetto test crea `CustomWebApplicationFactory` con queste regole:

1. Usa **InMemory** di EF Core per semplicità (in alternativa: SQLite in-memory).
2. Imposta env var test-safe nel costruttore:
   - `DB_USE_AUTODETECT=false`
   - `DB_SERVER_VERSION=10.11.0-mariadb`
3. In `ConfigureWebHost`, rimuovi registrazioni EF precedenti prima di aggiungere InMemory.
4. Fornisci helper `ResetDatabaseAsync(...)` per isolamento test.

```csharp
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";

    public CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("DB_USE_AUTODETECT", "false");
        Environment.SetEnvironmentVariable("DB_SERVER_VERSION", "10.11.0-mariadb");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Rimuovi registrazioni esistenti del DbContext
            var descriptors = services.Where(d => 
                d.ServiceType == typeof(DbContextOptions<FilmDbContext>) ||
                d.ServiceType == typeof(FilmDbContext)).ToList();
            
            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            // Aggiungi InMemory
            services.AddDbContext<FilmDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FilmDbContext>();
            db.Database.EnsureCreated();
        });
    }

    public async Task InitializeAsync() => await Task.CompletedTask;
    public new async Task DisposeAsync() => await Task.CompletedTask;

    public async Task ResetDatabaseAsync(Func<FilmDbContext, Task>? seed = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FilmDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        if (seed is not null) { await seed(db); await db.SaveChangesAsync(); }
    }
}
```

---

## 7) Regole di Implementazione Test

### Per Unit Tests

1. Usa **EF Core InMemory** per il database di test (più semplice di Moq per questo progetto).
2. Testa **solo la logica del service**, non gli endpoint HTTP.
3. Usa naming: `MethodName_Scenario_ExpectedResult`.
4. Usa **FluentAssertions** per assertion leggibili.
5. Ogni test deve essere **isolato** e **veloce** (< 100ms).

**Nota**: In questo progetto didattico abbiamo preferito iniettare direttamente un `FilmDbContext` configurato con InMemory anziché usare Moq per mockare il database. Questo approccio è più semplice e sufficiente per testare la logica dei service.

```csharp
public class RegistaServiceTests : IAsyncLifetime
{
    private readonly IServiceProvider _serviceProvider;
    private readonly FilmDbContext _context;
    private readonly IRegistaService _service;

    public RegistaServiceTests()
    {
        var services = new ServiceCollection();
        
        // Configura database InMemory
        var options = new DbContextOptionsBuilder<FilmDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new FilmDbContext(options);
        
        // Registra nel container DI del test
        services.AddScoped(_ => _context);
        services.AddScoped<IRegistaService, RegistaService>();
        services.AddScoped<IFilmService, FilmService>();
        
        _serviceProvider = services.BuildServiceProvider();
        _service = _serviceProvider.GetRequiredService<IRegistaService>();
    }

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesRegista()
    {
        // Arrange
        var dto = new RegistaCreateDTO { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "UK" };
        
        // Act
        var result = await _service.CreateAsync(dto);
        
        // Assert
        result.Nome.Should().Be("Christopher");
    }
}
```

### Per Integration Tests

1. Scrivi test endpoint-level con `HttpClient` (no chiamate dirette al DbContext nei test principali).
2. Usa **EF Core InMemory** per il database di test (più semplice di SQLite per questo progetto didattico).
3. Naming test con ID specifica (`R1_...`, `F3_...`) per tracciabilità.
4. Ogni test deve fare reset del DB all'inizio.
5. Crea helper riusabili per seed (`CreateRegistaAsync`, `CreateFilmAsync`, ecc.).
6. Asserzioni minime per ogni test:
   - codice HTTP atteso
   - payload atteso (campi chiave)

**Nota**: Per semplicità, questo progetto usa InMemory anche per i test di integrazione. SQLite sarebbe più appropriato per test pre-produzione grazie al supporto nativo dei vincoli relazionali.

```csharp
public class RegistiEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RegistiEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task R2_PostRegisti_CreatesEntity_AndReturnsCreated()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var request = new RegistaCreateDTO { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "UK" };
        var response = await client.PostAsJsonAsync("/registi/", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
```

---

## 8) Mappa Copertura Richiesta

### Unit Tests (Service Layer)

- RegistaService: ~12 test
- FilmService: ~10 test
- CinemaService: ~5 test
- ProiezioneService: ~7 test

**Totale Unit: ~34 test**

### Integration Tests (HTTP Endpoints)

- Registi: `R1-R9`
- Films: `F1-F8`
- Cinemas: `C1-C5`
- Proiezioni: `P1-P8`
- Integrati: `E1-E3`

**Totale Integration: ~33 test**

**Totale complessivo: ~67 test**

---

## 9) Check di Validazione Finale

Comandi da eseguire:

```bash
# Solo unit tests
dotnet test tests/FilmAPI.Tests.csproj --filter "FullyQualifiedName~Unit"

# Solo integration tests
dotnet test tests/FilmAPI.Tests.csproj --filter "FullyQualifiedName~Integration"

# Tutti i test
dotnet test tests/FilmAPI.Tests.csproj
dotnet test claude-code-test.sln
```

Criterio accettazione:
- tutti i test verdi
- nessun warning ricorrente di copia artifact da `tests` al progetto API

---

## 10) Errori Comuni da Evitare

1. Non usare `InMemory` EF per integration tests se devi testare vincoli relazionali/unique: preferisci SQLite in-memory.
2. Non lasciare `AutoDetect` attivo nei test.
3. Non dimenticare `public partial class Program;`.
4. Non lasciare la cartella `tests` inclusa nel progetto API.
5. Non cambiare target framework del test in versione diversa dal progetto API.
6. Non confondere unit test con integration test: unit test usano Moq, integration test usano HttpClient.

---

## 11) Prompt Pronto da Dare a un Agente AI (versione completa)

"Implementa la suite di test completa per FilmAPI in `tests/FilmAPI.Tests.csproj`:

1. **Struttura**: Crea sottocartelle `Unit/` e `Integration/`
2. **Unit tests**: usa Moq + EF InMemory/SQLite, testa RegistaService, FilmService, CinemaService, ProiezioneService (~34 test)
3. **Integration tests**: usa WebApplicationFactory + HttpClient + SQLite in-memory, testa tutti gli endpoint (~33 test)
4. **Naming**: usa convenzione `MethodName_Scenario_ExpectedResult` per unit, e ID specifica (`R1_...`, `F3_...`) per integration
5. Mantieni `Program.cs` con `public partial class Program;` e gestione `DB_USE_AUTODETECT`/`DB_SERVER_VERSION`
6. Crea `CustomWebApplicationFactory` con `ResetDatabaseAsync` per isolamento test
7. Verifica con `dotnet test tests/FilmAPI.Tests.csproj` e `dotnet test claude-code-test.sln`" 
