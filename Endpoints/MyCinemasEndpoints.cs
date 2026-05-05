using FilmAPI.Data;
using FilmAPI.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Endpoints;

public static class MyCinemasEndpoints
{
    public static RouteGroupBuilder MapMyCinemas(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (FilmDbContext db) =>
        {
            var cinemas = await db.Cinemas
                .AsNoTracking()
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
            var shows = await db.Proiezioni
                .AsNoTracking()
                .Include(p => p.Film)
                .Include(p => p.Sala)
                .Where(p => p.CinemaId == cinemaId && p.Data.Date == selectedDay)
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
                Programmazione = films
            });
        }).AllowAnonymous();

        return group;
    }
}
