using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Endpoints;

public static class FilmEndpoints
{
    private static readonly string DefaultCoverImagePath = Environment.GetEnvironmentVariable("DEFAULT_COVER_IMAGE_PATH") ?? "/media/defaults/cover-default.jpg";

    public static RouteGroupBuilder MapFilms(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (FilmDbContext db) =>
        {
            var films = await db.Films
                .AsNoTracking()
                .Include(f => f.FilmCategorie)
                .ThenInclude(fc => fc.Categoria)
                .ToListAsync();

            return Results.Ok(films.Select(ToFilmResponse));
        }).AllowAnonymous();

        group.MapGet("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var film = await db.Films
                .AsNoTracking()
                .Include(x => x.FilmCategorie)
                .ThenInclude(fc => fc.Categoria)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (film is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(ToFilmResponse(film));
        }).AllowAnonymous();

        group.MapPost("/", async (FilmDTO dto, FilmDbContext db) =>
        {
            var validationError = await ValidateFilmInputAsync(dto, db);
            if (validationError is not null)
            {
                return validationError;
            }

            var categoriaIds = DistinctCategoriaIds(dto.CategorieIds);
            var film = new Film
            {
                Titolo = dto.Titolo,
                TitoloOriginale = dto.TitoloOriginale?.Trim() ?? string.Empty,
                DataProduzione = dto.DataProduzione,
                DataUscita = dto.DataUscita,
                RegistaId = dto.RegistaId,
                Durata = dto.Durata,
                CopertinaPath = string.IsNullOrWhiteSpace(dto.CopertinaPath) ? DefaultCoverImagePath : dto.CopertinaPath,
                BackdropPath = dto.BackdropPath,
                FilmatoPath = dto.FilmatoPath,
                DescrizioneLunga = dto.DescrizioneLunga?.Trim() ?? string.Empty,
                CastPrincipale = dto.CastPrincipale?.Trim() ?? string.Empty,
                TmdbMovieId = dto.TmdbMovieId
            };

            db.Films.Add(film);
            await db.SaveChangesAsync();

            AddFilmCategorie(db, film.Id, categoriaIds);
            await db.SaveChangesAsync();

            return Results.Created($"/films/{film.Id}", film);
        }).RequireAuthorization("AdminOrPowerUser");

        group.MapPut("/{id:int}", async (int id, FilmDTO dto, FilmDbContext db) =>
        {
            var film = await db.Films.Include(x => x.FilmCategorie).FirstOrDefaultAsync(x => x.Id == id);
            if (film is null)
            {
                return Results.NotFound();
            }

            var validationError = await ValidateFilmInputAsync(dto, db);
            if (validationError is not null)
            {
                return validationError;
            }

            var categoriaIds = DistinctCategoriaIds(dto.CategorieIds);

            film.Titolo = dto.Titolo;
            film.TitoloOriginale = dto.TitoloOriginale?.Trim() ?? string.Empty;
            film.DataProduzione = dto.DataProduzione;
            film.DataUscita = dto.DataUscita;
            film.RegistaId = dto.RegistaId;
            film.Durata = dto.Durata;
            film.CopertinaPath = string.IsNullOrWhiteSpace(dto.CopertinaPath) ? DefaultCoverImagePath : dto.CopertinaPath;
            film.BackdropPath = dto.BackdropPath;
            film.FilmatoPath = dto.FilmatoPath;
            film.DescrizioneLunga = dto.DescrizioneLunga?.Trim() ?? string.Empty;
            film.CastPrincipale = dto.CastPrincipale?.Trim() ?? string.Empty;
            film.TmdbMovieId = dto.TmdbMovieId;

            db.FilmCategorie.RemoveRange(film.FilmCategorie);
            AddFilmCategorie(db, film.Id, categoriaIds);

            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOrPowerUser");

        group.MapDelete("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var film = await db.Films.FindAsync(id);
            if (film is null)
            {
                return Results.NotFound();
            }

            db.Films.Remove(film);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOrPowerUser");

        return group;
    }

    private static List<int> DistinctCategoriaIds(List<int>? categoriaIds)
    {
        return (categoriaIds ?? []).Distinct().ToList();
    }

    private static async Task<IResult?> ValidateFilmInputAsync(FilmDTO dto, FilmDbContext db)
    {
        if (dto.Durata <= 0)
        {
            return Results.BadRequest(new { error = "Durata deve essere > 0" });
        }

        var registaExists = await db.Registi.AnyAsync(r => r.Id == dto.RegistaId);
        if (!registaExists)
        {
            return Results.BadRequest(new { error = "Regista non trovato" });
        }

        var categoriaIds = DistinctCategoriaIds(dto.CategorieIds);
        if (categoriaIds.Count == 0)
        {
            return null;
        }

        var existingCount = await db.Categorie.CountAsync(c => categoriaIds.Contains(c.Id));
        if (existingCount != categoriaIds.Count)
        {
            return Results.BadRequest(new { error = "Una o piu categorie non esistono" });
        }

        return null;
    }

    private static void AddFilmCategorie(FilmDbContext db, int filmId, List<int> categoriaIds)
    {
        if (categoriaIds.Count == 0)
        {
            return;
        }

        db.FilmCategorie.AddRange(categoriaIds.Select(categoriaId => new FilmCategoria
        {
            FilmId = filmId,
            CategoriaId = categoriaId
        }));
    }

    private static object ToFilmResponse(Film film)
    {
        return new
        {
            film.Id,
            film.Titolo,
            film.TitoloOriginale,
            film.DataProduzione,
            film.DataUscita,
            film.RegistaId,
            film.Durata,
            film.CopertinaPath,
            film.BackdropPath,
            film.FilmatoPath,
            film.DescrizioneLunga,
            film.CastPrincipale,
            film.TmdbMovieId,
            film.UltimaSyncTmdbUtc,
            film.TmdbSyncStato,
            CategorieIds = film.FilmCategorie.Select(fc => fc.CategoriaId).ToList(),
            Categorie = film.FilmCategorie.Select(fc => fc.Categoria.Nome).ToList()
        };
    }
}
