# Guida Introduttiva al Testing in FilmAPI

**Autore:** Claude AI Assistant
**Data:** 16 Marzo 2026
**Progetto di Riferimento:** FilmAPI
**Linguaggio:** C# / .NET 9.0

---

## Indice

1. [Panoramica della Strategia di Testing](#1-panoramica-della-strategia-di-testing)
2. [Esecuzione dei Test](#2-esecuzione-dei-test)
3. [Architettura dei Test](#3-architettura-dei-test)
4. [Stack di Database Doppio: Produzione e Testing](#4-stack-di-database-doppio-produzione-e-testing)
5. [Introduzione dei Servizi e Dependency Injection](#5-introduzione-dei-servizi-e-dependency-injection)
6. [Comandi Utili](#6-comandi-utili)

---

## 1. Panoramica della Strategia di Testing

Il progetto FilmAPI adotta una strategia di testing stratificata che comprende due tipologie principali di test:

### 1.1 Test Unitari

I test unitari verificano il comportamento di singole unità di codice, tipicamente metodi all'interno delle classi service, in isolamento dalle dipendenze esterne. Questi test utilizano un database in-memory per simulare le operazioni di persistenza senza modificare i dati di produzione. La caratteristica distintiva dei test unitari nel progetto FilmAPI è l'impiego di Entity Framework Core InMemory, un provider che consente di eseguire query e operazioni CRUD su un database virtuale residente interamente in memoria RAM.

I test unitari offrono numerosi vantaggi nell'ambito dello sviluppo software: eseguono in tempi estremamente rapidi (dell'ordine dei millisecondi), non richiedono infrastruttura esterna come server di database, garantiscono l'isolamento completo tra un test e l'altro, e permettono di verificare la logica di business in modofocused e controllato. Nel contesto didattico del progetto, i test unitari rappresentano uno strumento fondamentale per comprendere il comportamento dei singoli componenti prima di verificare le interazioni complesse.

### 1.2 Test di Integrazione

I test di integrazione verificano che gli endpoint HTTP dell'API funzionino correttamente quando chiamati da un client esterno. A differenza dei test unitari, i test di integrazione coinvolgono l'intera catena di elaborazione di una richiesta HTTP: dal routing dell'endpoint fino alla persistenza nel database, passando per la validazione dei dati, l'esecuzione della logica di business nel service layer e la serializzazione della risposta in formato JSON.

Questa tipologia di test utilizza la classe `WebApplicationFactory` fornita da ASP.NET Core per creare un'istanza in-memory dell'applicazione completa. Il test effettua chiamate HTTP reali agli endpoint utilizzando `HttpClient`, esattamente come farebbe un client esterno, ma senza necessitare di avviare fisicamente il server su una porta di rete.

### 1.3 Tabella Comparativa

| Caratteristica | Test Unitari | Test di Integrazione |
|----------------|--------------|----------------------|
| Scope | Singolo metodo/classe | Intera richiesta HTTP |
| Database | InMemory (virtuale) | InMemory (isolato per test) |
| Tempo di esecuzione | < 10ms | 100-500ms |
| Dipendenze esterne | Nessuna | Nessuna (tutto in-memory) |
| Numero consigliato | Molti (decine/centinaia) | Pochi (33 nel progetto) |

---

## 2. Esecuzione dei Test

### 2.1 Comandi Fondamentali

L'esecuzione dei test in .NET avviene tramite l'interfaccia a riga di comando `dotnet test`. Di seguito sono riportati i comandi più utilizzati nel progetto FilmAPI:

```bash
# Esegue tutti i test presenti nel progetto
dotnet test

# Esegue tutti i test con output dettagliato
dotnet test --verbosity detailed

# Esegue tutti i test con output minimal (solo riepilogo)
dotnet test --verbosity minimal

# Esegue i test unitari
dotnet test --filter "FullyQualifiedName~Unit"

# Esegue i test di integrazione
dotnet test --filter "FullyQualifiedName~Integration"

# Esegue un test specifico per nome
dotnet test --filter "FullyQualifiedName~RegistaServiceTests.U_R1"

# Esegue i test saltando la ricompilazione (se già compilati)
dotnet test --no-build

# Esegue i test generando un report di coverage
dotnet test --collect:"XPlat Code Coverage"
```

### 2.2 Build dei Progetti di Test

Prima di eseguire i test, .NET compila automaticamente tutti i progetti dipendenti. È possibile compilare manualmente i progetti per verificare la presenza di errori di compilazione:

```bash
# Compila il progetto principale FilmAPI
dotnet build FilmAPI.csproj

# Compila il progetto di test
dotnet build tests/FilmAPI.Tests.csproj

# Compila entrambi i progetti
dotnet build
```

### 2.3 Configurazione del Progetto di Test

Il file `FilmAPI.Tests.csproj` contiene le dipendenze necessarie per l'esecuzione dei test:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="FluentAssertions" Version="8.8.0" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.11" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="9.0.11" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\FilmAPI.csproj" />
  </ItemGroup>
</Project>
```

---

## 3. Architettura dei Test

### 3.1 Struttura delle Cartelle

Il progetto di test FilmAPI è organizzato secondo una struttura che rispecchia le best practices per progetti .NET di medie dimensioni:

```
tests/
├── FilmAPI.Tests.csproj              # File di progetto
├── Unit/                             # Test unitari
│   ├── RegistaServiceTests.cs       # Test per RegistaService
│   ├── FilmServiceTests.cs          # Test per FilmService
│   ├── CinemaServiceTests.cs        # Test per CinemaService
│   └── ProiezioneServiceTests.cs    # Test per ProiezioneService
└── Integration/                     # Test di integrazione
    ├── ApiIntegrationTests.cs       # Test per tutti gli endpoint
    └── CustomWebApplicationFactory.cs  # Factory per l'app in-memory
```

### 3.2 Test Unitari: Chiamata Diretta ai Servizi

Nei test unitari, il codice sotto test viene invocato direttamente senza passare attraverso il layer HTTP. Il test crea un'istanza del service e chiama i suoi metodi direttamente, come illustrato nel seguente esempio:

```csharp
[Fact]
public async Task U_R1_GetAllAsync_WhenNoRegistiExist_ReturnsEmptyList()
{
    // Il test chiama direttamente il metodo del service
    var result = await _service.GetAllAsync();
    
    // Verifica il risultato
    result.Should().BeEmpty();
}
```

Questa modalità di testing presenta diversi vantaggi: l'esecuzione è estremamente veloce in quanto non comporta l'inizializzazione dell'intera pipeline HTTP, i test sono facili da scrivere e mantenere grazie alla loro semplicità, e l'isolamento del codice sotto test è garantito in quanto non dipende da componenti esterni.

### 3.3 Test di Integrazione: Chiamata agli Endpoint HTTP

Nei test di integrazione, il test effettua chiamate HTTP reali utilizzando la classe `HttpClient`. Il flusso di esecuzione attraversa l'intera applicazione, come dimostrato nel seguente schema:

```
Test → HttpClient → Endpoint HTTP → Service → Database → Response HTTP → Test
```

L'esempio seguente illustra come viene effettuata una chiamata HTTP in un test di integrazione:

```csharp
[Fact]
public async Task R1_GetAllRegisti_ReturnsOk()
{
    // Arrange: il test prepara una richiesta HTTP
    var client = _factory.CreateClient();
    
    // Act: effettua la chiamata HTTP GET all'endpoint
    var response = await client.GetAsync("/api/registi");
    
    // Assert: verifica il codice di stato HTTP
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    
    // Verifica il contenuto della risposta JSON
    var content = await response.Content.ReadFromJsonAsync<List<RegistaDTO>>();
    content.Should().BeEmpty();
}
```

La classe `CustomWebApplicationFactory` si occupa di configurare un'istanza in-memory dell'applicazione ASP.NET Core, sostituendo il database di produzione con un database InMemory isolato per i test.

---

## 4. Stack di Database Doppio: Produzione e Testing

### 4.1 Il Problema della Gestione del Database nei Test

Una delle sfide più significative nei test automatizzati è gestire l'accesso al database. Esistono due approcci principali: utilizzare il database di produzione (con tutti i rischi associati alla modifica dei dati) oppure utilizzare un database separato dedicato ai test. Il progetto FilmAPI adotta il secondo approccio, utilizzando database InMemory per entrambe le tipologie di test.

### 4.2 Perché Utilizzare un Database Diverso da Quello di Produzione

L'utilizzo di un database separato per i test è una pratica fondamentale per diversi motivi che riguardano sia l'affidabilità dei test stessi sia la protezione dei dati di produzione.

Il primo motivo riguarda l'isolamento dei test. Ogni test deve poter essere eseguito in modo indipendente dagli altri, senza che l'esecuzione di un test possa influenzare il risultato di un altro. Se i test utilizzassero il database di produzione, un test che crea dati potrebbe interferire con un test che verifica l'assenza di dati, rendendo i risultati non deterministici e non affidabili.

Il secondo motivo riguarda la velocità di esecuzione. I database InMemory operano interamente in RAM e non richiedono operazioni di I/O su disco. Questo si traduce in tempi di esecuzione dei test nell'ordine dei millisecondi, contro i secondi o decine di secondi necessari con database su disco. Per progetti con centinaia di test, questa differenza può tradursi in minuti di tempo risparmiato.

Il terzo motivo riguarda la protezione dei dati. I test che verificano operazioni di creazione, modifica o eliminazione non devono in alcun modo modificare i dati di produzione. Anche quando i test vengono eseguiti in ambienti di staging o development, l'utilizzo di un database separato garantisce che eventuali errori nei test non provochino perdita o corruzione di dati.

### 4.3 Implementazione del Database InMemory con Entity Framework Core

Entity Framework Core fornisce il provider InMemory che consente di configurare un database virtuale interamente in memoria RAM. Questo approccio è particolarmente utile per i test perché implementa la stessa interfaccia `DbContext` utilizzata con i database reali, senza richiedere modifiche al codice dell'applicazione.

#### Configurazione nei Test Unitari

Nei test unitari, il database InMemory viene configurato nel costruttore della classe di test:

```csharp
public class RegistaServiceTests : IAsyncLifetime
{
    private readonly IServiceProvider _serviceProvider;
    private readonly FilmDbContext _context;
    private readonly IRegistaService _service;

    public RegistaServiceTests()
    {
        var services = new ServiceCollection();
        
        // Configura il database InMemory con un nome univoco
        var options = new DbContextOptionsBuilder<FilmDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        // Crea il contesto del database
        _context = new FilmDbContext(options);
        
        // Registra le dipendenze nel container DI
        services.AddScoped(_ => _context);
        services.AddScoped<IRegistaService, RegistaService>();
        services.AddScoped<IFilmService, FilmService>();
        
        // Costruisce il service provider
        _serviceProvider = services.BuildServiceProvider();
        _service = _serviceProvider.GetRequiredService<IRegistaService>();
    }
}
```

L'utilizzo di `Guid.NewGuid().ToString()` come nome del database garantisce che ogni istanza della classe di test utilizzi un database completamente isolato. Questo è fondamentale quando i test vengono eseguiti in parallelo, poiché previene qualsiasi interferenza tra test eseguiti simultaneamente.

#### Configurazione nei Test di Integrazione

Nei test di integrazione, la configurazione del database avviene nella `CustomWebApplicationFactory`, che estende `WebApplicationFactory` di ASP.NET Core:

```csharp
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Rimuove il servizio DbContext esistente
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<FilmDbContext>));
            
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }
            
            // Aggiunge il database InMemory per i test
            services.AddDbContext<FilmDbContext>(options =>
            {
                options.UseInMemoryDatabase("IntegrationTestDb");
            });
            
            // Costruisce il service provider per garantire l'inizializzazione
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FilmDbContext>();
            db.Database.EnsureCreated();
        });
    }
}
```

### 4.4 Differenze Tra InMemory e Database Reale

È importante comprendere che il database InMemory di Entity Framework Core non è un sostituto perfetto del database di produzione. Esistono alcune differenze comportamentali che devono essere considerate:

La prima differenza riguarda i vincoli di integrità referenziale. Il provider InMemory non enforce i vincoli di chiave esterna nello stesso modo del database reale. Per questo motivo, nel `ProiezioneService` è stato necessario aggiungere un controllo esplicito per verificare l'esistenza di duplicati prima dell'inserimento, come illustrato nel codice seguente:

```csharp
// Controllo esplicito per duplicati (necessario per InMemory)
var existing = await _context.Proiezioni
    .AnyAsync(p => p.CinemaId == dto.CinemaId 
        && p.FilmId == dto.FilmId 
        && p.Data == dto.Data 
        && p.Ora == dto.Ora);

