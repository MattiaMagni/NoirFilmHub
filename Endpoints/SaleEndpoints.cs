using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Endpoints;

public static class SaleEndpoints
{
    private static readonly HashSet<string> TipologieValide =
    [
        "ISENSE",
        "XL",
        "3D",
        "2D"
    ];

    public static RouteGroupBuilder MapSale(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (FilmDbContext db, int? cinemaId) =>
        {
            var query = db.Sale.AsNoTracking().AsQueryable();
            if (cinemaId.HasValue)
            {
                query = query.Where(s => s.CinemaId == cinemaId.Value);
            }

            var sale = await query
                .OrderBy(s => s.CinemaId)
                .ThenBy(s => s.NumeroProgressivo)
                .ToListAsync();

            var items = sale.Select(ToSalaDto).ToList();

            return Results.Ok(items);
        }).AllowAnonymous();

        group.MapGet("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var sala = await db.Sale.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
            return sala is null ? Results.NotFound() : Results.Ok(ToSalaDto(sala));
        }).AllowAnonymous();

        group.MapPost("/", async (SalaCreateDTO dto, FilmDbContext db) =>
        {
            var validation = await ValidateInput(dto, db, null);
            if (validation is not null)
            {
                return validation;
            }

            var sala = new Sala
            {
                CinemaId = dto.CinemaId,
                NumeroProgressivo = dto.NumeroProgressivo,
                Tipologia = dto.Tipologia.Trim().ToUpperInvariant(),
                Nome = dto.Nome?.Trim() ?? string.Empty,
                NumeroFile = dto.NumeroFile,
                PostiPerFila = dto.PostiPerFila,
                MappaPostiJson = dto.MappaPostiJson?.Trim() ?? string.Empty,
                Attiva = dto.Attiva
            };

            db.Sale.Add(sala);
            await db.SaveChangesAsync();
            return Results.Created($"/sale/{sala.Id}", ToSalaDto(sala));
        }).RequireAuthorization("AdminOrPowerUser");

        group.MapPut("/{id:int}", async (int id, SalaCreateDTO dto, FilmDbContext db) =>
        {
            var sala = await db.Sale.FindAsync(id);
            if (sala is null)
            {
                return Results.NotFound();
            }

            var validation = await ValidateInput(dto, db, id);
            if (validation is not null)
            {
                return validation;
            }

            sala.CinemaId = dto.CinemaId;
            sala.NumeroProgressivo = dto.NumeroProgressivo;
            sala.Tipologia = dto.Tipologia.Trim().ToUpperInvariant();
            sala.Nome = dto.Nome?.Trim() ?? string.Empty;
            sala.NumeroFile = dto.NumeroFile;
            sala.PostiPerFila = dto.PostiPerFila;
            sala.MappaPostiJson = dto.MappaPostiJson?.Trim() ?? string.Empty;
            sala.Attiva = dto.Attiva;

            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOrPowerUser");

        group.MapDelete("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var sala = await db.Sale.FindAsync(id);
            if (sala is null)
            {
                return Results.NotFound();
            }

            var hasShows = await db.Proiezioni.AnyAsync(p => p.SalaId == id);
            if (hasShows)
            {
                return Results.BadRequest(new { error = "Impossibile eliminare la sala: esistono show collegati" });
            }

            db.Sale.Remove(sala);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOrPowerUser");

        return group;
    }

    private static SalaDTO ToSalaDto(Sala sala)
    {
        return new SalaDTO
        {
            Id = sala.Id,
            CinemaId = sala.CinemaId,
            NumeroProgressivo = sala.NumeroProgressivo,
            Tipologia = sala.Tipologia,
            Nome = sala.Nome,
            NumeroFile = sala.NumeroFile,
            PostiPerFila = sala.PostiPerFila,
            MappaPostiJson = sala.MappaPostiJson,
            Attiva = sala.Attiva
        };
    }

    private static async Task<IResult?> ValidateInput(SalaCreateDTO dto, FilmDbContext db, int? currentId)
    {
        if (dto.CinemaId <= 0)
        {
            return Results.BadRequest(new { error = "CinemaId non valido" });
        }

        if (dto.NumeroProgressivo <= 0)
        {
            return Results.BadRequest(new { error = "Numero progressivo non valido" });
        }

        var tipologia = (dto.Tipologia ?? string.Empty).Trim().ToUpperInvariant();
        if (!TipologieValide.Contains(tipologia))
        {
            return Results.BadRequest(new { error = "Tipologia sala non valida" });
        }

        if (dto.NumeroFile < 1 || dto.NumeroFile > 50 || dto.PostiPerFila < 1 || dto.PostiPerFila > 50)
        {
            return Results.BadRequest(new { error = "Dimensioni sala non valide" });
        }

        var cinemaExists = await db.Cinemas.AnyAsync(c => c.Id == dto.CinemaId);
        if (!cinemaExists)
        {
            return Results.BadRequest(new { error = "Cinema non trovato" });
        }

        var duplicate = await db.Sale.AnyAsync(s => s.CinemaId == dto.CinemaId && s.NumeroProgressivo == dto.NumeroProgressivo && (!currentId.HasValue || s.Id != currentId.Value));
        if (duplicate)
        {
            return Results.Conflict(new { error = "Numero sala gia usato nel cinema selezionato" });
        }

        return null;
    }
}
