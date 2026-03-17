using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;

namespace FilmAPI.Endpoints;

public static class FilmEndpoints
{
    private static readonly string DefaultCoverImagePath = Environment.GetEnvironmentVariable("DEFAULT_COVER_IMAGE_PATH") ?? "/media/defaults/cover-default.jpg";

    public static RouteGroupBuilder MapFilms(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (FilmDbContext db) => await db.Films.AsNoTracking().ToListAsync());

        group.MapGet("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var f = await db.Films.FindAsync(id);
            return f is not null ? Results.Ok(f) : Results.NotFound();
        });

        group.MapPost("/", async (FilmDTO dto, FilmDbContext db) =>
        {
            var reg = await db.Registi.FindAsync(dto.RegistaId);
            if (reg is null) return Results.BadRequest(new { error = "Regista non trovato" });
            if (dto.Durata <= 0) return Results.BadRequest(new { error = "Durata deve essere > 0" });
            
            var copertinaPath = string.IsNullOrWhiteSpace(dto.CopertinaPath) ? DefaultCoverImagePath : dto.CopertinaPath;
            
            var film = new Film { 
                Titolo = dto.Titolo, 
                DataProduzione = dto.DataProduzione, 
                RegistaId = dto.RegistaId, 
                Durata = dto.Durata,
                CopertinaPath = copertinaPath,
                FilmatoPath = dto.FilmatoPath
            };
            db.Films.Add(film);
            await db.SaveChangesAsync();
            return Results.Created($"/films/{film.Id}", film);
        });

        group.MapPut("/{id:int}", async (int id, FilmDTO dto, FilmDbContext db) =>
        {
            var f = await db.Films.FindAsync(id);
            if (f is null) return Results.NotFound();
            var reg = await db.Registi.FindAsync(dto.RegistaId);
            if (reg is null) return Results.BadRequest(new { error = "Regista non trovato" });
            
            f.Titolo = dto.Titolo;
            f.DataProduzione = dto.DataProduzione;
            f.RegistaId = dto.RegistaId;
            f.Durata = dto.Durata;
            f.CopertinaPath = string.IsNullOrWhiteSpace(dto.CopertinaPath) ? DefaultCoverImagePath : dto.CopertinaPath;
            f.FilmatoPath = dto.FilmatoPath;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapDelete("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var f = await db.Films.FindAsync(id);
            if (f is null) return Results.NotFound();
            db.Films.Remove(f);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return group;
    }
}