if (existing)
{
    throw new InvalidOperationException("Esiste già una proiezione...");
}
```

Questo codice non sarebbe necessario con un database reale come MySQL o SQLite, poiché il vincolo UNIQUE verrebbe enforce dal database stesso. Tuttavia, il controllo esplicito migliora anche il comportamento dell'applicazione in produzione, fornendo un messaggio di errore più chiaro all'utente.

La seconda differenza riguarda le transazioni. Il database InMemory non supporta transazioni distribuite nello stesso modo dei database relazionali. Questo generalmente non è un problema per i test, ma può influenzare il comportamento di operazioni che coinvolgono multiple entità.

### 4.5 Gestione del DbContext in Produzione

In produzione, l'applicazione FilmAPI utilizza MySQL come database relazionale, configurato in `Program.cs`:

```csharp
// Configurazione del database in produzione
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<FilmDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
```

Il `FilmDbContext` è condiviso sia dai test che dall'applicazione di produzione, ma viene configurato con provider diversi: MySQL per la produzione e InMemory per i test. Questo approccio garantisce che il codice di accesso ai dati sia identico in entrambi gli ambienti, mentre la configurazione del provider specifico viene gestita dal framework di dependency injection.

### 4.6 Selezione del Database: Nessuna Modifica al Backend

Un aspetto fondamentale da comprendere è che **il progetto di backend (FilmAPI) non contiene alcun riferimento al database utilizzato nei test**. Il codice sorgente dell'applicazione rimane completamente ignaro di quale database venga utilizzato durante l'esecuzione dei test. La selezione del database InMemory avviene interamente all'interno del codice dei test, non nel backend.

#### Perché il Backend Non Contiene Codice di Test

Questa separazione è intenzionale e rappresenta una best practice dell'architettura del software. Il progetto di backend dovrebbe contenere solo la logica di business e la configurazione necessaria per l'esecuzione in produzione. Includere logica condizionale per selezionare il database in base all'ambiente (produzione vs test) introdurrebbe complessità non necessaria nel codice di produzione e violerebbe il principio di separation of concerns.

Il backend definisce solo l'interfaccia `DbContext` e i servizi che lo utilizzano. La configurazione specifica del database (MySQL, InMemory, SQLite, ecc.) viene decisa dal chiamante, che nel caso dei test è il codice di test stesso.

#### Schema della Selezione del Database

```
┌─────────────────────────────────────────────────────────────────┐
│                      PROGETTO BACKEND                           │
│                                                                 │
│   FilmDbContext.cs    ──────► Non conosce il database usato   │
│   RegistaService.cs   ──────► Riceve DbContext tramite DI      │
│   Program.cs          ──────► Configura MySQL per produzione    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │ DbContext iniettato
                              │
