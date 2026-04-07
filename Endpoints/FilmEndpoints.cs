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

            var response = films.Select(f => new
            {
                f.Id,
                f.Titolo,
                f.DataProduzione,
                f.RegistaId,
                f.Durata,
                f.CopertinaPath,
                f.FilmatoPath,
                CategorieIds = f.FilmCategorie.Select(fc => fc.CategoriaId).ToList(),
                Categorie = f.FilmCategorie.Select(fc => fc.Categoria.Nome).ToList()
            });

            return Results.Ok(response);
        }).AllowAnonymous();

        group.MapGet("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var f = await db.Films
                .AsNoTracking()
                .Include(x => x.FilmCategorie)
                .ThenInclude(fc => fc.Categoria)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (f is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new
            {
                f.Id,
                f.Titolo,
                f.DataProduzione,
                f.RegistaId,
                f.Durata,
                f.CopertinaPath,
                f.FilmatoPath,
                CategorieIds = f.FilmCategorie.Select(fc => fc.CategoriaId).ToList(),
                Categorie = f.FilmCategorie.Select(fc => fc.Categoria.Nome).ToList()
            });
        }).AllowAnonymous();

        group.MapPost("/", async (FilmDTO dto, FilmDbContext db) =>
        {
            var reg = await db.Registi.FindAsync(dto.RegistaId);
            if (reg is null)
            {
                return Results.BadRequest(new { error = "Regista non trovato" });
            }

            if (dto.Durata <= 0)
            {
                return Results.BadRequest(new { error = "Durata deve essere > 0" });
            }

            var categoriaIds = (dto.CategorieIds ?? new List<int>()).Distinct().ToList();
            if (categoriaIds.Count > 0)
            {
                var existingCount = await db.Categorie.CountAsync(c => categoriaIds.Contains(c.Id));
                if (existingCount != categoriaIds.Count)
                {
                    return Results.BadRequest(new { error = "Una o piu categorie non esistono" });
                }
            }

            var copertinaPath = string.IsNullOrWhiteSpace(dto.CopertinaPath) ? DefaultCoverImagePath : dto.CopertinaPath;
            var film = new Film
            {
                Titolo = dto.Titolo,
                DataProduzione = dto.DataProduzione,
                RegistaId = dto.RegistaId,
                Durata = dto.Durata,
                CopertinaPath = copertinaPath,
                FilmatoPath = dto.FilmatoPath
            };

            db.Films.Add(film);
            await db.SaveChangesAsync();

            if (categoriaIds.Count > 0)
            {
                db.FilmCategorie.AddRange(categoriaIds.Select(categoriaId => new FilmCategoria
                {
                    FilmId = film.Id,
                    CategoriaId = categoriaId
                }));
                await db.SaveChangesAsync();
            }

            return Results.Created($"/films/{film.Id}", film);
        }).RequireAuthorization("AdminOrPowerUser");

        group.MapPut("/{id:int}", async (int id, FilmDTO dto, FilmDbContext db) =>
        {
            var f = await db.Films.Include(x => x.FilmCategorie).FirstOrDefaultAsync(x => x.Id == id);
            if (f is null)
            {
                return Results.NotFound();
            }

            var reg = await db.Registi.FindAsync(dto.RegistaId);
            if (reg is null)
            {
                return Results.BadRequest(new { error = "Regista non trovato" });
            }

            if (dto.Durata <= 0)
            {
                return Results.BadRequest(new { error = "Durata deve essere > 0" });
            }

            var categoriaIds = (dto.CategorieIds ?? new List<int>()).Distinct().ToList();
            if (categoriaIds.Count > 0)
            {
                var existingCount = await db.Categorie.CountAsync(c => categoriaIds.Contains(c.Id));
                if (existingCount != categoriaIds.Count)
                {
                    return Results.BadRequest(new { error = "Una o piu categorie non esistono" });
                }
            }

            f.Titolo = dto.Titolo;
            f.DataProduzione = dto.DataProduzione;
            f.RegistaId = dto.RegistaId;
            f.Durata = dto.Durata;
            f.CopertinaPath = string.IsNullOrWhiteSpace(dto.CopertinaPath) ? DefaultCoverImagePath : dto.CopertinaPath;
            f.FilmatoPath = dto.FilmatoPath;

            db.FilmCategorie.RemoveRange(f.FilmCategorie);
            if (categoriaIds.Count > 0)
            {
                db.FilmCategorie.AddRange(categoriaIds.Select(categoriaId => new FilmCategoria
                {
                    FilmId = f.Id,
                    CategoriaId = categoriaId
                }));
            }

            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOrPowerUser");

        group.MapDelete("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var f = await db.Films.FindAsync(id);
            if (f is null)
            {
                return Results.NotFound();
            }

            db.Films.Remove(f);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOrPowerUser");

        return group;
    }
}
