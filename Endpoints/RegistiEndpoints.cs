using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;

namespace FilmAPI.Endpoints;

public static class RegistiEndpoints
{
    public static RouteGroupBuilder MapRegisti(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (FilmDbContext db) => await db.Registi.AsNoTracking().ToListAsync());

        group.MapGet("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var r = await db.Registi.FindAsync(id);
            return r is not null ? Results.Ok(r) : Results.NotFound();
        });

        group.MapPost("/", async (RegistaDTO dto, FilmDbContext db) =>
        {
            var entity = new Regista { Nome = dto.Nome, Cognome = dto.Cognome, Nazionalita = dto.Nazionalita };
            db.Registi.Add(entity);
            await db.SaveChangesAsync();
            return Results.Created($"/registi/{entity.Id}", entity);
        });

        group.MapPut("/{id:int}", async (int id, RegistaDTO dto, FilmDbContext db) =>
        {
            var r = await db.Registi.FindAsync(id);
            if (r is null) return Results.NotFound();
            r.Nome = dto.Nome;
            r.Cognome = dto.Cognome;
            r.Nazionalita = dto.Nazionalita;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapDelete("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var r = await db.Registi.FindAsync(id);
            if (r is null) return Results.NotFound();
            db.Registi.Remove(r);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapGet("/{id:int}/films", async (int id, FilmDbContext db) =>
        {
            var reg = await db.Registi.FindAsync(id);
            if (reg is null) return Results.NotFound();
            var films = await db.Films.Where(f => f.RegistaId == id).AsNoTracking().ToListAsync();
            return Results.Ok(films);
        });

        group.MapPost("/{id:int}/films", async (int id, FilmDTO dto, FilmDbContext db) =>
        {
            var reg = await db.Registi.FindAsync(id);
            if (reg is null) return Results.NotFound();
            var film = new Film { Titolo = dto.Titolo, DataProduzione = dto.DataProduzione, RegistaId = id, Durata = dto.Durata };
            db.Films.Add(film);
            await db.SaveChangesAsync();
            return Results.Created($"/films/{film.Id}", film);
        });

        return group;
    }
}