┌─────────────────────────────────────────────────────────────────┐
│                      PROGETTO DI TEST                           │
│                                                                 │
│   RegistaServiceTests.cs    ──► Configura UseInMemory          │
│   CustomWebApplicationFactory.cs ──► Sostituisce con InMemory  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

#### Come Avviene la Sostituzione nei Test Unitari

Nei test unitari, il service riceve il `DbContext` tramite dependency injection, ma è il test stesso a creare e configurare il contesto con il provider InMemory:

```csharp
// Il service riceve FilmDbContext, ma non sa che è InMemory
services.AddScoped<IRegistaService, RegistaService>();

// Il test crea e configura il DbContext
var options = new DbContextOptionsBuilder<FilmDbContext>()
    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
    .Options;

var context = new FilmDbContext(options);
services.AddScoped(_ => context);  // Sostituisce quello di produzione
```

#### Come Avviene la Sostituzione nei Test di Integrazione

Nei test di integrazione, la `CustomWebApplicationFactory` intercetta la registrazione del `DbContext` nel container DI dell'applicazione e la sostituisce con una configurazione InMemory:

```csharp
builder.ConfigureServices(services =>
{
    // 1. Trova la registrazione esistente del DbContext (quella per MySQL)
    var descriptor = services.SingleOrDefault(
        d => d.ServiceType == typeof(DbContextOptions<FilmDbContext>));
    
    // 2. La rimuove
    if (descriptor != null)
        services.Remove(descriptor);
    
    // 3. Aggiunge la configurazione InMemory
    services.AddDbContext<FilmDbContext>(options =>
    {
        options.UseInMemoryDatabase("IntegrationTestDb");
    });
});
```

