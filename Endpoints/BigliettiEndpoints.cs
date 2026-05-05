using System.Security.Claims;
using FilmAPI.Data;
using FilmAPI.Model;
using FilmAPI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Endpoints;

public static class BigliettiEndpoints
{
    public static RouteGroupBuilder MapBiglietti(this RouteGroupBuilder group)
    {
        group.MapGet("/{codiceAcquisto}", async (string codiceAcquisto, FilmDbContext db) =>
        {
            var ticket = await QueryTicketByCode(db, codiceAcquisto).FirstOrDefaultAsync();
            return ticket is null ? Results.NotFound() : Results.Ok(ticket);
        }).RequireAuthorization();

        group.MapGet("/validate/{codiceAcquisto}", async (string codiceAcquisto, FilmDbContext db) =>
        {
            var ticket = await QueryTicketByCode(db, codiceAcquisto).FirstOrDefaultAsync();
            return ticket is null ? Results.NotFound() : Results.Ok(ticket);
        }).AllowAnonymous();

        group.MapPost("/{codiceAcquisto}/validate", async (string codiceAcquisto, ClaimsPrincipal user, FilmDbContext db) =>
        {
            if (!TryGetUserId(user, out var userId))
            {
                return Results.Unauthorized();
            }

            var operatore = await db.Utenti.FirstOrDefaultAsync(u => u.Id == userId);
            if (operatore is null)
            {
                return Results.Unauthorized();
            }

            var booking = await db.Prenotazioni
                .Include(p => p.Proiezione)
                .ThenInclude(p => p.Sala)
                .FirstOrDefaultAsync(p => p.CodiceAcquisto == codiceAcquisto);

            if (booking is null)
            {
                return Results.NotFound();
            }

            if (booking.Validato)
            {
                return Results.Conflict(new { error = "Biglietto gia validato" });
            }

            if (!operatore.CinemaPreferitoId.HasValue)
            {
                return Results.BadRequest(new { error = "Operatore senza cinema associato" });
            }

            var cinemaShowId = booking.Proiezione.CinemaId;
            if (operatore.CinemaPreferitoId.Value != cinemaShowId)
            {
                return Results.Forbid();
            }

            booking.Validato = true;
            booking.ValidatoAtUtc = DateTime.UtcNow;
            booking.ValidatoDaUtenteId = operatore.Id;
            booking.CinemaValidazioneId = operatore.CinemaPreferitoId;
            booking.Stato = "Validata";
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                booking.CodiceAcquisto,
                booking.Validato,
                booking.ValidatoAtUtc,
                booking.CinemaValidazioneId
            });
        }).RequireAuthorization("AdminOrPowerUser");

        group.MapGet("/{codiceAcquisto}/pdf", async (string codiceAcquisto, ClaimsPrincipal user, FilmDbContext db, TicketPdfService pdfService) =>
        {
            if (!TryGetUserId(user, out var userId))
            {
                return Results.Unauthorized();
            }

            var booking = await db.Prenotazioni
                .Include(p => p.Utente)
                .Include(p => p.Proiezione)
                .ThenInclude(p => p.Film)
                .Include(p => p.Proiezione)
                .ThenInclude(p => p.Cinema)
                .Include(p => p.Proiezione)
                .ThenInclude(p => p.Sala)
                .FirstOrDefaultAsync(p => p.CodiceAcquisto == codiceAcquisto);

            if (booking is null)
            {
                return Results.NotFound();
            }

            var requesterRole = GetRole(user);
            var isAdminOrPower = requesterRole == RuoloUtente.Admin || requesterRole == RuoloUtente.PowerUser;
            if (!isAdminOrPower && booking.UtenteId != userId)
            {
                return Results.Forbid();
            }

            var validateBaseUrl = Environment.GetEnvironmentVariable("APP_BASE_URL") ?? "http://localhost:5001";
            var pdfBytes = pdfService.GenerateOrderPdf(booking, validateBaseUrl);
            var fileName = $"ticket-{booking.CodiceAcquisto}.pdf";
            return Results.File(pdfBytes, "application/pdf", fileName);
        }).RequireAuthorization();

        return group;
    }

    private static IQueryable<object> QueryTicketByCode(FilmDbContext db, string code)
    {
        return db.Prenotazioni
            .AsNoTracking()
            .Include(p => p.Proiezione)
            .ThenInclude(pr => pr.Film)
            .Include(p => p.Proiezione)
            .ThenInclude(pr => pr.Cinema)
            .Where(p => p.CodiceAcquisto == code)
            .Select(p => new
            {
                p.Id,
                p.CodiceAcquisto,
                p.NumeroPosti,
                p.PostiSelezionati,
                p.TotalePrezzo,
                p.ImportoCartaUsato,
                p.Validato,
                p.ValidatoAtUtc,
                p.Stato,
                Film = p.Proiezione.Film.Titolo,
                Data = p.Proiezione.Data,
                Ora = p.Proiezione.Ora,
                Cinema = p.Proiezione.Cinema.Nome,
                CinemaId = p.Proiezione.CinemaId
            });
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out int userId)
    {
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out userId);
    }

    private static string? GetRole(ClaimsPrincipal user)
    {
        var role = user.FindFirstValue(ClaimTypes.Role);
        return string.IsNullOrWhiteSpace(role) ? null : role.Trim().ToLowerInvariant();
    }
}
