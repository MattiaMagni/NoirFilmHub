using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Helpers;
using FilmAPI.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Endpoints;

public static class MyCinemasEndpoints
{
    public static RouteGroupBuilder MapMyCinemas(this RouteGroupBuilder group)
    {
        group.MapGet("/tipologie", async (FilmDbContext db) =>
        {
            var tipologie = await db.Sale
                .AsNoTracking()
                .Where(s => s.Attiva)
                .Select(s => s.Tipologia)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            return Results.Ok(tipologie);
        }).AllowAnonymous();

        group.MapGet("/", async (
            FilmDbContext db,
            string? citta,
            string? tipologiaSala,
            double? lat,
            double? lng,
            double? raggio) =>
        {
            IQueryable<Cinema> query = db.Cinemas.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(citta))
            {
                query = query.Where(c => EF.Functions.Like(c.Citta, $"%{citta.Trim()}%"));
            }

            if (!string.IsNullOrWhiteSpace(tipologiaSala))
            {
                query = query.Where(c => c.Sale.Any(s => s.Attiva && s.Tipologia == tipologiaSala.Trim()));
            }

            var hasGeo = lat.HasValue && lng.HasValue;

            List<Cinema> cinemas;
            if (hasGeo)
            {
                cinemas = await query
                    .Include(c => c.Sale)
                    .Where(c => c.Latitudine.HasValue && c.Longitudine.HasValue)
                    .ToListAsync();

                var withDistance = cinemas
                    .Select(c => new
                    {
                        c.Id,
                        c.Nome,
                        c.Citta,
                        c.Indirizzo,
                        c.Latitudine,
                        c.Longitudine,
                        c.CodiceLocale,
                        TipologieSala = c.Sale
                            .Where(s => s.Attiva)
                            .Select(s => s.Tipologia)
                            .Distinct()
                            .OrderBy(x => x)
                            .ToList(),
                        DistanzaKm = GeoHelper.DistanceKm(lat!.Value, lng!.Value, c.Latitudine!.Value, c.Longitudine!.Value)
                    })
                    .Where(c => !raggio.HasValue || c.DistanzaKm <= raggio.Value)
                    .OrderBy(c => c.DistanzaKm)
                    .ThenBy(c => c.Citta)
                    .ThenBy(c => c.Nome)
                    .ToList();

                return Results.Ok(withDistance);
            }

            cinemas = await query
                .Include(c => c.Sale)
                .OrderBy(c => c.Citta)
                .ThenBy(c => c.Nome)
                .ToListAsync();

            var response = cinemas
                .Select(c => new
                {
                    c.Id,
                    c.Nome,
                    c.Citta,
                    c.Indirizzo,
                    c.Latitudine,
                    c.Longitudine,
                    c.CodiceLocale,
                    TipologieSala = c.Sale
                        .Where(s => s.Attiva)
                        .Select(s => s.Tipologia)
                        .Distinct()
                        .OrderBy(x => x)
                        .ToList()
                })
                .ToList();

            return Results.Ok(response);
        }).AllowAnonymous();

        group.MapGet("/{cinemaId:int}/programmazione", async (int cinemaId, FilmDbContext db, DateTime? day) =>
        {
            var cinema = await db.Cinemas
                .AsNoTracking()
                .Include(c => c.Sale)
                .FirstOrDefaultAsync(c => c.Id == cinemaId);
            if (cinema is null)
            {
                return Results.NotFound();
            }

            var selectedDay = (day ?? DateTime.Today).Date;
            var nextDay = selectedDay.AddDays(1);
            var windowStart = DateTime.Today;
            var windowEndExclusive = windowStart.AddDays(31);

            var availableDays = await db.Proiezioni
                .AsNoTracking()
                .Where(p => p.CinemaId == cinemaId && p.Data >= windowStart && p.Data < windowEndExclusive)
                .Select(p => p.Data.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            var shows = await db.Proiezioni
                .AsNoTracking()
                .Include(p => p.Film)
                .Include(p => p.Sala)
                .Where(p => p.CinemaId == cinemaId && p.Data >= selectedDay && p.Data < nextDay)
                .OrderBy(p => p.Ora)
                .ToListAsync();

            var films = shows
                .GroupBy(p => p.FilmId)
                .Select(g => new
                {
                    FilmId = g.Key,
                    Titolo = g.First().Film.Titolo,
                    CopertinaPath = g.First().Film.CopertinaPath,
                    DescrizioneLunga = g.First().Film.DescrizioneLunga,
                    Tipologie = g
                        .GroupBy(x => x.Sala?.Tipologia ?? "2D")
                        .Select(t => new ProiezioneCalendarioDTO
                        {
                            TipologiaSala = t.Key,
                            Orari = t
                                .OrderBy(x => x.Ora)
                                .Select(x => new ProiezioneCalendarioItemDTO
                                {
                                    ProiezioneId = x.Id,
                                    SalaId = x.SalaId ?? 0,
                                    Ora = x.Ora.ToString("HH:mm"),
                                    Prezzo = x.PrezzoBase
                                })
                                .ToList()
                        })
                        .OrderBy(x => x.TipologiaSala)
                        .ToList()
                })
                .OrderBy(x => x.Titolo)
                .ToList();

            return Results.Ok(new
            {
                Cinema = new
                {
                    cinema.Id,
                    cinema.Nome,
                    cinema.Citta,
                    cinema.Indirizzo,
                    TipologieSala = cinema.Sale.Where(s => s.Attiva).Select(s => s.Tipologia).Distinct().OrderBy(x => x).ToList()
                },
                Giorno = selectedDay,
                AvailableDays = availableDays,
                Programmazione = films
            });
        }).AllowAnonymous();

        return group;
    }
}