Questo meccanismo permette di testare l'applicazione esattamente come funzionerebbe in produzione (stessa logica, stessi endpoint, stessi servizi), ma con un database isolato e temporaneo.

#### Vantaggi di Questo Approccio

- **Zero modifiche al backend**: Il codice di produzione non deve contenere condizioni o configurazioni per i test
- **Isolamento completo**: I test non dipendono da configurazioni esterne o variabili d'ambiente
- **Semplicità**: Ogni test configura il proprio ambiente in modo autonomo e auto-contenuto
- **Manutenibilità**: Aggiungere nuovi test o modificare quelli esistenti non richiede modifiche al backend

### 4.7 Alternative al Database InMemory: SQLite e MySQL/MariaDB

Il progetto FilmAPI utilizza attualmente il database InMemory di Entity Framework Core per i test. Questa scelta è ottimale per un progetto didattico in quanto non richiede setup aggiuntivo e garantisce tempi di esecuzione estremamente rapidi. Tuttavia, in scenari di testing pre-produzione o per progetti più maturi, può essere necessario utilizzare un database reale come SQLite o una seconda istanza di MySQL/MariaDB.

#### Perché Considerare Database Reali nei Test

Esistono diversi motivi per cui un progetto potrebbe decidere di utilizzare un database reale per i test invece del database InMemory. Il primo motivo riguarda la conformità comportamentale: il database InMemory non si comporta esattamente come un database relazionale reale, specialmente per quanto riguarda i vincoli di integrità referenziale, le transazioni e le query complesse. Utilizzando un database reale si ottiene una maggiore certezza che il codice funzionerà correttamente anche in produzione.

