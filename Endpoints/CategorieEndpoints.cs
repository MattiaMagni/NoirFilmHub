using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Endpoints;

public static class CategorieEndpoints
{
    public static RouteGroupBuilder MapCategorie(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (FilmDbContext db) =>
        {
            var categorie = await db.Categorie.AsNoTracking().OrderBy(c => c.Nome).ToListAsync();
            return Results.Ok(categorie);
        }).AllowAnonymous();

        group.MapGet("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var categoria = await db.Categorie.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            return categoria is null ? Results.NotFound() : Results.Ok(categoria);
        }).AllowAnonymous();

        group.MapGet("/{id:int}/films", async (int id, FilmDbContext db) =>
        {
            var categoria = await db.Categorie.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (categoria is null)
            {
                return Results.NotFound();
            }

            var films = await db.FilmCategorie
                .AsNoTracking()
                .Include(fc => fc.Film)
                .Where(fc => fc.CategoriaId == id)
                .Select(fc => fc.Film)
                .ToListAsync();

            return Results.Ok(films);
        }).AllowAnonymous();

        group.MapPost("/", async (CategoriaUpsertDTO dto, FilmDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Nome))
            {
                return Results.BadRequest(new { error = "Nome categoria obbligatorio" });
            }

            var normalizedName = dto.Nome.Trim();
            var exists = await db.Categorie.AnyAsync(c => c.Nome == normalizedName);
            if (exists)
            {
                return Results.Conflict(new { error = "Categoria gia esistente" });
            }

            var categoria = new Categoria
            {
                Nome = normalizedName,
                Descrizione = dto.Descrizione?.Trim()
            };

            db.Categorie.Add(categoria);
            await db.SaveChangesAsync();
            return Results.Created($"/categorie/{categoria.Id}", categoria);
        }).RequireAuthorization("AdminOnly");

        group.MapPut("/{id:int}", async (int id, CategoriaUpsertDTO dto, FilmDbContext db) =>
        {
            var categoria = await db.Categorie.FindAsync(id);
            if (categoria is null)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(dto.Nome))
            {
                return Results.BadRequest(new { error = "Nome categoria obbligatorio" });
            }

            var normalizedName = dto.Nome.Trim();
            var exists = await db.Categorie.AnyAsync(c => c.Nome == normalizedName && c.Id != id);
            if (exists)
            {
                return Results.Conflict(new { error = "Categoria gia esistente" });
            }

            categoria.Nome = normalizedName;
            categoria.Descrizione = dto.Descrizione?.Trim();
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        group.MapDelete("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var categoria = await db.Categorie.FindAsync(id);
            if (categoria is null)
            {
                return Results.NotFound();
            }

            db.Categorie.Remove(categoria);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        return group;
    }
}
