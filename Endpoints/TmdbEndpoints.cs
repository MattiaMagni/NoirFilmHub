using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Endpoints;

public static class TmdbEndpoints
{
    public static RouteGroupBuilder MapTmdb(this RouteGroupBuilder group)
    {
        group.MapGet("/status", (TmdbService service) =>
        {
            return Results.Ok(new
            {
                configured = service.IsConfigured(),
                language = Environment.GetEnvironmentVariable("TMDB_LANGUAGE") ?? "it-IT",
                fallbackLanguage = Environment.GetEnvironmentVariable("TMDB_FALLBACK_LANGUAGE") ?? "en-US",
                nightlyEnabled = (Environment.GetEnvironmentVariable("TMDB_SYNC_ENABLED") ?? "true")
            });
        }).RequireAuthorization("AdminOrPowerUser");

        group.MapPost("/sync/film/{filmId:int}", async (int filmId, TmdbService service) =>
        {
            var result = await service.SyncFilmAsync(filmId);
            if (!result.Success)
            {
                return Results.BadRequest(new { error = result.Message });
            }

            return Results.Ok(new { message = result.Message });
        }).RequireAuthorization("AdminOrPowerUser");

        group.MapPost("/sync/films", async (TmdbService service) =>
        {
            if (!service.IsConfigured())
            {
                return Results.BadRequest(new { error = "TMDB non configurato" });
            }

            var result = await service.SyncMissingAsync();
            return Results.Ok(new { result.Success, result.Failed });
        }).RequireAuthorization("AdminOrPowerUser");

        group.MapGet("/latest", async (TmdbService service, int? limit, int? page) =>
        {
            if (!service.IsConfigured())
            {
                return Results.BadRequest(new { error = "TMDB non configurato" });
            }

            var items = await service.GetLatestReleasesAsync(limit ?? 20, page ?? 1);
            return Results.Ok(new
            {
                limit = Math.Clamp(limit ?? 20, 1, 50),
                page = Math.Max(1, page ?? 1),
                count = items.Count,
                items
            });
        }).RequireAuthorization("AdminOrPowerUser");

        group.MapGet("/search", async (TmdbService service, string? title, int? limit, int? page) =>
        {
            if (!service.IsConfigured())
            {
                return Results.BadRequest(new { error = "TMDB non configurato" });
            }

            var query = (title ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                return Results.BadRequest(new { error = "Titolo di ricerca obbligatorio" });
            }

            var items = await service.SearchMoviesByTitleAsync(query, limit ?? 20, page ?? 1);
            return Results.Ok(new
            {
                title = query,
                limit = Math.Clamp(limit ?? 20, 1, 50),
                page = Math.Max(1, page ?? 1),
                count = items.Count,
                items
            });
        }).RequireAuthorization("AdminOrPowerUser");

        group.MapPost("/import-latest", async (TmdbImportRequestDTO dto, TmdbService service) =>
        {
            if (!service.IsConfigured())
            {
                return Results.BadRequest(new { error = "TMDB non configurato" });
            }

            var ids = dto.TmdbMovieIds ?? [];
            if (ids.Count == 0)
            {
                return Results.BadRequest(new { error = "Nessun film selezionato" });
            }

            var result = await service.ImportMoviesAsync(ids);
            return Results.Ok(new
            {
                created = result.Created,
                skippedExisting = result.SkippedExisting,
                failed = result.Failed,
                createdIds = result.CreatedFilmIds
            });
        }).RequireAuthorization("AdminOrPowerUser");

        group.MapGet("/missing", async (FilmDbContext db) =>
        {
            var missing = await db.Films
                .AsNoTracking()
                .Where(f => f.TmdbMovieId == null || string.IsNullOrWhiteSpace(f.DescrizioneLunga) || string.IsNullOrWhiteSpace(f.CastPrincipale))
                .Select(f => new { f.Id, f.Titolo, f.TmdbMovieId, f.TmdbSyncStato, f.UltimaSyncTmdbUtc })
                .OrderBy(f => f.Titolo)
                .ToListAsync();

            return Results.Ok(missing);
        }).RequireAuthorization("AdminOrPowerUser");

        return group;
    }
}