Il secondo motivo riguarda il testing pre-release: prima di deployare in produzione, è prassi comune eseguire i test su un ambiente che replica esattamente l'ambiente di produzione, incluso il database. Questo permette di individuare problemi specifici del database relazionale che non emergerebbero con InMemory.

Il terzo motivo riguarda l'integrazione continua: in pipeline CI/CD che includono test automatici, l'utilizzo di un database reale come SQLite (che non richiede installazione separata) può essere preferito per la sua semplicità e affidabilità.

#### Utilizzo di SQLite per i Test

SQLite è un database relazionale leggero che memorizza i dati in un file locale. Offre un buon compromesso tra la semplicità dell'InMemory e la conformità comportamentale di un database completo. Per utilizzare SQLite nei test, è necessario aggiungere il pacchetto NuGet appropriato e modificare la configurazione del database:

```bash
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
```

La configurazione nei test unitari diverrebbe:

```csharp
public class RegistaServiceTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly IServiceProvider _serviceProvider;
    private readonly FilmDbContext _context;

    public RegistaServiceTests()
    {
        _dbPath = $"test_{Guid.NewGuid()}.db";
        
        var services = new ServiceCollection();
        
        var options = new DbContextOptionsBuilder<FilmDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        
        _context = new FilmDbContext(options);
        _context.Database.EnsureCreated();
        
        services.AddScoped(_ => _context);
        services.AddScoped<IRegistaService, RegistaService>();
        
        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
        File.Delete(_dbPath);
    }
}
```

L'utilizzo di SQLite presenta diversi vantaggi rispetto a InMemory: supporta vincoli di chiave esterna e unicità, le transazioni funzionano correttamente, il comportamento è molto simile a MySQL, e il file può essere eliminato dopo ogni test garantendo l'isolamento. Gli svantaggi includono la necessità di gestire il cleanup dei file temporanei e una leggera riduzione delle prestazioni rispetto a InMemory.

#### Utilizzo di MySQL/MariaDB per i Test

Per progetti che richiedono un testing completo con il database di produzione, è possibile configurare una seconda istanza di MySQL o MariaDB dedicata ai test. Questa configurazione è tipica degli ambienti di staging o pre-produzione:

```csharp
// In CustomWebApplicationFactory per test di integrazione
builder.ConfigureServices(services =>
{
    var descriptor = services.SingleOrDefault(
        d => d.ServiceType == typeof(DbContextOptions<FilmDbContext>));
    
    if (descriptor != null)
        services.Remove(descriptor);
    
    // Database di test separato
    services.AddDbContext<FilmDbContext>(options =>
    {
        options.UseMySql(
            "Server=localhost;Database=FilmAPITest;User=root;Password=...",
            ServerVersion.AutoDetect("Server=localhost")
        );
    });
});
```

È necessario creare il database `FilmAPITest` prima di eseguire i test:

```sql
CREATE DATABASE FilmAPITest;
```

Per garantire l'isolamento tra i test, esistono diverse strategie. La prima prevede l'utilizzo di un database separato per ogni sessione di test, creando un database univoco per ogni esecuzione. La seconda prevede l'utilizzo di transazioni che vengono rolled back dopo ogni test. La terza prevede il truncate delle tabelle prima di ogni test.

#### Tabella Comparativa delle Alternative

| Caratteristica | InMemory | SQLite | MySQL/MariaDB |
|----------------|----------|--------|---------------|
| Velocità | Molto alta | Alta | Media |
| Setup richiesto | Nessuno | File temporaneo | Server MySQL |
| Vincoli DB | Non enforce | Parziale | Completi |
| Transazioni | Limitate | Complete | Complete |
| Comportamento reale | Basso | Medio | Alto |
| Parallelismo test | Ottimale | Buono | Ottimale |
| Adatto a CI/CD | Sì | Sì | Sì (con container) |

#### Raccomandazioni per il Progetto Didattico

