# AI Testing Quick Guide (Short)

## Goal

Generate and run FilmAPI tests (both unit and integration) without breaking project setup.

## Quick Reference

### Test Types

| Type | What | Tools | Speed |
|------|------|-------|-------|
| **Unit** | Service logic | InMemory (EF Core) | ~10ms |
| **Integration** | HTTP endpoints | WebApplicationFactory + HttpClient + InMemory | ~100-500ms |

## Do This

### 1. Project Setup
- Test project: `tests/FilmAPI.Tests.csproj` on `net9.0`
- Structure: `Unit/` + `Integration/` folders

### 2. Required Packages

```xml
<PackageReference Include="xunit" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
<PackageReference Include="Moq" />
<PackageReference Include="FluentAssertions" />
```

### 3. API Project Requirements

- Add `public partial class Program;` at end of `Program.cs`
- Use env vars for DB:
  - `DB_USE_AUTODETECT` (default `true`)
  - `DB_SERVER_VERSION` (default `10.11.0-mariadb`)

### 4. Integration Test Factory

```csharp
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";

    public CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("DB_USE_AUTODETECT", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptors = services.Where(d => 
                d.ServiceType == typeof(DbContextOptions<FilmDbContext>) ||
                d.ServiceType == typeof(FilmDbContext)).ToList();
            
            foreach (var descriptor in descriptors)
                services.Remove(descriptor);

            services.AddDbContext<FilmDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }

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

### 5. Exclude Tests from API Project

In `FilmAPI.csproj`:

```xml
<ItemGroup>
  <Compile Remove="tests\**\*.cs" />
  <Content Remove="tests\**\*" />
  <None Remove="tests\**\*" />
</ItemGroup>
```

### 6. Unit Test Example (InMemory, not Moq)

```csharp
public class RegistaServiceTests : IAsyncLifetime
{
    private readonly IServiceProvider _sp;
    private readonly IRegistaService _service;

    public RegistaServiceTests()
    {
        var services = new ServiceCollection();
        var options = new DbContextOptionsBuilder<FilmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        
        var context = new FilmDbContext(options);
        services.AddScoped(_ => context);
        services.AddScoped<IRegistaService, RegistaService>();
        
        _sp = services.BuildServiceProvider();
        _service = _sp.GetRequiredService<IRegistaService>();
    }

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesRegista()
    {
        var dto = new RegistaCreateDTO { Nome = "Nolan", Cognome = "Christopher", Nazionalita = "UK" };
        var result = await _service.CreateAsync(dto);
        result.Nome.Should().Be("Nolan");
    }
}
```

## Final Checks

```bash
# Unit tests only
dotnet test --filter "FullyQualifiedName~Unit"

# Integration tests only  
dotnet test --filter "FullyQualifiedName~Integration"

# All tests
dotnet test tests/FilmAPI.Tests.csproj
```

Both must pass (66 total).
