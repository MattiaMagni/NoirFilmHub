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
            var (success, error, response) = await authService.LoginAsync(dto);
            if (!success || response is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(response);
        }).AllowAnonymous();

        group.MapPost("/refresh", async (RefreshTokenRequestDTO dto, AuthService authService) =>
        {
            var (success, error, response) = await authService.RefreshAsync(dto.RefreshToken);
            if (!success || response is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(response);
        }).AllowAnonymous();

        group.MapPost("/logout", async (ClaimsPrincipal user, AuthService authService) =>
        {
            var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId))
            {
                return Results.Unauthorized();
            }

            await authService.LogoutAsync(userId);
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapGet("/me", async (ClaimsPrincipal user, FilmDbContext db) =>
        {
            var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId))
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
            var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId))
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
            if (ruolo != Model.RuoloUtente.Admin && ruolo != Model.RuoloUtente.PowerUser && ruolo != Model.RuoloUtente.Utente)
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

        return group;
    }
}
