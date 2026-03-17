using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;

namespace FilmAPI.Endpoints;

public static class CinemaEndpoints
{
    public static RouteGroupBuilder MapCinemas(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (FilmDbContext db) => await db.Cinemas.AsNoTracking().ToListAsync());

        group.MapGet("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var c = await db.Cinemas.FindAsync(id);
            return c is not null ? Results.Ok(c) : Results.NotFound();
        });

        group.MapPost("/", async (CinemaDTO dto, FilmDbContext db) =>
        {
            var c = new Cinema { Nome = dto.Nome, Indirizzo = dto.Indirizzo, Citta = dto.Citta };
            db.Cinemas.Add(c);
            await db.SaveChangesAsync();
            return Results.Created($"/cinemas/{c.Id}", c);
        });

        group.MapPut("/{id:int}", async (int id, CinemaDTO dto, FilmDbContext db) =>
        {
            var c = await db.Cinemas.FindAsync(id);
            if (c is null) return Results.NotFound();
            c.Nome = dto.Nome;
            c.Indirizzo = dto.Indirizzo;
            c.Citta = dto.Citta;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapDelete("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var c = await db.Cinemas.FindAsync(id);
            if (c is null) return Results.NotFound();
            db.Cinemas.Remove(c);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return group;
    }
}
