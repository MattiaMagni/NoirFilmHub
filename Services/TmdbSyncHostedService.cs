using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FilmAPI.Services;

public class TmdbSyncHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TmdbSyncHostedService> _logger;

    public TmdbSyncHostedService(IServiceProvider serviceProvider, ILogger<TmdbSyncHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = (Environment.GetEnvironmentVariable("TMDB_SYNC_ENABLED") ?? "true")
            .Equals("true", StringComparison.OrdinalIgnoreCase);
        if (!enabled)
        {
            _logger.LogInformation("TMDB sync notturna disabilitata");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delay = ComputeDelay();
                await Task.Delay(delay, stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var tmdb = scope.ServiceProvider.GetRequiredService<TmdbService>();
                if (!tmdb.IsConfigured())
                {
                    _logger.LogWarning("TMDB sync saltata: token non configurato");
                    continue;
                }

                var result = await tmdb.SyncMissingAsync();
                _logger.LogInformation("TMDB sync notturna completata: success={Success}, failed={Failed}", result.Success, result.Failed);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la TMDB sync notturna");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private static TimeSpan ComputeDelay()
    {
        var configuredHourRaw = Environment.GetEnvironmentVariable("TMDB_SYNC_HOUR") ?? "3";
        var configuredHour = int.TryParse(configuredHourRaw, out var hour) ? Math.Clamp(hour, 0, 23) : 3;
        var now = DateTime.Now;
        var next = new DateTime(now.Year, now.Month, now.Day, configuredHour, 0, 0, now.Kind);
        if (next <= now)
        {
            next = next.AddDays(1);
        }

        return next - now;
    }
}