Per un progetto didattico come FilmAPI, la scelta di utilizzare il database InMemory rimane la più appropriata per diversi motivi. Non è richiesto alcun setup aggiuntivo per gli studenti, che possono clonare il repository ed eseguire i test immediatamente. L'assenza di dipendenze esterne semplifica la curva di apprendimento iniziale. Gli errori legati ai vincoli di database possono essere visibili e gestiti esplicitamente nel codice, costituendo un importante valore didattico. Infine, i tempi di esecuzione estremamente rapidi permettono agli studenti di vedere i risultati dei test immediatamente.

Per ambienti di testing pre-produzione o per progetti che richiedono una validazione più approfondita, è consigliabile passare a SQLite o a un database MySQL dedicato. Questa transizione può avvenire in un secondo momento, quando gli studenti hanno acquisito familiarità con i concetti fondamentali del testing.

### 4.8 Approccio Alternativo: Utilizzo di Moq per il Mock del Database

Il progetto FilmAPI utilizza un approccio diretto iniettando un `FilmDbContext` configurato con InMemory nei test. Esiste tuttavia un approccio alternativo che utilizza la libreria **Moq** per creare mock delle dipendenze, in particolare del `DbContext` o delle interfacce dei repository.

#### In cosa Consiste il Mocking con Moq

Il mocking è una tecnica che permette di creare oggetti simulati che replicano il comportamento delle dipendenze reali. Invece di utilizzare un database InMemory reale, si crea un mock del `DbContext` che risponde alle chiamate con dati di test predefiniti.

```csharp
using Moq;
using Microsoft.EntityFrameworkCore;

// Crea un mock del DbContext
var mockContext = new Mock<FilmDbContext>(new DbContextOptions<FilmDbContext>());

// Configura il mock per restituire dati quando viene chiamato Registi
var mockDbSet = new Mock<DbSet<Regista>>();
mockDbSet.Setup(m => m.ToListAsync(It.IsAny<CancellationToken>()))
    .ReturnsAsync(new List<Regista> { new Regista { Id = 1, Nome = "Nolan" } });

mockContext.Setup(c => c.Registi).Returns(mockDbSet.Object);

// Inietta il mock nel service
var service = new RegistaService(mockContext.Object);
```

#### Vantaggi del Mocking con Moq

Il primo vantaggio riguarda l'isolamento completo. Il test non dipende affatto dal database, nemmeno InMemory. Questo significa che è possibile testare la logica di business senza preoccuparsi di come i dati vengono persistiti.

Il secondo vantaggio riguarda il controllo preciso. Il test può controllare esattamente quali dati vengono restituiti e come vengono chiamati i metodi, permettendo di verificare che il service chiami i metodi corretti.

Il terzo vantaggio riguarda la velocità. Poiché non viene utilizzato nessun database (nemmeno InMemory), i test possono essere ancora più veloci.

#### Svantaggi del Mocking con Moq

Il primo svantaggio riguarda la complessità. Configurare i mock per comportamenti complessi (query LINQ, relazioni tra entità, lazy loading) può diventare laborioso.

Il secondo svantaggio riguarda la manutenzione. Se l'interfaccia del DbContext cambia, i mock devono essere aggiornati.

Il terzo svantaggio riguarda il rischio di testare il mock invece del codice reale. È facile cadere nella trappola di configurare i mock in modo che il test passi sempre, senza verificare il comportamento reale.

#### Quando Usare InMemory e Quando Usare Moq

Per progetti didattici come FilmAPI, dove l'obiettivo è comprendere il funzionamento della logica di business e dove la semplicità è prioritaria, l'approccio InMemory diretto è consigliato. Non richiede configurazione complessa, gli studenti possono vedere i dati nel database e il passaggio a database reali (SQLite, MySQL) è più naturale.

Per progetti più grandi o professionali, dove è necessario testare scenari complessi con molti stati, dove si vuole evitare qualsiasi dipendenza dal database, o dove si utilizzano pattern come Repository con interfacce specifiche, Moq può essere preferibile.

#### Esempio Con Interfaccia Repository

Un pattern comune è definire interfacce repository che vengono poi mockate con Moq:

