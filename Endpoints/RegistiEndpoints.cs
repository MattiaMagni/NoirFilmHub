using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Endpoints;

public static class RegistiEndpoints
{
    public static RouteGroupBuilder MapRegisti(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (FilmDbContext db) => await db.Registi.AsNoTracking().ToListAsync()).AllowAnonymous();

        group.MapGet("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var r = await db.Registi.FindAsync(id);
            return r is not null ? Results.Ok(r) : Results.NotFound();
        }).AllowAnonymous();

        group.MapPost("/", async (RegistaDTO dto, FilmDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Nome) || string.IsNullOrWhiteSpace(dto.Cognome) || string.IsNullOrWhiteSpace(dto.Nazionalita))
            {
                return Results.BadRequest(new { error = "Dati regista non validi" });
            }

            var entity = new Regista { Nome = dto.Nome, Cognome = dto.Cognome, Nazionalita = dto.Nazionalita };
            db.Registi.Add(entity);
            await db.SaveChangesAsync();
            return Results.Created($"/registi/{entity.Id}", entity);
        }).RequireAuthorization("AdminOrPowerUser");

        group.MapPut("/{id:int}", async (int id, RegistaDTO dto, FilmDbContext db) =>
        {
            var r = await db.Registi.FindAsync(id);
            if (r is null)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(dto.Nome) || string.IsNullOrWhiteSpace(dto.Cognome) || string.IsNullOrWhiteSpace(dto.Nazionalita))
            {
                return Results.BadRequest(new { error = "Dati regista non validi" });
            }

            r.Nome = dto.Nome;
            r.Cognome = dto.Cognome;
            r.Nazionalita = dto.Nazionalita;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOrPowerUser");

        group.MapDelete("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var r = await db.Registi.FindAsync(id);
            if (r is null)
            {
                return Results.NotFound();
            }

            db.Registi.Remove(r);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOrPowerUser");

        group.MapGet("/{id:int}/films", async (int id, FilmDbContext db) =>
        {
            var reg = await db.Registi.FindAsync(id);
            if (reg is null)
            {
                return Results.NotFound();
            }

            var films = await db.Films
                .Where(f => f.RegistaId == id)
                .AsNoTracking()
                .ToListAsync();
            return Results.Ok(films);
        }).AllowAnonymous();

        group.MapPost("/{id:int}/films", async (int id, FilmDTO dto, FilmDbContext db) =>
        {
            var reg = await db.Registi.FindAsync(id);
            if (reg is null)
            {
                return Results.NotFound();
            }

            if (dto.Durata <= 0)
            {
                return Results.BadRequest(new { error = "Durata deve essere > 0" });
            }

            var film = new Film
            {
                Titolo = dto.Titolo,
                DataProduzione = dto.DataProduzione,
                RegistaId = id,
                Durata = dto.Durata,
                CopertinaPath = string.IsNullOrWhiteSpace(dto.CopertinaPath) ? null : dto.CopertinaPath,
                FilmatoPath = dto.FilmatoPath
            };
            db.Films.Add(film);
            await db.SaveChangesAsync();

            var categoriaIds = (dto.CategorieIds ?? new List<int>()).Distinct().ToList();
            if (categoriaIds.Count > 0)
            {
                var existingCount = await db.Categorie.CountAsync(c => categoriaIds.Contains(c.Id));
                if (existingCount != categoriaIds.Count)
                {
                    return Results.BadRequest(new { error = "Una o piu categorie non esistono" });
                }

                db.FilmCategorie.AddRange(categoriaIds.Select(categoriaId => new FilmCategoria
                {
                    FilmId = film.Id,
                    CategoriaId = categoriaId
                }));
                await db.SaveChangesAsync();
            }

            return Results.Created($"/films/{film.Id}", film);
        }).RequireAuthorization("AdminOrPowerUser");

        return group;
    }
}
