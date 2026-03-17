using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;

namespace FilmAPI.Endpoints;

public static class ProiezioniEndpoints
{
    public static RouteGroupBuilder MapProiezioni(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (FilmDbContext db) => await db.Proiezioni.AsNoTracking().ToListAsync());

        group.MapGet("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var p = await db.Proiezioni.FindAsync(id);
            return p is not null ? Results.Ok(p) : Results.NotFound();
        });

        group.MapPost("/", async (ProiezioneCreateDTO dto, FilmDbContext db) =>
        {
            var film = await db.Films.FindAsync(dto.FilmId);
            if (film is null) return Results.BadRequest(new { error = "Film non trovato" });
            var cinema = await db.Cinemas.FindAsync(dto.CinemaId);
            if (cinema is null) return Results.BadRequest(new { error = "Cinema non trovato" });

            var p = new Proiezione { FilmId = dto.FilmId, CinemaId = dto.CinemaId, Data = dto.Data, Ora = dto.Ora };
            db.Proiezioni.Add(p);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // handle unique constraint / duplicate
                return Results.Conflict(new { error = "Proiezione duplicata o vincolo violato", details = ex.Message });
            }
            return Results.Created($"/proiezioni/{p.Id}", p);
        });

        group.MapPut("/{id:int}", async (int id, ProiezioneCreateDTO dto, FilmDbContext db) =>
        {
            var p = await db.Proiezioni.FindAsync(id);
            if (p is null) return Results.NotFound();
            var film = await db.Films.FindAsync(dto.FilmId);
            if (film is null) return Results.BadRequest(new { error = "Film non trovato" });
            var cinema = await db.Cinemas.FindAsync(dto.CinemaId);
            if (cinema is null) return Results.BadRequest(new { error = "Cinema non trovato" });
            p.FilmId = dto.FilmId;
            p.CinemaId = dto.CinemaId;
            p.Data = dto.Data;
            p.Ora = dto.Ora;
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Proiezione duplicata o vincolo violato", details = ex.Message });
            }
            return Results.NoContent();
        });

        group.MapDelete("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var p = await db.Proiezioni.FindAsync(id);
            if (p is null) return Results.NotFound();
            db.Proiezioni.Remove(p);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return group;
    }
}
