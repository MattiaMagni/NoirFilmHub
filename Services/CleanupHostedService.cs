using FilmAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Services;

public class CleanupHostedService : IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private Timer? _timer;

    public CleanupHostedService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(async _ => await DoCleanup(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
        return Task.CompletedTask;
    }

    private async Task DoCleanup()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FilmDbContext>();
            var now = DateTime.UtcNow;

            await db.ExternalAuthStates
                .Where(s => s.ExpiresAtUtc < now)
                .ExecuteDeleteAsync();

            await db.AccountActionTokens
                .Where(t => t.ExpiresAtUtc < now.AddHours(-1) && t.ConsumedAtUtc != null)
                .ExecuteDeleteAsync();

            await db.AccountActionTokens
                .Where(t => t.ExpiresAtUtc < now.AddDays(-1))
                .ExecuteDeleteAsync();
        }
        catch
        {
            // best-effort cleanup
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
