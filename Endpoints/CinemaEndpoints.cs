using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Helpers;
using FilmAPI.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Endpoints;

public static class CinemaEndpoints
{
    public static RouteGroupBuilder MapCinemas(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (FilmDbContext db) =>
        {
            var items = await db.Cinemas.AsNoTracking().OrderBy(c => c.Citta).ThenBy(c => c.Nome).ToListAsync();
            return Results.Ok(items);
        }).AllowAnonymous();

        group.MapGet("/nearby", async (FilmDbContext db, double lat, double lng) =>
        {
            var all = await db.Cinemas
                .AsNoTracking()
                .Where(c => c.Latitudine.HasValue && c.Longitudine.HasValue)
                .ToListAsync();

            var cinemas = all
                .Select(c => new
                {
                    c.Id,
                    c.Nome,
                    c.Citta,
                    c.Indirizzo,
                    c.Latitudine,
                    c.Longitudine,
                    DistanzaKm = GeoHelper.DistanceKm(lat, lng, c.Latitudine!.Value, c.Longitudine!.Value)
                })
                .OrderBy(c => c.DistanzaKm)
                .Take(50)
                .ToList();

            return Results.Ok(cinemas);
        }).AllowAnonymous();

        group.MapGet("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var c = await db.Cinemas.FindAsync(id);
            return c is not null ? Results.Ok(c) : Results.NotFound();
        }).AllowAnonymous();

        group.MapPost("/", async (CinemaDTO dto, FilmDbContext db) =>
        {
            if (dto.Capienza < 20 || dto.Capienza > 500)
            {
                return Results.BadRequest(new { error = "Capienza non valida (20-500)" });
            }

            if (string.IsNullOrWhiteSpace(dto.Nome) || string.IsNullOrWhiteSpace(dto.Indirizzo) || string.IsNullOrWhiteSpace(dto.Citta))
            {
                return Results.BadRequest(new { error = "Dati cinema non validi" });
            }

            var codiceLocale = NormalizeCodiceLocale(dto.CodiceLocale);
            var codeInUse = await db.Cinemas.AnyAsync(c => c.CodiceLocale == codiceLocale);
            if (codeInUse)
            {
                return Results.Conflict(new { error = "Codice locale gia utilizzato" });
            }

            var c = new Cinema
            {
                Nome = dto.Nome.Trim(),
                Indirizzo = dto.Indirizzo.Trim(),
                Citta = dto.Citta.Trim(),
                Capienza = dto.Capienza,
                Latitudine = dto.Latitudine,
                Longitudine = dto.Longitudine,
                CodiceLocale = codiceLocale,
                Attivo = dto.Attivo
            };
            db.Cinemas.Add(c);
            await db.SaveChangesAsync();
            return Results.Created($"/cinemas/{c.Id}", c);
        }).RequireAuthorization("AdminOnly");

        group.MapPut("/{id:int}", async (int id, CinemaDTO dto, FilmDbContext db) =>
        {
            var c = await db.Cinemas.FindAsync(id);
            if (c is null)
            {
                return Results.NotFound();
            }

            if (dto.Capienza < 20 || dto.Capienza > 500)
            {
                return Results.BadRequest(new { error = "Capienza non valida (20-500)" });
            }

            if (string.IsNullOrWhiteSpace(dto.Nome) || string.IsNullOrWhiteSpace(dto.Indirizzo) || string.IsNullOrWhiteSpace(dto.Citta))
            {
                return Results.BadRequest(new { error = "Dati cinema non validi" });
            }

            var codiceLocale = NormalizeCodiceLocale(dto.CodiceLocale);
            var codeInUse = await db.Cinemas.AnyAsync(x => x.CodiceLocale == codiceLocale && x.Id != id);
            if (codeInUse)
            {
                return Results.Conflict(new { error = "Codice locale gia utilizzato" });
            }

            c.Nome = dto.Nome.Trim();
            c.Indirizzo = dto.Indirizzo.Trim();
            c.Citta = dto.Citta.Trim();
            c.Capienza = dto.Capienza;
            c.Latitudine = dto.Latitudine;
            c.Longitudine = dto.Longitudine;
            c.CodiceLocale = codiceLocale;
            c.Attivo = dto.Attivo;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

	group.MapDelete("/{id:int}", async (int id, FilmDbContext db) =>
	{
		var c = await db.Cinemas.FindAsync(id);
		if (c is null)
		{
			return Results.NotFound();
		}

		db.Cinemas.Remove(c);
		await db.SaveChangesAsync();
		return Results.NoContent();
	}).RequireAuthorization("AdminOnly");

        return group;
    }

    private static string NormalizeCodiceLocale(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return DateTime.UtcNow.Ticks.ToString();
    }
}