```csharp
// Interfaccia repository
public interface IRegistaRepository
{
    Task<List<Regista>> GetAllAsync();
    Task<Regista?> GetByIdAsync(int id);
    Task<Regista> AddAsync(Regista regista);
}

// Service che usa l'interfaccia
public class RegistaService
{
    private readonly IRegistaRepository _repository;
    
    public RegistaService(IRegistaRepository repository)
    {
        _repository = repository;
    }
}

// Test con Moq
public class RegistaServiceTests
{
    private readonly Mock<IRegistaRepository> _mockRepo;
    private readonly RegistaService _service;

    public RegistaServiceTests()
    {
        _mockRepo = new Mock<IRegistaRepository>();
        _service = new RegistaService(_mockRepo.Object);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsRegisti()
    {
        // Arrange
        var expectedRegisti = new List<Regista> 
        { 
            new Regista { Id = 1, Nome = "Nolan" } 
        };
        
        _mockRepo.Setup(r => r.GetAllAsync())
            .ReturnsAsync(expectedRegisti);

        // Act
        var result = await _service.GetAllAsync();

        // Assert
        result.Should().HaveCount(1);
    }
}
```

---

## 5. Introduzione dei Servizi e Dependency Injection

### 5.1 L'Architettura Originale e le Sue Limitazioni

Nella versione iniziale del progetto FilmAPI, la logica di business era implementata direttamente all'interno degli endpoint HTTP. Ogni endpoint conteneva la logica per leggere i dati dal database, eseguire le operazioni richieste e restituire il risultato. Questa architettura, sebbene semplice per applicazioni molto piccole, presentava diversi problemi che sono emersi chiaramente durante l'implementazione dei test.

Il primo problema riguardava la testabilità. Per testare la logica di business era necessario simulare l'intera richiesta HTTP,Including la serializzazione JSON, il routing e la validazione dei dati in ingresso. Questo rendeva i test complessi da scrivere e fragili, poiché piccole modifiche alla struttura della richiesta potevano far fallire test non correlati alla modifica stessa.

Il secondo problema riguardava il riutilizzo del codice. Se la stessa logica di business doveva essere utilizzata da endpoint diversi (ad esempio, calcolare il numero di film di un regista per la pagina di dettaglio del regista e per una statistica generale), era necessario duplicare il codice in entrambi gli endpoint, con il conseguente rischio di incoerenze.

Il terzo problema riguardava la separation of concerns. Gli endpoint HTTP dovrebbero occuparsi principalmente di gestire la comunicazione tra client e server (parsing delle richieste, autenticazione, selezione del formato di risposta), mentre la logica di business dovrebbe risiedere in un layer separato.

### 5.2 Introduzione del Layer Servizi

Per risolvere questi problemi, il progetto FilmAPI è stato ristrutturato introducendo un layer di servizi. Ogni entità del dominio (Regista, Film, Cinema, Proiezione) ora ha un service dedicato che incapsula la logica di business:

```
Endpoints/
├── RegistiEndpoints.cs     # Gestisce le richieste HTTP
├── FilmsEndpoints.cs
├── CinemasEndpoints.cs
└── ProiezioniEndpoints.cs

Services/
├── IRegistaService.cs      # Interfaccia del servizio
├── RegistaService.cs       # Implementazione della logica
├── IFilmService.cs
├── FilmService.cs
└── ...
```

Ogni service espone metodi che corrispondono alle operazioni di business necessarie. Ad esempio, `RegistaService` espone i seguenti metodi:

- `GetAllAsync()`: Recupera tutti i registi
- `GetByIdAsync(int id)`: Recupera un regista per ID
- `CreateAsync(RegistaCreateDTO dto)`: Crea un nuovo regista
- `UpdateAsync(int id, RegistaUpdateDTO dto)`: Aggiorna un regista esistente
- `DeleteAsync(int id)`: Elimina un regista
- `GetFilmsByRegistaIdAsync(int registaId)`: Recupera i film di un regista

### 5.3 Dependency Injection e Inversion of Control

Il pattern Dependency Injection (DI) è un'implementazione del principio più ampio Inversion of Control (IoC). Invece che una classe crei direttamente le proprie dipendenze, queste vengono fornite dall'esterno (tipicamente da un container DI). Questo approccio offre numerosi vantaggi:

Il primo vantaggio riguarda la testabilità. Poiché le dipendenze vengono fornite dall'esterno, è possibile sostituirle con implementazioni mock durante i test. Ad esempio, nel test unitario è possibile iniettare un database InMemory senza che il service sia a conoscenza di questa sostituzione.

Il secondo vantaggio riguarda il disaccoppiamento. Le classi non dipendono più da implementazioni concrete ma da astrazioni (interfacce). Questo permette di modificare l'implementazione di una dipendenza senza dover modificare la classe che la utilizza.

Il terzo vantaggio riguarda la configurazione centralizzata. Il container DI gestisce il ciclo di vita di tutti i servizi, facilitando la configurazione e la manutenzione dell'applicazione.

