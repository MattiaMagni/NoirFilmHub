using System.Security.Claims;
using System.Text.Json;
using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;
using FilmAPI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
        // --- Existing endpoints ---

        group.MapPost("/register", async (RegisterRequestDTO dto, AuthService authService) =>
        {
            var (success, error, utente) = await authService.RegisterAsync(dto);
            if (!success || utente is null)
                return Results.BadRequest(new { error });

            return Results.Created($"/auth/users/{utente.Id}", AuthService.ToUtenteDto(utente));
        }).AllowAnonymous();

        group.MapPost("/login", async (LoginRequestDTO dto, AuthService authService,
            HttpContext httpContext) =>
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            var ua = httpContext.Request.Headers.UserAgent.ToString();
            var (success, error, response) = await authService.LoginAsync(dto, ip, ua);
            if (!success || response is null)
                return Results.BadRequest(new { error });

            return Results.Ok(response);
        }).AllowAnonymous();

        group.MapPost("/refresh", async (RefreshTokenRequestDTO dto, AuthService authService) =>
        {
            var (success, _, response) = await authService.RefreshAsync(dto.RefreshToken);
            if (!success || response is null)
                return Results.Unauthorized();

            return Results.Ok(response);
        }).AllowAnonymous();

        group.MapPost("/logout", async (ClaimsPrincipal user, LogoutRequestDTO? dto, AuthService authService) =>
        {
            if (!TryGetUserId(user, out var userId))
                return Results.Unauthorized();

            await authService.LogoutAsync(userId, dto?.AllDevices ?? false);
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapGet("/me", async (ClaimsPrincipal user, FilmDbContext db) =>
        {
            if (!TryGetUserId(user, out var userId))
                return Results.Unauthorized();

            var utente = await db.Utenti
                .Include(u => u.ExternalLogins)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (utente is null) return Results.NotFound();

            return Results.Ok(AuthService.ToUtenteDto(utente));
        }).RequireAuthorization();

        group.MapPut("/me", async (ClaimsPrincipal user, UtenteUpdateDTO dto, FilmDbContext db) =>
        {
            if (!TryGetUserId(user, out var userId))
                return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.Nome) || string.IsNullOrWhiteSpace(dto.Cognome))
                return Results.BadRequest(new { error = "Nome e cognome sono obbligatori" });

            var utente = await db.Utenti.FindAsync(userId);
            if (utente is null) return Results.NotFound();

            utente.Nome = dto.Nome.Trim();
            utente.Cognome = dto.Cognome.Trim();
            utente.Telefono = dto.Telefono?.Trim() ?? string.Empty;
            await db.SaveChangesAsync();

            return Results.Ok(AuthService.ToUtenteDto(utente));
        }).RequireAuthorization();

        group.MapGet("/me/cinema-preferito", async (ClaimsPrincipal user, FilmDbContext db) =>
        {
            if (!TryGetUserId(user, out var userId))
                return Results.Unauthorized();

            var utente = await db.Utenti.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (utente is null) return Results.NotFound();

            if (!utente.CinemaPreferitoId.HasValue)
                return Results.Ok(new { cinemaPreferitoId = (int?)null });

            var cinema = await db.Cinemas.AsNoTracking().FirstOrDefaultAsync(c => c.Id == utente.CinemaPreferitoId.Value);
            if (cinema is null)
                return Results.Ok(new { cinemaPreferitoId = (int?)null });

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
                return Results.Unauthorized();

            var cinema = await db.Cinemas.AsNoTracking().FirstOrDefaultAsync(c => c.Id == dto.CinemaId);
            if (cinema is null)
                return Results.BadRequest(new { error = "Cinema non trovato" });

            var utente = await db.Utenti.FirstOrDefaultAsync(u => u.Id == userId);
            if (utente is null) return Results.NotFound();

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

        // --- Password Management ---

        group.MapPost("/me/change-password", async (ClaimsPrincipal user, ChangePasswordDTO dto,
            AuthService authService, HttpContext httpContext) =>
        {
            if (!TryGetUserId(user, out var userId))
                return Results.Unauthorized();

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error, response) = await authService.ChangePasswordAsync(
                userId, dto.CurrentPassword, dto.NewPassword, ip);
            if (!success || response is null)
                return Results.BadRequest(new { error });

            return Results.Ok(response);
        }).RequireAuthorization();

        group.MapPost("/forgot-password", async (ForgotPasswordDTO dto, AuthService authService) =>
        {
            var (success, message, resetToken, resetEmail) = await authService.ForgotPasswordAsync(dto.Email);
            var result = new { message };
            if (success && resetToken != null)
                return Results.Ok(new { message, token = resetToken, email = resetEmail });
            return Results.Ok(result);
        }).AllowAnonymous();

        group.MapPost("/reset-password", async (ResetPasswordDTO dto, AuthService authService) =>
        {
            var (success, error, response) = await authService.ResetPasswordAsync(
                dto.Email, dto.Token, dto.NewPassword);
            if (!success || response is null)
                return Results.BadRequest(new { error });

            return Results.Ok(response);
        }).AllowAnonymous();

        group.MapPost("/me/request-password-setup", async (ClaimsPrincipal user, AuthService authService) =>
        {
            if (!TryGetUserId(user, out var userId))
                return Results.Unauthorized();

            var (success, error) = await authService.RequestPasswordSetupAsync(userId);
            if (!success)
                return Results.BadRequest(new { error });

            return Results.Ok(new { message = "Email di setup inviata. Controlla la tua casella di posta." });
        }).RequireAuthorization();

        group.MapPost("/setup-password", async (SetupPasswordDTO dto, AuthService authService) =>
        {
            var (success, error, response) = await authService.SetupPasswordAsync(
                dto.Email, dto.Token, dto.NewPassword);
            if (!success || response is null)
                return Results.BadRequest(new { error });

            return Results.Ok(response);
        }).AllowAnonymous();

        // --- Session Management ---

        group.MapPost("/revoke-all-sessions", async (ClaimsPrincipal user, AuthService authService) =>
        {
            if (!TryGetUserId(user, out var userId))
                return Results.Unauthorized();

            await authService.RevokeAllSessionsAsync(userId);
            return Results.NoContent();
        }).RequireAuthorization();

        // --- External Logins ---

        group.MapGet("/me/external-logins", async (ClaimsPrincipal user, SocialAuthService socialAuth) =>
        {
            if (!TryGetUserId(user, out var userId))
                return Results.Unauthorized();

            var logins = await socialAuth.GetExternalLoginsAsync(userId);
            return Results.Ok(logins);
        }).RequireAuthorization();

        group.MapDelete("/me/external-logins/{id:int}", async (ClaimsPrincipal user, int id,
            SocialAuthService socialAuth, SecurityAuditService audit, HttpContext httpContext) =>
        {
            if (!TryGetUserId(user, out var userId))
                return Results.Unauthorized();

            var success = await socialAuth.UnlinkExternalLoginAsync(userId, id);
            if (!success)
                return Results.BadRequest(new { error = "Impossibile scollegare il provider. Devi avere almeno un metodo di accesso." });

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            await audit.LogEventAsync(userId, "SocialUnlinked", null, ip);
            return Results.NoContent();
        }).RequireAuthorization();

        // --- Social Login ---

        group.MapGet("/external/{provider}", async (string provider, string? returnUrl, string? mode,
            SocialAuthService socialAuth, HttpContext httpContext) =>
        {
            var apiBaseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var (authUrl, error) = await socialAuth.InitiateAsync(provider, returnUrl, mode, apiBaseUrl);
            if (authUrl is null)
                return Results.BadRequest(new { error });

            return Results.Ok(new ExternalAuthInitiateDTO { AuthorizationUrl = authUrl });
        }).AllowAnonymous();

        group.MapGet("/external/callback", async (string code, string state,
            SocialAuthService socialAuth, HttpContext httpContext) =>
        {
            var apiBaseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var frontendBaseUrl = Environment.GetEnvironmentVariable("APP_BASE_URL")
                ?? "http://localhost:5001";
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            var ua = httpContext.Request.Headers.UserAgent.ToString();
            var (success, error, redirectUrl) = await socialAuth.HandleCallbackAsync(
                code, state, apiBaseUrl, ip, ua, frontendBaseUrl: frontendBaseUrl);

            if (!success)
            {
                var errorFragment = Uri.EscapeDataString(error ?? "Errore di autenticazione");
                return Results.Redirect(
                    $"{frontendBaseUrl}/social-login-complete.html#error={errorFragment}");
            }

            return Results.Redirect(redirectUrl!);
        }).AllowAnonymous();

        // --- Admin User Management ---

        group.MapGet("/admin/utenti", async (
            string? search, string? ruolo, bool? isDisabled, bool? hasLocalCredentials,
            int page, int pageSize, string? orderBy, string? orderDirection,
            AuthService authService) =>
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 20;
            var result = await authService.GetUsersAsync(
                search, ruolo, isDisabled, hasLocalCredentials,
                page, pageSize, orderBy, orderDirection);
            return Results.Ok(result);
        }).RequireAuthorization("AdminOnly");

        group.MapGet("/admin/utenti/{id:int}", async (int id, AuthService authService) =>
        {
            var utente = await authService.GetUserDetailAsync(id);
            if (utente is null) return Results.NotFound();
            return Results.Ok(utente);
        }).RequireAuthorization("AdminOnly");

        group.MapPut("/admin/utenti/{id:int}/ruolo", async (int id, UpdateRuoloDTO dto,
            ClaimsPrincipal user, AuthService authService, HttpContext httpContext) =>
        {
            if (!TryGetUserId(user, out var currentUserId))
                return Results.Unauthorized();

            var ruolo = (dto.Ruolo ?? string.Empty).Trim().ToLowerInvariant();
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error) = await authService.ChangeUserRoleAsync(id, ruolo, currentUserId, ip);
            if (!success)
                return Results.BadRequest(new { error });

            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        group.MapPut("/admin/utenti/{id:int}/disable", async (int id,
            ClaimsPrincipal user, AuthService authService) =>
        {
            if (!TryGetUserId(user, out var currentUserId))
                return Results.Unauthorized();

            var (success, error) = await authService.DisableUserAsync(id, currentUserId);
            if (!success)
                return Results.BadRequest(new { error });

            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        group.MapPut("/admin/utenti/{id:int}/enable", async (int id, AuthService authService) =>
        {
            var (success, error) = await authService.EnableUserAsync(id);
            if (!success)
                return Results.BadRequest(new { error });

            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        group.MapPost("/admin/utenti/{id:int}/force-password-reset", async (int id,
            ClaimsPrincipal user, AuthService authService) =>
        {
            if (!TryGetUserId(user, out var currentUserId))
                return Results.Unauthorized();

            var (success, error) = await authService.ForcePasswordResetAsync(id, currentUserId);
            if (!success)
                return Results.BadRequest(new { error });

            return Results.Ok(new { message = "Email di reset inviata all'utente." });
        }).RequireAuthorization("AdminOnly");

        group.MapDelete("/admin/utenti/{id:int}", async (int id,
            ClaimsPrincipal user, AuthService authService) =>
        {
            if (!TryGetUserId(user, out var currentUserId))
                return Results.Unauthorized();

            var (success, error) = await authService.DeleteUserAsync(id, currentUserId);
            if (!success)
                return Results.BadRequest(new { error });

            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        group.MapPost("/admin/invite", async (InviteUserDTO dto, AuthService authService) =>
        {
            var (success, error, utente) = await authService.InviteUserAsync(dto);
            if (!success || utente is null)
                return Results.BadRequest(new { error });

            return Results.Created($"/auth/admin/utenti/{utente.Id}",
                new { id = utente.Id, email = utente.Email, ruolo = utente.Ruolo });
        }).RequireAuthorization("AdminOnly");

        // --- Legacy compat: keep old routes working ---

        group.MapGet("/utenti", async (AuthService authService) =>
        {
            var result = await authService.GetUsersAsync(null, null, null, null, 1, 1000, "id", "asc");
            var items = result.Items.Select(u => new UtenteDTO
            {
                Id = u.Id,
                Email = u.Email,
                Nome = u.Nome,
                Cognome = u.Cognome,
                Telefono = "",
                Ruolo = u.Ruolo,
                CinemaPreferitoId = null,
                LocalCredentialsEnabled = u.LocalCredentialsEnabled,
                EmailVerified = u.EmailVerified,
                IsDisabled = u.IsDisabled,
                LastLoginAtUtc = u.LastLoginAtUtc,
                CreatedAtUtc = u.CreatedAtUtc,
                ExternalLogins = u.ExternalLogins
            }).ToList();
            return Results.Ok(items);
        }).RequireAuthorization("AdminOnly");

        group.MapPut("/utenti/{id:int}/ruolo", async (int id, UpdateRuoloDTO dto,
            ClaimsPrincipal user, AuthService authService, HttpContext httpContext) =>
        {
            if (!TryGetUserId(user, out var currentUserId))
                return Results.Unauthorized();

            var ruolo = (dto.Ruolo ?? string.Empty).Trim().ToLowerInvariant();
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error) = await authService.ChangeUserRoleAsync(id, ruolo, currentUserId, ip);
            if (!success)
                return Results.BadRequest(new { error });

            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        group.MapDelete("/utenti/{id:int}", async (int id, ClaimsPrincipal user, AuthService authService) =>
        {
            if (!TryGetUserId(user, out var currentUserId))
                return Results.Unauthorized();

            var (success, error) = await authService.DeleteUserAsync(id, currentUserId);
            if (!success)
                return Results.BadRequest(new { error });

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
