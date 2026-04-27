using System.Security.Claims;
using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Endpoints;

public static class AuthEndpoints
{
    private static readonly HashSet<string> RuoliValidi =
    [
        Model.RuoloUtente.Admin,
        Model.RuoloUtente.PowerUser,
        Model.RuoloUtente.Utente
    ];

    public static RouteGroupBuilder MapAuth(this RouteGroupBuilder group)
    {
        group.MapPost("/register", async (RegisterRequestDTO dto, AuthService authService) =>
        {
            var (success, error, utente) = await authService.RegisterAsync(dto);
            if (!success || utente is null)
            {
                return Results.BadRequest(new { error });
            }

            return Results.Created($"/auth/users/{utente.Id}", AuthService.ToUtenteDto(utente));
        }).AllowAnonymous();

        group.MapPost("/login", async (LoginRequestDTO dto, AuthService authService) =>
        {
            var (success, _, response) = await authService.LoginAsync(dto);
            if (!success || response is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(response);
        }).AllowAnonymous();

        group.MapPost("/refresh", async (RefreshTokenRequestDTO dto, AuthService authService) =>
        {
            var (success, _, response) = await authService.RefreshAsync(dto.RefreshToken);
            if (!success || response is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(response);
        }).AllowAnonymous();

        group.MapPost("/logout", async (ClaimsPrincipal user, AuthService authService) =>
        {
            if (!TryGetUserId(user, out var userId))
            {
                return Results.Unauthorized();
            }

            await authService.LogoutAsync(userId);
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapGet("/me", async (ClaimsPrincipal user, FilmDbContext db) =>
        {
            if (!TryGetUserId(user, out var userId))
            {
                return Results.Unauthorized();
            }

            var utente = await db.Utenti.FindAsync(userId);
            if (utente is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(AuthService.ToUtenteDto(utente));
        }).RequireAuthorization();

        group.MapPut("/me", async (ClaimsPrincipal user, UtenteUpdateDTO dto, FilmDbContext db) =>
        {
            if (!TryGetUserId(user, out var userId))
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(dto.Nome) || string.IsNullOrWhiteSpace(dto.Cognome))
            {
                return Results.BadRequest(new { error = "Nome e cognome sono obbligatori" });
            }

            var utente = await db.Utenti.FindAsync(userId);
            if (utente is null)
            {
                return Results.NotFound();
            }

            utente.Nome = dto.Nome.Trim();
            utente.Cognome = dto.Cognome.Trim();
            utente.Telefono = dto.Telefono?.Trim() ?? string.Empty;
            await db.SaveChangesAsync();

            return Results.Ok(AuthService.ToUtenteDto(utente));
        }).RequireAuthorization();

        group.MapGet("/me/cinema-preferito", async (ClaimsPrincipal user, FilmDbContext db) =>
        {
            if (!TryGetUserId(user, out var userId))
            {
                return Results.Unauthorized();
            }

            var utente = await db.Utenti.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (utente is null)
            {
                return Results.NotFound();
            }

            if (!utente.CinemaPreferitoId.HasValue)
            {
                return Results.Ok(new { cinemaPreferitoId = (int?)null });
            }

            var cinema = await db.Cinemas.AsNoTracking().FirstOrDefaultAsync(c => c.Id == utente.CinemaPreferitoId.Value);
            if (cinema is null)
            {
                return Results.Ok(new { cinemaPreferitoId = (int?)null });
            }

            return Results.Ok(new
            {
                cinemaPreferitoId = cinema.Id,
                cinema.Nome,
                cinema.Citta,
                cinema.Indirizzo,
                cinema.CodiceLocale
            });
        }).RequireAuthorization();

        group.MapPut("/me/cinema-preferito", async (ClaimsPrincipal user, CinemaPreferitoUpdateDTO dto, FilmDbContext db) =>
        {
            if (!TryGetUserId(user, out var userId))
            {
                return Results.Unauthorized();
            }

            var cinema = await db.Cinemas.AsNoTracking().FirstOrDefaultAsync(c => c.Id == dto.CinemaId);
            if (cinema is null)
            {
                return Results.BadRequest(new { error = "Cinema non trovato" });
            }

            var utente = await db.Utenti.FirstOrDefaultAsync(u => u.Id == userId);
            if (utente is null)
            {
                return Results.NotFound();
            }

            utente.CinemaPreferitoId = cinema.Id;
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                cinemaPreferitoId = cinema.Id,
                cinema.Nome,
                cinema.Citta,
                cinema.Indirizzo,
                cinema.CodiceLocale
            });
        }).RequireAuthorization();

        group.MapGet("/utenti", async (FilmDbContext db) =>
        {
            var utenti = await db.Utenti
                .AsNoTracking()
                .Select(u => AuthService.ToUtenteDto(u))
                .ToListAsync();

            return Results.Ok(utenti);
        }).RequireAuthorization("AdminOnly");

        group.MapPut("/utenti/{id:int}/ruolo", async (int id, UpdateRuoloDTO dto, FilmDbContext db) =>
        {
            var ruolo = (dto.Ruolo ?? string.Empty).Trim().ToLowerInvariant();
            if (!RuoliValidi.Contains(ruolo))
            {
                return Results.BadRequest(new { error = "Ruolo non valido" });
            }

            var utente = await db.Utenti.FindAsync(id);
            if (utente is null)
            {
                return Results.NotFound();
            }

            utente.Ruolo = ruolo;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        group.MapDelete("/utenti/{id:int}", async (int id, ClaimsPrincipal user, FilmDbContext db) =>
        {
            if (!TryGetUserId(user, out var currentUserId))
            {
                return Results.Unauthorized();
            }

            if (id == currentUserId)
            {
                return Results.BadRequest(new { error = "Non puoi eliminare il tuo account" });
            }

            var utente = await db.Utenti.FindAsync(id);
            if (utente is null)
            {
                return Results.NotFound();
            }

            db.Utenti.Remove(utente);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        return group;
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out int userId)
    {
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out userId);
    }
}
