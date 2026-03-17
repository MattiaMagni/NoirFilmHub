# Guida ai Test Unitari - xUnit Framework

**Autore:** Claude AI Assistant
**Data:** 10 Marzo 2026
**Progetto di Riferimento:** FilmAPI
**Framework:** xUnit 2.9.2
**Linguaggio:** C# / .NET 9.0

---

## Indice
1. [Introduzione ai Test Unitari](#1-introduzione-ai-test-unitari)
2. [Il Framework xUnit](#2-il-framework-xunit)
3. [Setup dell'Ambiente di Testing](#3-setup-dellambiente-di-testing)
4. [Il Pattern Arrange-Act-Assert](#4-il-pattern-arrange-act-assert)
5. [Librerie di Supporto](#5-librerie-di-supporto)
6. [Esempi Pratici dal Progetto FilmAPI](#6-esempi-pratici-dal-progetto-FilmAPI)
7. [Best Practices](#7-best-practices)

---

## 1. Introduzione ai Test Unitari

### 1.1 Cosa sono i Test Unitari?

I **test unitari** (o unit test) sono test automatici che verificano il funzionamento di una singola unità di codice, tipicamente un **metodo** o una **classe**, in isolamento dalle sue dipendenze esterne.

```
┌─────────────────────────────────────────────────────────┐
│                    Applicazione                          │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌─────────┐ │
│  │   UI     │  │  API     │  │ Services │  │  Data   │ │
│  └──────────┘  └──────────┘  └──────────┘  └─────────┘ │
│                                              ▲          │
│                                              │          │
│                              ┌───────────────┘          │
│                              │                          │
│                         ┌────▼────┐                     │
│                         │ Unit    │                     │
│                         │  Test   │                     │
│                         └─────────┘                     │
└─────────────────────────────────────────────────────────┘
```

### 1.2 Perché scrivere Test Unitari?

| Vantaggio | Descrizione |
|-----------|-------------|
| **Early Bug Detection** | Trova bug prima che il codice vada in produzione |
| **Refactoring Sicuro** | Modifica il codice con la certezza di non rompere funzionalità esistenti |
| **Documentazione Viva** | I test documentano il comportamento atteso del codice |
| **Design Migliore** | Codice testabile è spesso meglio progettato (più modulare) |
| **Sviluppo Guidato dal Test (TDD)** | Scrivi prima il test, poi il codice |

### 1.3 Unit Test vs Altri Tipi di Test

```
┌────────────────────────────────────────────────────────────────────┐
│                       Piramide dei Test                           │
│                                                              /    │
│                    End-to-End Tests                          /      │
│                                  \            Integration    /       │
│                                   \           Tests      /        │
│                                    \                    /         │
│                              Unit Tests \                /          │
│                                         \              /           │
│                                          \____________/            │
└────────────────────────────────────────────────────────────────────┘
  • Unit Tests: Veloci, molti, testano singole funzioni
  • Integration Tests: Più lenti, testano interazioni tra componenti
  • E2E Tests: Lenti, pochi, testano l'intera applicazione
```

---

## 2. Il Framework xUnit

### 2.1 Cos'è xUnit?

**xUnit** è un framework di testing **free**, **open source** e **comunità-driven** per .NET. Il nome deriva dal concetto di testare unità di codice (x = qualsiasi tipo di test).

**Caratteristiche principali:**
- ✅ Integrato perfettamente con .NET
- ✅ Supporto per .NET 9.0
- ✅ Runner per Visual Studio, VS Code, CLI
- ✅ Parallel execution dei test
- ✅ Estensibile con custom assertions

### 2.2 Confronto con altri Framework

| Framework | DiffKey | Note |
|-----------|---------|------|
| **xUnit** | `[Fact]` | Più moderno, preferito per nuovi progetti |
| **NUnit** | `[Test]` | Più vecchio, molto diffuso |
| **MSTest** | `[TestMethod]` | Framework Microsoft tradizionale |

### 2.3 Struttura di un Test xUnit

```csharp
using Xunit;  // Importa xUnit
using FluentAssertions;  // Per assertion più leggibili

namespace FilmAPI.Tests.Unit;

public class RegistaServiceTests  // La classe di test
{
    [Fact]  // Attributo che marca il metodo come test
    public async Task MetodoDaTestare_Scenario_CosaSiAspetta()
    {
        // Arrange: Prepara il test
        // Act: Esegui l'azione
        // Assert: Verifica il risultato
    }
}
```

**Anatomia di un Test:**

```
┌─────────────────────────────────────────────────────────────────┐
│  [Fact]                                                         │
│  public async Task MetodoDaTestare_Scenario_CosaSiAspetta()    │
│  │           │          │        │         │                  │
│  │           │          │        │         └─ Descrizione del │
│  │           │          │        │            risultato atteso│
│  │           │          │        └─────────────────────────   │
│  │           │          └─ Condizione di test                │
│  │           └─ Metodo sotto test                            │
│  └─ Attributo xUnit (marker)                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2.4 [Fact] vs [Theory]

```csharp
// [Fact]: Test singolo, eseguito una volta
[Fact]
public async Task GetAllAsync_WhenNoRegistiExist_ReturnsEmptyList()
{
    // Test con dati specifici
    var result = await _service.GetAllAsync();
    result.Should().BeEmpty();
}

// [Theory]: Test parametrico, eseguito più volte con dati diversi
[Theory]
[InlineData(1, true)]
[InlineData(2, true)]
[InlineData(99, false)]
public async Task GetByIdAsync_WithDifferentIds_ReturnsCorrectly(int id, bool shouldExist)
{
    // Test eseguito 3 volte con dati diversi
    var result = await _service.GetByIdAsync(id);
    (result != null).Should().Be(shouldExist);
}
```

---

## 3. Setup dell'Ambiente di Testing

### 3.1 Creare un Progetto di Test

```bash
# Crea un nuovo progetto xUnit
dotnet new xunit -n FilmAPI.Tests

# Aggiungi riferimento al progetto principale
dotnet add FilmAPI.Tests/FilmAPI.Tests.csproj reference FilmAPI/FilmAPI.csproj

# Aggiungi pacchetti NuGet necessari
dotnet add FilmAPI.Tests/FilmAPI.Tests.csproj package FluentAssertions
dotnet add FilmAPI.Tests/FilmAPI.Tests.csproj package Moq
dotnet add FilmAPI.Tests/FilmAPI.Tests.csproj package Microsoft.EntityFrameworkCore.InMemory
```

### 3.2 Struttura del Progetto di Test

```
FilmAPI.Tests/
├── Unit/                           # Test unitari
│   ├── RegistaServiceTests.cs
│   └── FilmServiceTests.cs
├── Integration/                    # Test di integrazione
│   ├── RegistaEndpointsTests.cs
│   └── ProiezioneEndpointsTests.cs
├── Helpers/                        # Classi helper
│   └── DatabaseFixture.cs
└── FilmAPI.Tests.csproj          # File di progetto
```

### 3.3 File di Progetto (.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <!-- Pacchetti per testing -->
  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.2" />
    <PackageReference Include="FluentAssertions" Version="8.8.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="9.0.11" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <!-- Riferimento al progetto principale -->
  <ItemGroup>
    <ProjectReference Include="..\FilmAPI\FilmAPI.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

---

## 4. Il Pattern Arrange-Act-Assert

### 4.1 Cos'è AAA?

Il pattern **Arrange-Act-Assert** (AAA) è una struttura standard per scrivere test chiari e leggibili.

```csharp
[Fact]
public async Task CreateAsync_WithValidData_CreatesRegista()
{
    // ═══════════════════════════════════════════════════════
    // ARRANGE: Prepara tutto ciò che serve per il test
    // ═══════════════════════════════════════════════════════
    var dto = new CreateRegistaDTO("Quentin", "Tarantino", "Americana");

    // ═══════════════════════════════════════════════════════
    // ACT: Esegui l'azione che vuoi testare
    // ═══════════════════════════════════════════════════════
    var result = await _service.CreateAsync(dto);

    // ═══════════════════════════════════════════════════════
    // ASSERT: Verifica che il risultato sia quello atteso
    // ═══════════════════════════════════════════════════════
    result.Cognome.Should().Be("Tarantino");
    result.Nome.Should().Be("Quentin");
}
```

### 4.2 Visualizzazione del Pattern

```
┌────────────────────────────────────────────────────────────────┐
│                    TEST LIFECYCLE                             │
│                                                                │
│  ╔═════════════════════════════════════════════════════════╗ │
│  ║              ARRANGE (Preparazione)                       ║ │
│  ║  • Crea oggetti mock                                       ║ │
│  ║  • Inizializza variabili                                   ║ │
│  ║  • Prepara il contesto                                     ║ │
│  ╚════════════════════════════╤═══════════════════════════════╝ │
│                             │                                  │
│                             ▼                                  │
│  ╔═════════════════════════════════════════════════════════╗ │
│  ║                 ACT (Azione)                             ║ │
│  ║  • Chiama il metodo sotto test                            ║ │
│  ║  • Esegue l'operazione                                     ║ │
│  ╚════════════════════════════╤═══════════════════════════════╝ │
│                             │                                  │
│                             ▼                                  │
│  ╔═════════════════════════════════════════════════════════╗ │
│  ║               ASSERT (Verifica)                          ║ │
│  ║  • Verifica il risultato                                  ║ │
│  ║  • Confronta con il valore atteso                         ║ │
│  ║  • Lancia eccezione se fallisce                            ║ │
│  ╚══════════════════════════════════════════════════════════╝ │
└────────────────────────────────────────────────────────────────┘
```

---

## 5. Librerie di Supporto

### 5.1 FluentAssertions - Assertion Leggibili

**FluentAssertions** è una libreria che rende le assertion più leggibili e espressive.

```csharp
// Senza FluentAssertions (assertion standard xUnit)
Assert.Equal("Tarantino", result.Cognome);
Assert.NotNull(result);
Assert.True(result.Id > 0);

// Con FluentAssertions - Molto più leggibile!
result.Cognome.Should().Be("Tarantino");
result.Should().NotBeNull();
result.Id.Should().BeGreaterThan(0);
```

**Principali Metodi di FluentAssertions:**

| Metodo | Descrizione | Esempio |
|--------|-------------|---------|
| `Be()` | Verifica uguaglianza | `result.Should().Be(expected)` |
| `NotBeNull()` | Verifica non null | `result.Should().NotBeNull()` |
| `BeEmpty()` | Verifica che la collezione sia vuota | `list.Should().BeEmpty()` |
| `HaveCount()` | Verifica numero elementi | `list.Should().HaveCount(5)` |
| `ContainSingle()` | Verifica che ci sia esattamente uno che soddisfa una condizione | `list.Should().ContainSingle(x => x.Id == 1)` |
| `BeEquivalentTo()` | Confronta oggetti per proprietà | `result.Should().BeEquivalentTo(expected)` |
| `ThrowAsync()` | Verifica che venga lanciata un'eccezione | `await act.Should().ThrowAsync<ArgumentException>()` |

### 5.2 Moq - Mocking delle Dipendenze

**Moq** è una libreria per creare **mock** (oggetti simulati) delle dipendenze.

```csharp
using Moq;

// Crea un mock di ILogger<T>
var loggerMock = new Mock<ILogger<RegistaService>>();

// Il logger mock non fa nulla di default
var service = new RegistaService(context, loggerMock.Object);

// .Object ottiene l'istanza mockata da passare al servizio
```

**Quando usare Moq?**

- Quando una classe ha dipendenze che non vuoi usare nel test
- Per isolare il codice sotto test
- Per evitare effetti collaterali (es. database reale)

### 5.3 Entity Framework InMemory Database

**InMemory Database** simula un database EF Core in memoria.

```csharp
var options = new DbContextOptionsBuilder<FilmDbContext>()
    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
    .Options;

var context = new FilmDbContext(options);
// Ora puoi usare context come un vero database!
```

**Vantaggi:**
- ✅ Molto veloce (nessuna I/O reale)
- ✅ Isolato (ogni test ha il suo database)
- ✅ Non richiede setup di database reali

---

## 6. Esempi Pratici dal Progetto FilmAPI

### 6.1 Testare un Service - RegistaServiceTests

```csharp
using FluentAssertions;
using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Models;
using FilmAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FilmAPI.Tests.Unit;

public class RegistaServiceTests : IDisposable
{
    // Dipendenze comuni a tutti i test
    private readonly FilmDbContext _context;
    private readonly RegistaService _service;

    // Constructor: eseguito prima di OGNI test
    public RegistaServiceTests()
    {
        // Creo un database in memoria per questo test
        var options = new DbContextOptionsBuilder<FilmDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new FilmDbContext(options);

        // Mock del logger (non ci interessa per questi test)
        _service = new RegistaService(_context, Mock.Of<ILogger<RegistaService>>());
    }

    // Cleanup: eseguito dopo OGNI test
    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task GetAllAsync_WhenNoRegistiExist_ReturnsEmptyList()
    {
        // ACT: chiamata diretta al servizio
        var result = await _service.GetAllAsync();

        // ASSERT: verifica che la lista sia vuota
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WhenRegistiExist_ReturnsAllRegisti()
    {
        // ARRANGE: crea dati di test
        var regista1 = new Regista { Nome = "Mario", Cognome = "Rossi", Nazionalita = "Italiana" };
        var regista2 = new Regista { Nome = "Steven", Cognome = "Spielberg", Nazionalita = "Americana" };
        await _context.Registi.AddRangeAsync(regista1, regista2);
        await _context.SaveChangesAsync();

        // ACT
        var result = await _service.GetAllAsync();

        // ASSERT
        result.Should().HaveCount(2);
        result.Should().ContainSingle(r => r.Cognome == "Rossi");
        result.Should().ContainSingle(r => r.Cognome == "Spielberg");
    }

    [Fact]
    public async Task GetByIdAsync_WhenRegistaExists_ReturnsRegista()
    {
        // ARRANGE
        var regista = new Regista { Nome = "Mario", Cognome = "Rossi", Nazionalita = "Italiana" };
        await _context.Registi.AddAsync(regista);
        await _context.SaveChangesAsync();

        // ACT
        var result = await _service.GetByIdAsync(regista.Id);

        // ASSERT
        result.Should().NotBeNull();
        result!.Cognome.Should().Be("Rossi");
        result.Nome.Should().Be("Mario");
    }

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesRegista()
    {
        // ARRANGE
        var dto = new CreateRegistaDTO("Quentin", "Tarantino", "Americana");

        // ACT
        var result = await _service.CreateAsync(dto);

        // ASSERT: verifica il DTO restituito
        result.Cognome.Should().Be("Tarantino");
        result.Nome.Should().Be("Quentin");
        result.Nazionalita.Should().Be("Americana");

        // ASSERT: verifica che sia stato salvato nel database
        var saved = await _context.Registi.FindAsync(result.Id);
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_WhenRegistaExists_UpdatesRegista()
    {
        // ARRANGE
        var regista = new Regista { Nome = "Mario", Cognome = "Rossi", Nazionalita = "Italiana" };
        await _context.Registi.AddAsync(regista);
        await _context.SaveChangesAsync();

        var dto = new UpdateRegistaDTO("Marco", "Verdi", "Italiana");

        // ACT
        var result = await _service.UpdateAsync(regista.Id, dto);

        // ASSERT
        result.Should().NotBeNull();
        result!.Cognome.Should().Be("Verdi");
        result.Nome.Should().Be("Marco");
    }

    [Fact]
    public async Task DeleteAsync_WhenRegistaExists_DeletesRegista()
    {
        // ARRANGE
        var regista = new Regista { Nome = "Mario", Cognome = "Rossi", Nazionalita = "Italiana" };
        await _context.Registi.AddAsync(regista);
        await _context.SaveChangesAsync();

        // ACT
        var result = await _service.DeleteAsync(regista.Id);

        // ASSERT: verifica che l'operazione abbia successo
        result.Should().BeTrue();

        // ASSERT: verifica che il regista sia stato eliminato
        var deleted = await _context.Registi.FindAsync(regista.Id);
        deleted.Should().BeNull();
    }
}
```

### 6.2 Testare con Eccezioni - FilmServiceTests

```csharp
[Fact]
public async Task CreateAsync_WhenRegistaDoesNotExist_ThrowsArgumentException()
{
    // ARRANGE
    var dto = new CreateFilmDTO("Kill Bill", new DateTime(2003, 1, 1), 111, 999);
    //                                                        ^^^^
    //                                                        ID inesistente

    // ACT: crea un delegate per testare il codice async
    var act = async () => await _service.CreateAsync(dto);

    // ASSERT: verifica che venga lanciata ArgumentException
    await act.Should().ThrowAsync<ArgumentException>()
        .WithMessage("*Regista con ID 999 non trovato*");
}
```

### 6.3 Organizzare i Test per Categoria

```csharp
public class RegistaServiceTests
{
    // Test per GetAll
    [Fact]
    public async Task GetAllAsync_WhenNoRegistiExist_ReturnsEmptyList() { }

    [Fact]
    public async Task GetAllAsync_WhenRegistiExist_ReturnsAllRegisti() { }

    // Test per GetById
    [Fact]
    public async Task GetByIdAsync_WhenRegistaExists_ReturnsRegista() { }

    [Fact]
    public async Task GetByIdAsync_WhenRegistaNotFound_ReturnsNull() { }

    // Test per Create
    [Fact]
    public async Task CreateAsync_WithValidData_CreatesRegista() { }

    // Test per Update
    [Fact]
    public async Task UpdateAsync_WhenRegistaExists_UpdatesRegista() { }

    [Fact]
    public async Task UpdateAsync_WhenRegistaNotFound_ReturnsNull() { }

    // Test per Delete
    [Fact]
    public async Task DeleteAsync_WhenRegistaExists_DeletesRegista() { }

    [Fact]
    public async Task DeleteAsync_WhenRegistaNotFound_ReturnsFalse() { }

    // Test per metodi specifici
    [Fact]
    public async Task GetFilmsByRegistaIdAsync_WhenRegistaHasFilms_ReturnsFilms() { }

    [Fact]
    public async Task GetFilmsByRegistaIdAsync_WhenRegistaHasNoFilms_ReturnsEmptyList() { }
}
```

---

## 7. Best Practices

### 7.1 Convenzioni di Naming

```
MethodName_Scenario_ExpectedResult
    │          │         │
    │          │         └─ Cosa dovrebbe accadere
    │          └─ In quali circostanze
    └─ Il metodo che stai testando
```

**Esempi:**
- ✅ `GetAllAsync_WhenNoRegistiExist_ReturnsEmptyList`
- ✅ `CreateAsync_WithValidData_CreatesRegista`
- ✅ `DeleteAsync_WhenRegistaNotFound_ReturnsFalse`
- ❌ `Test1()`, `TestRegista()`, `Works()` → Troppo generici!

### 7.2 Principi AAA

| Regola | Descrizione |
|--------|-------------|
| **One Assert per Test** | Mantieni i test focalizzati su un singolo comportamento |
| **Arrange on Top** | Metti tutto il setup in cima, prima dell'Act |
| **Descriptive Names** | Il nome del test dovrebbe documentare cosa fa |
| **Test Independent** | Ogni test deve poter essere eseguito singolarmente |
| **Fast Tests** | I test unitari dovrebbero essere veloci (< 100ms) |

### 7.3 Cosa Testare

```csharp
// ✅ TESTA: Comportamento pubblico
public async Task CreateAsync_WithValidData_CreatesRegista() { }

// ✅ TESTA: Casi edge
public async Task GetByIdAsync_WhenRegistaNotFound_ReturnsNull() { }

// ✅ TESTA: Gestione errori
public async Task CreateAsync_WhenRegistaDoesNotExist_ThrowsArgumentException() { }

// ❌ NON TESTARE: Dettagli implementativi
public void Constructor_SetsPropertiesCorrectly() { } // Implementazione detail
```

### 7.4 Eseguire i Test

```bash
# Esegui tutti i test
dotnet test

# Esegui solo test unitari
dotnet test --filter "FullyQualifiedName~Unit"

# Esegui con output dettagliato
dotnet test --logger "console;verbosity=detailed"

# Esegui un test specifico
dotnet test --filter "FullyQualifiedName~CreateAsync_WithValidData"
```

---

## Riepilogo

### Concetti Chiave

| Concetto | Descrizione |
|----------|-------------|
| **Unit Test** | Test di un singolo metodo/classe in isolamento |
| **xUnit** | Framework di testing per .NET |
| **[Fact]** | Attributo per marcare un metodo come test |
| **AAA Pattern** | Arrange-Act-Assert per organizzare i test |
| **FluentAssertions** | Libreria per assertion leggibili |
| **Moq** | Libreria per creare mock delle dipendenze |
| **InMemory DB** | Database simulato per velocizzare i test |

### Prossimi Passi

1. **Scrivi il tuo primo test** → Copia un test esistente e modificalo
2. **Esegui i test regolarmente** → Ogni volta che modifichi il codice
3. **Aggiungi test per nuovi metodi** → Man mano che aggiungi funzionalità
4. **Mantieni i test aggiornati** → Quando cambi il comportamento, aggiorna anche i test

---

**Documento creato il:** 10 Marzo 2026
**Versione:** 1.0
**Progetto:** FilmAPI - Tutorial Test Unitari
