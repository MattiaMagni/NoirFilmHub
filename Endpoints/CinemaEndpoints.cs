using FilmAPI.Data;
using FilmAPI.DTOs;
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
            var cinemas = await db.Cinemas
                .AsNoTracking()
                .Where(c => c.Latitudine.HasValue && c.Longitudine.HasValue)
                .Select(c => new
                {
                    c.Id,
                    c.Nome,
                    c.Citta,
                    c.Indirizzo,
                    c.Latitudine,
                    c.Longitudine,
                    DistanzaKm = DistanceKm(lat, lng, c.Latitudine!.Value, c.Longitudine!.Value)
                })
                .OrderBy(c => c.DistanzaKm)
                .Take(50)
                .ToListAsync();

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

    private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371d;
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return Math.Round(r * c, 2);
    }

    private static double ToRad(double degree) => degree * Math.PI / 180d;
}