### 5.4 Registrazione dei Servizi in ASP.NET Core

In ASP.NET Core, i servizi vengono registrati nel container DI nel metodo `ConfigureServices` del file `Program.cs`:

```csharp
// Registrazione dei servizi nel container DI
builder.Services.AddScoped<IRegistaService, RegistaService>();
builder.Services.AddScoped<IFilmService, FilmService>();
builder.Services.AddScoped<ICinemaService, CinemaService>();
builder.Services.AddScoped<IProiezioneService, ProiezioneService>();

// Registrazione del DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<FilmDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
```

La riga `builder.Services.AddScoped<IRegistaService, RegistaService>()` indica al container DI che quando un componente richiede un'istanza di `IRegistaService`, deve creare e fornire un'istanza di `RegistaService`. Il lifetime `Scoped` indica che l'istanza viene creata una volta per ogni richiesta HTTP.

### 5.5 Utilizzo dei Servizi negli Endpoint

Gli endpoint ottengono le istanze dei servizi tramite dependency injection nel costruttore:

```csharp
public class RegistiEndpoints
{
    private readonly IRegistaService _registaService;

    // Il service viene iniettato automaticamente da ASP.NET Core
    public RegistiEndpoints(IRegistaService registaService)
    {
        _registaService = registaService;
    }

    public async Task<IResult> GetAll()
    {
        var registi = await _registaService.GetAllAsync();
        return Results.Ok(registi);
    }

    public async Task<IResult> GetById(int id)
    {
        var regista = await _registaService.GetByIdAsync(id);
        if (regista is null)
            return Results.NotFound();
        
        return Results.Ok(regista);
    }
}
```

### 5.6 Vantaggi per i Test

L'introduzione del layer servizi con dependency injection ha semplificato drasticamente la scrittura dei test:

Nei test unitari, è possibile creare direttamente un'istanza del service iniettando un `FilmDbContext` configurato con database InMemory. Il test può quindi chiamare i metodi del service direttamente, senza dover simulare l'intera pipeline HTTP.

Nei test di integrazione, la `CustomWebApplicationFactory` può sostituire le registrazioni dei servizi con versioni che utilizzano il database InMemory, permettendo di testare gli endpoint completi con un database isolato.

---

## 6. Comandi Utili

### 6.1 Comandi per l'Esecuzione dei Test

```bash
# Esegue tutti i test
dotnet test

# Esegue solo i test unitari
dotnet test --filter "FullyQualifiedName~Unit"

# Esegue solo i test di integrazione
dotnet test --filter "FullyQualifiedName~Integration"

# Esegue un test specifico
dotnet test --filter "FullyQualifiedName~RegistaServiceTests.U_R1"

# Esegue i test con coverage
dotnet test --collect:"XPlat Code Coverage"

# Esegue i test in parallelo
dotnet test --parallel

# Esegue i test senza ricompilare
dotnet test --no-build

# Mostra output dettagliato
dotnet test --verbosity detailed
```

### 6.2 Comandi per la Build

```bash
# Compila l'intera soluzione
dotnet build

# Compila il progetto principale
dotnet build FilmAPI.csproj

# Compila il progetto di test
dotnet build tests/FilmAPI.Tests.csproj

# Compila in Release mode
dotnet build -c Release
```

### 6.3 Comandi per la Gestione dei Pacchetti

```bash
# Ripristina le dipendenze
dotnet restore

# Aggiunge un pacchetto NuGet
dotnet add package FluentAssertions

# Aggiunge un pacchetto a un progetto specifico
dotnet add tests/FilmAPI.Tests.csproj package Microsoft.EntityFrameworkCore.InMemory
```

---

## Riepilogo

Il progetto FilmAPI adotta un approccio strutturato al testing che comprende test unitari e test di integrazione. I test unitari verificano la logica di business isolatamente, utilizzando database InMemory per garantire velocità e isolamento. I test di integrazione verificano il corretto funzionamento degli endpoint HTTP, simulando chiamate HTTP reali con l'intera applicazione in-memory.

L'introduzione del layer servizi con dependency injection ha migliorato significativamente la testabilità del codice, permettendo di testare la logica di business in isolamento senza dover simulare l'intera pipeline HTTP. Il doppio stack di database (InMemory per i test, MySQL per la produzione) è gestito attraverso la configurazione del DbContext, che utilizza provider diversi in base all'ambiente di esecuzione.

---

**Documento creato il:** 16 Marzo 2026
**Versione:** 1.0
**Progetto:** FilmAPI - Guida Introduttiva al Testing
