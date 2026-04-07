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
        group.MapGet("/", async (FilmDbContext db) => await db.Cinemas.AsNoTracking().ToListAsync()).AllowAnonymous();

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

            var c = new Cinema { Nome = dto.Nome, Indirizzo = dto.Indirizzo, Citta = dto.Citta, Capienza = dto.Capienza };
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

            c.Nome = dto.Nome;
            c.Indirizzo = dto.Indirizzo;
            c.Citta = dto.Citta;
            c.Capienza = dto.Capienza;
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
}
