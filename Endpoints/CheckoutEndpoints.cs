using System.Security.Claims;
using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;
using FilmAPI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Endpoints;

public static class CheckoutEndpoints
{
    public static RouteGroupBuilder MapCheckout(this RouteGroupBuilder group)
    {
        group.MapGet("/seats/{proiezioneId:int}", async (int proiezioneId, ClaimsPrincipal user, FilmDbContext db) =>
        {
            TryGetUserId(user, out var userId);

            var show = await db.Proiezioni
                .AsNoTracking()
                .Include(p => p.Sala)
                .FirstOrDefaultAsync(p => p.Id == proiezioneId);
            if (show is null)
            {
                return Results.NotFound();
            }

            var now = DateTime.UtcNow;

            var soldRows = await db.Prenotazioni
                .AsNoTracking()
                .Where(p => p.ProiezioneId == proiezioneId && p.Stato != "Annullata")
                .Select(p => p.PostiSelezionati)
                .ToListAsync();

            var sold = ExpandSeats(soldRows);

            var locks = await db.SeatLocks
                .AsNoTracking()
                .Where(l => l.ProiezioneId == proiezioneId && l.ExpiresAtUtc > now)
                .ToListAsync();

            var soldSet = sold.ToHashSet();
            var myLocks = userId > 0 ? locks.Where(l => l.UtenteId == userId).Select(l => l.PostoCodice).ToHashSet() : new HashSet<string>();
            var lockedByOthers = locks.Where(l => l.UtenteId != userId).Select(l => l.PostoCodice).ToHashSet();
            var vipSeats = SeatPricingUtils.GetVipSeats(show.Sala?.NumeroFile ?? 10, show.Sala?.PostiPerFila ?? 12, show.Sala?.MappaPostiJson);

            return Results.Ok(new
            {
                ProiezioneId = proiezioneId,
                SalaId = show.SalaId,
                PrezzoBase = show.PrezzoBase,
                VipSupplement = SeatPricingUtils.VipSupplement,
                NumeroFile = show.Sala?.NumeroFile ?? 10,
                PostiPerFila = show.Sala?.PostiPerFila ?? 12,
                MappaPostiJson = show.Sala?.MappaPostiJson ?? string.Empty,
                VipSeats = vipSeats,
                Sold = soldSet,
                LockedByOthers = lockedByOthers,
                MyLocks = myLocks,
                LockExpiresAtUtc = locks.Where(l => l.UtenteId == userId).OrderByDescending(l => l.ExpiresAtUtc).Select(l => (DateTime?)l.ExpiresAtUtc).FirstOrDefault()
            });
        }).AllowAnonymous();

        group.MapPost("/locks", async (SeatLockCreateDTO dto, ClaimsPrincipal user, FilmDbContext db) =>
        {
            if (!TryGetUserId(user, out var userId))
            {
                return Results.Unauthorized();
            }

            if (dto.ProiezioneId <= 0 || dto.Posti.Count == 0)
            {
                return Results.BadRequest(new { error = "Dati lock non validi" });
            }

            var show = await db.Proiezioni
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == dto.ProiezioneId);
            if (show is null)
            {
                return Results.BadRequest(new { error = "Show non trovato" });
            }

            var now = DateTime.UtcNow;
            var ttl = Math.Clamp(dto.LockMinutes ?? 10, 8, 10);
            var expiresAt = now.AddMinutes(ttl);

            var normalizedSeats = dto.Posti
                .Select(x => x.Trim().ToUpperInvariant())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            if (normalizedSeats.Count == 0)
            {
                return Results.BadRequest(new { error = "Nessun posto valido" });
            }

            var soldRows = await db.Prenotazioni
                .AsNoTracking()
                .Where(p => p.ProiezioneId == dto.ProiezioneId && p.Stato != "Annullata")
                .Select(p => p.PostiSelezionati)
                .ToListAsync();

            var soldSeats = ExpandSeats(soldRows);

            var soldSet = soldSeats.ToHashSet();
            var lockedByOthers = await db.SeatLocks
                .Where(l => l.ProiezioneId == dto.ProiezioneId && l.ExpiresAtUtc > now && l.UtenteId != userId)
                .Select(l => l.PostoCodice)
                .ToListAsync();
            var lockedSet = lockedByOthers.ToHashSet();

            var blocked = normalizedSeats.Where(seat => soldSet.Contains(seat) || lockedSet.Contains(seat)).ToList();
            if (blocked.Count > 0)
            {
                return Results.Conflict(new { error = "Posti non disponibili", posti = blocked });
            }

		var existingUserLocks = await db.SeatLocks
		.Where(l => l.ProiezioneId == dto.ProiezioneId && l.UtenteId == userId)
		.ToListAsync();

		var existingSet = existingUserLocks.Select(x => x.PostoCodice).ToHashSet();
		var normalizedSet = normalizedSeats.ToHashSet();

		foreach (var lockItem in existingUserLocks)
		{
			if (normalizedSet.Contains(lockItem.PostoCodice))
			{
				lockItem.ExpiresAtUtc = expiresAt;
			}
			else
			{
				db.SeatLocks.Remove(lockItem);
			}
		}

		var toCreate = normalizedSeats.Where(seat => !existingSet.Contains(seat)).ToList();
            foreach (var seat in toCreate)
            {
                db.SeatLocks.Add(new SeatLock
                {
                    ProiezioneId = dto.ProiezioneId,
                    UtenteId = userId,
                    PostoCodice = seat,
                    CreatedAtUtc = now,
                    ExpiresAtUtc = expiresAt
                });
            }

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                ProiezioneId = dto.ProiezioneId,
                Posti = normalizedSeats,
                ExpiresAtUtc = expiresAt,
                LockMinutes = ttl
            });
        }).RequireAuthorization();

        group.MapDelete("/locks/{proiezioneId:int}", async (int proiezioneId, ClaimsPrincipal user, FilmDbContext db) =>
        {
            if (!TryGetUserId(user, out var userId))
            {
                return Results.Unauthorized();
            }

            var locks = await db.SeatLocks
                .Where(l => l.ProiezioneId == proiezioneId && l.UtenteId == userId)
                .ToListAsync();

            db.SeatLocks.RemoveRange(locks);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization();

        return group;
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out int userId)
    {
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out userId);
    }

    private static List<string> ExpandSeats(IEnumerable<string?> seatRows)
    {
        return seatRows
            .Where(row => !string.IsNullOrWhiteSpace(row))
            .SelectMany(row => row!.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(seat => seat.Trim().ToUpperInvariant())
                .Where(seat => !string.IsNullOrWhiteSpace(seat)))
            .Distinct()
            .ToList();
    }
}
