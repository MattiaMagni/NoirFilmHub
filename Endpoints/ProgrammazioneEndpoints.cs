using FilmAPI.Data;
using FilmAPI.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Endpoints;

public static class ProgrammazioneEndpoints
{
    public static RouteGroupBuilder MapProgrammazione(this RouteGroupBuilder group)
    {
        group.MapGet("/shows", async (FilmDbContext db, int? filmId, int? cinemaId, DateTime? day) =>
        {
            var date = (day ?? DateTime.Today).Date;

            var query = db.Proiezioni
                .AsNoTracking()
                .Include(p => p.Sala)
                .Where(p => p.Data.Date == date);

            if (filmId.HasValue)
            {
                query = query.Where(p => p.FilmId == filmId.Value);
            }

            if (cinemaId.HasValue)
            {
                query = query.Where(p => p.CinemaId == cinemaId.Value);
            }

            var list = await query
                .OrderBy(p => p.Ora)
                .ToListAsync();

            var grouped = list
                .GroupBy(p => p.Sala?.Tipologia ?? "2D")
                .Select(g => new ProiezioneCalendarioDTO
                {
                    TipologiaSala = g.Key,
                    Orari = g
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
                .ToList();

            return Results.Ok(new
            {
                data = date,
                filmId,
                cinemaId,
                tipologie = grouped
            });
        }).AllowAnonymous();

        group.MapGet("/films", async (FilmDbContext db, string? tab, string? search, int? categoria, int? cinemaId) =>
        {
            var now = DateTime.Today;
            var nextWeek = now.AddDays(7);
            var nextTwoWeeks = now.AddDays(14);

            var filmsQuery = db.Films
                .AsNoTracking()
                .Include(f => f.FilmCategorie)
                .ThenInclude(fc => fc.Categoria)
                .Include(f => f.Regista)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchText = search.Trim().ToLowerInvariant();
                filmsQuery = filmsQuery.Where(f => f.Titolo.ToLower().Contains(searchText) || f.TitoloOriginale.ToLower().Contains(searchText));
            }

            if (categoria.HasValue)
            {
                filmsQuery = filmsQuery.Where(f => f.FilmCategorie.Any(fc => fc.CategoriaId == categoria.Value));
            }

            var films = await filmsQuery.ToListAsync();

            var showsQuery = db.Proiezioni
                .AsNoTracking()
                .Include(p => p.Sala)
                .Where(p => p.Data >= now && p.Data <= nextTwoWeeks);

            if (cinemaId.HasValue)
            {
                showsQuery = showsQuery.Where(p => p.CinemaId == cinemaId.Value);
            }

            var shows = await showsQuery.ToListAsync();

            var showCountByFilm = shows
                .GroupBy(s => s.FilmId)
                .ToDictionary(g => g.Key, g => g.Count());

            var featuredFilmIds = shows
                .Where(s => s.Data <= nextWeek)
                .GroupBy(s => s.FilmId)
                .OrderByDescending(g => g.Count())
                .Take(12)
                .Select(g => g.Key)
                .ToHashSet();

            var comingFilmIds = films
                .Where(f => f.DataUscita.HasValue && f.DataUscita.Value.Date > now && f.DataUscita.Value.Date <= nextTwoWeeks)
                .Select(f => f.Id)
                .ToHashSet();

            var normalizedTab = (tab ?? "all").Trim().ToLowerInvariant();

            var filtered = films.Where(f =>
            {
                if (normalizedTab == "featured")
                {
                    return featuredFilmIds.Contains(f.Id);
                }

                if (normalizedTab == "coming")
                {
                    return comingFilmIds.Contains(f.Id);
                }

                return true;
            });

            var list = filtered
                .OrderBy(f => f.Titolo)
                .Select(f => new
                {
                    f.Id,
                    f.Titolo,
                    f.TitoloOriginale,
                    f.DataProduzione,
                    f.DataUscita,
                    Regista = f.Regista != null ? $"{f.Regista.Nome} {f.Regista.Cognome}".Trim() : string.Empty,
                    f.Durata,
                    f.CopertinaPath,
                    f.BackdropPath,
                    f.DescrizioneLunga,
                    f.CastPrincipale,
                    f.FilmatoPath,
                    Categorie = f.FilmCategorie.Select(fc => fc.Categoria.Nome).ToList(),
                    ShowCount = showCountByFilm.TryGetValue(f.Id, out var count) ? count : 0,
                    IsFeatured = featuredFilmIds.Contains(f.Id),
                    IsComing = comingFilmIds.Contains(f.Id),
                    PresenteNelCinemaSelezionato = cinemaId.HasValue && shows.Any(s => s.FilmId == f.Id && s.CinemaId == cinemaId.Value)
                })
                .ToList();

            if (normalizedTab == "coming")
            {
                list = list.Where(x => x.IsComing).ToList();
            }

            if (normalizedTab == "featured")
            {
                list = list.Where(x => x.IsFeatured).ToList();
            }

            return Results.Ok(list);
        }).AllowAnonymous();

        group.MapGet("/films/{filmId:int}", async (int filmId, FilmDbContext db, int? cinemaId) =>
        {
            var film = await db.Films
                .AsNoTracking()
                .Include(f => f.Regista)
                .Include(f => f.FilmCategorie)
                .ThenInclude(fc => fc.Categoria)
                .FirstOrDefaultAsync(f => f.Id == filmId);

            if (film is null)
            {
                return Results.NotFound();
            }

            var today = DateTime.Today;
            var endDate = today.AddDays(14);

            var selectedCinema = cinemaId.HasValue
                ? await db.Cinemas.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cinemaId.Value)
                : null;

            var shows = await db.Proiezioni
                .AsNoTracking()
                .Include(p => p.Cinema)
                .Include(p => p.Sala)
                .Where(p => p.FilmId == filmId && p.Data >= today && p.Data <= endDate && (!cinemaId.HasValue || p.CinemaId == cinemaId.Value))
                .OrderBy(p => p.Data)
                .ThenBy(p => p.Ora)
                .ToListAsync();

            var grouped = shows
                .GroupBy(p => p.Data.Date)
                .Select(g => new
                {
                    Data = g.Key,
                    CinemaNome = g.First().Cinema.Nome,
                    Citta = g.First().Cinema.Citta,
                    Indirizzo = g.First().Cinema.Indirizzo,
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
                        .ToList()
                })
                .ToList();

            return Results.Ok(new
            {
                film.Id,
                film.Titolo,
                film.TitoloOriginale,
                film.CopertinaPath,
                film.BackdropPath,
                film.Durata,
                film.DataProduzione,
                film.DataUscita,
                film.DescrizioneLunga,
                film.CastPrincipale,
                film.FilmatoPath,
                Regista = film.Regista is null ? null : $"{film.Regista.Nome} {film.Regista.Cognome}".Trim(),
                Categorie = film.FilmCategorie.Select(fc => fc.Categoria.Nome).ToList(),
                CinemaNome = selectedCinema?.Nome ?? string.Empty,
                Citta = selectedCinema?.Citta ?? string.Empty,
                Indirizzo = selectedCinema?.Indirizzo ?? string.Empty,
                Calendario = grouped
            });
        }).AllowAnonymous();

        return group;
    }
}
