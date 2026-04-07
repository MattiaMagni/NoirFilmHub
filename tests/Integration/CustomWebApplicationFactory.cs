using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FilmAPI.Data;
using Xunit;

namespace FilmAPI.Tests.Integration;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";

    public CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("DB_USE_AUTODETECT", "false");
        Environment.SetEnvironmentVariable("DB_SERVER_VERSION", "10.11.0-mariadb");
        Environment.SetEnvironmentVariable("DB_PROVIDER", "InMemory");
        Environment.SetEnvironmentVariable("TEST_DB_NAME", _dbName);
        Environment.SetEnvironmentVariable("AUTH_ENABLED", "false");
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
