using System.Security.Claims;
using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;
using FilmAPI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Endpoints;

public static class PrenotazioniEndpoints
{
    public static RouteGroupBuilder MapPrenotazioni(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (FilmDbContext db) =>
        {
            var prenotazioni = await QueryPrenotazioni(db).ToListAsync();
            return Results.Ok(prenotazioni);
        }).RequireAuthorization("AdminOnly");

        group.MapGet("/mie", async (ClaimsPrincipal user, FilmDbContext db) =>
        {
            if (!TryGetUserId(user, out var userId))
            {
                return Results.Unauthorized();
            }

            var prenotazioni = await QueryPrenotazioni(db)
                .Where(p => p.UtenteId == userId)
                .ToListAsync();
            return Results.Ok(prenotazioni);
        }).RequireAuthorization();

        group.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, FilmDbContext db) =>
        {
            if (!TryGetUserId(user, out var userId))
            {
                return Results.Unauthorized();
            }

            var role = user.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            var prenotazione = await QueryPrenotazioni(db)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prenotazione is null)
            {
                return Results.NotFound();
            }

            if (role != RuoloUtente.Admin && prenotazione.UtenteId != userId)
            {
                return Results.Forbid();
            }

            return Results.Ok(prenotazione);
        }).RequireAuthorization();

        group.MapPost("/", async (PrenotazioneCreateDTO dto, ClaimsPrincipal user, FilmDbContext db) =>
        {
            if (!TryGetUserId(user, out var userId))
            {
                return Results.Unauthorized();
            }

            if (dto.NumeroPosti <= 0)
            {
                return Results.BadRequest(new { error = "Numero posti deve essere > 0" });
            }

            if (dto.NumeroPosti > 10)
            {
                return Results.BadRequest(new { error = "Numero posti massimo acquistabile: 10" });
            }

            var proiezione = await db.Proiezioni
                .AsNoTracking()
                .Include(p => p.Cinema)
                .FirstOrDefaultAsync(p => p.Id == dto.ProiezioneId);
            if (proiezione is null)
            {
                return Results.BadRequest(new { error = "Proiezione non trovata" });
            }

            var postiGiaPrenotati = await db.Prenotazioni
                .Where(p => p.ProiezioneId == dto.ProiezioneId && p.Stato != "Annullata")
                .SumAsync(p => (int?)p.NumeroPosti) ?? 0;

            var postiDisponibili = proiezione.Cinema.Capienza - postiGiaPrenotati;
            if (dto.NumeroPosti > postiDisponibili)
            {
                return Results.BadRequest(new
                {
                    error = $"Posti insufficienti: disponibili {Math.Max(0, postiDisponibili)} su {proiezione.Cinema.Capienza}"
                });
            }

            var prenotazione = new Prenotazione
            {
                UtenteId = userId,
                ProiezioneId = dto.ProiezioneId,
                NumeroPosti = dto.NumeroPosti,
                PostiSelezionati = dto.PostiSelezionati?.Trim() ?? string.Empty,
                TotalePrezzo = decimal.Round(dto.NumeroPosti * proiezione.PrezzoBase, 2),
                ImportoCartaUsato = decimal.Round(dto.NumeroPosti * proiezione.PrezzoBase, 2),
                CodiceAcquisto = $"NFH-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                DataPrenotazione = DateTime.UtcNow,
                Stato = "Confermata"
            };

            db.Prenotazioni.Add(prenotazione);
            await db.SaveChangesAsync();

            return Results.Created($"/prenotazioni/{prenotazione.Id}", new { prenotazione.Id });
        }).RequireAuthorization();

        group.MapPut("/{id:int}/annulla", async (int id, ClaimsPrincipal user, FilmDbContext db, EmailService emailService) =>
        {
            if (!TryGetUserId(user, out var userId))
            {
                return Results.Unauthorized();
            }

            var role = user.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            var prenotazione = await db.Prenotazioni
                .Include(p => p.Utente)
                .Include(p => p.Proiezione)
                .ThenInclude(pr => pr.Film)
                .Include(p => p.Proiezione)
                .ThenInclude(pr => pr.Cinema)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (prenotazione is null)
            {
                return Results.NotFound();
            }

            var canCancelAny = role == RuoloUtente.Admin || role == RuoloUtente.PowerUser;
            if (!canCancelAny && prenotazione.UtenteId != userId)
            {
                return Results.Forbid();
            }

            if (prenotazione.Stato == "Annullata")
            {
                return Results.BadRequest(new { error = "Prenotazione gia annullata" });
            }

            string? giftCardCodice = null;
            decimal refundAmount = 0;

            if (prenotazione.Stato == "Confermata")
            {
                refundAmount = decimal.Round(prenotazione.TotalePrezzo * 0.5m, 2);
                if (refundAmount > 0)
                {
                    giftCardCodice = "NFH-RF-" + Guid.NewGuid().ToString("N")[..8].ToUpper();
                    db.GiftCards.Add(new GiftCard
                    {
                        Codice = giftCardCodice, ImportoIniziale = refundAmount, SaldoResiduo = refundAmount,
                        UtenteAcquirenteId = prenotazione.UtenteId,
                        Messaggio = $"Rimborso 50% per prenotazione annullata (#{prenotazione.Id})",
                        Stato = "Active", CreatoIl = DateTime.UtcNow, Scadenza = DateTime.UtcNow.AddYears(1)
                    });
                }
            }

            prenotazione.Stato = "Annullata";
            await db.SaveChangesAsync();

            if (giftCardCodice != null && prenotazione.Utente != null)
            {
                var filmTitolo = prenotazione.Proiezione?.Film?.Titolo ?? "N/A";
                var cinemaNome = prenotazione.Proiezione?.Cinema?.Nome ?? "N/A";
                _ = emailService.SendCancellationRefundEmail(
                    prenotazione.Utente.Email,
                    prenotazione.Utente.Nome,
                    prenotazione.Id,
                    prenotazione.CodiceAcquisto,
                    filmTitolo,
                    cinemaNome,
                    refundAmount,
                    giftCardCodice
                );
            }

            return Results.Ok(new { giftCardCodice, refundAmount, stato = prenotazione.Stato });
        }).RequireAuthorization();

        return group;
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out int userId)
    {
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out userId);
    }

    private static IQueryable<PrenotazioneDTO> QueryPrenotazioni(FilmDbContext db)
    {
        return db.Prenotazioni
            .AsNoTracking()
            .Include(p => p.Proiezione)
            .ThenInclude(pr => pr.Film)
            .Include(p => p.Proiezione)
            .ThenInclude(pr => pr.Cinema)
            .Include(p => p.Proiezione)
            .ThenInclude(pr => pr.Sala)
            .Select(p => new PrenotazioneDTO
            {
                Id = p.Id,
                ProiezioneId = p.ProiezioneId,
                FilmId = p.Proiezione.FilmId,
                TitoloFilm = p.Proiezione.Film.Titolo,
                CinemaId = p.Proiezione.CinemaId,
                NomeCinema = p.Proiezione.Cinema.Nome,
                SalaId = p.Proiezione.SalaId ?? 0,
                NomeSala = p.Proiezione.Sala != null
                    ? (string.IsNullOrWhiteSpace(p.Proiezione.Sala.Nome)
                        ? $"SALA {p.Proiezione.Sala.NumeroProgressivo}"
                        : p.Proiezione.Sala.Nome)
                    : "SALA",
                TipologiaSala = p.Proiezione.Sala != null ? p.Proiezione.Sala.Tipologia : "2D",
                Data = p.Proiezione.Data,
                Ora = p.Proiezione.Ora,
                NumeroPosti = p.NumeroPosti,
                PostiSelezionati = p.PostiSelezionati,
                TotalePrezzo = p.TotalePrezzo,
                ImportoCartaUsato = p.ImportoCartaUsato,
                CodiceAcquisto = p.CodiceAcquisto,
                Validato = p.Validato,
                ValidatoAtUtc = p.ValidatoAtUtc,
                Stato = p.Stato,
                DataPrenotazione = p.DataPrenotazione,
                UtenteId = p.UtenteId
            });
    }
}
