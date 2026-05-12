using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Endpoints;

public static class ProiezioniEndpoints
{
    public static RouteGroupBuilder MapProiezioni(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (FilmDbContext db, int? filmId, int? cinemaId, DateTime? day) =>
        {
            var query = db.Proiezioni
                .AsNoTracking()
                .Include(p => p.Sala)
                .AsQueryable();

            if (filmId.HasValue)
            {
                query = query.Where(p => p.FilmId == filmId.Value);
            }

            if (cinemaId.HasValue)
            {
                query = query.Where(p => p.CinemaId == cinemaId.Value);
            }

            if (day.HasValue)
            {
                var selected = day.Value.Date;
                query = query.Where(p => p.Data.Date == selected);
            }

            var items = await query.OrderBy(p => p.Data).ThenBy(p => p.Ora).ToListAsync();
            return Results.Ok(items.Select(ToDto));
        }).AllowAnonymous();

        group.MapGet("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var p = await db.Proiezioni
                .AsNoTracking()
                .Include(x => x.Sala)
                .FirstOrDefaultAsync(x => x.Id == id);
            return p is not null ? Results.Ok(ToDto(p)) : Results.NotFound();
        }).AllowAnonymous();

        group.MapPost("/", async (ProiezioneCreateDTO dto, FilmDbContext db) =>
        {
            var film = await db.Films.FindAsync(dto.FilmId);
            if (film is null)
            {
                return Results.BadRequest(new { error = "Film non trovato" });
            }

            var cinema = await db.Cinemas.FindAsync(dto.CinemaId);
            if (cinema is null)
            {
                return Results.BadRequest(new { error = "Cinema non trovato" });
            }

            var salaId = await ResolveOrCreateSalaIdAsync(db, dto.CinemaId, dto.SalaId);
            if (!salaId.HasValue)
            {
                return Results.BadRequest(new { error = "Sala non trovata per il cinema selezionato" });
            }

            if (dto.PrezzoBase <= 0)
            {
                return Results.BadRequest(new { error = "Prezzo base non valido" });
            }

            var hasOverlap = await HasSalaOverlap(db, dto, salaId.Value, null);
            if (hasOverlap)
            {
                return Results.Conflict(new { error = "Conflitto orario sala: show sovrapposto" });
            }

            var p = new Proiezione
            {
                FilmId = dto.FilmId,
                CinemaId = dto.CinemaId,
                SalaId = salaId.Value,
                Data = dto.Data,
                Ora = dto.Ora,
                PrezzoBase = dto.PrezzoBase
            };
            db.Proiezioni.Add(p);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Proiezione duplicata o vincolo violato", details = ex.Message });
            }

            return Results.Created($"/proiezioni/{p.Id}", p);
        }).RequireAuthorization("AdminOrPowerUser");

        group.MapPut("/{id:int}", async (int id, ProiezioneCreateDTO dto, FilmDbContext db) =>
        {
            var p = await db.Proiezioni.FindAsync(id);
            if (p is null)
            {
                return Results.NotFound();
            }

            var film = await db.Films.FindAsync(dto.FilmId);
            if (film is null)
            {
                return Results.BadRequest(new { error = "Film non trovato" });
            }

            var cinema = await db.Cinemas.FindAsync(dto.CinemaId);
            if (cinema is null)
            {
                return Results.BadRequest(new { error = "Cinema non trovato" });
            }

            var salaId = await ResolveOrCreateSalaIdAsync(db, dto.CinemaId, dto.SalaId, p.SalaId);
            if (!salaId.HasValue)
            {
                return Results.BadRequest(new { error = "Sala non trovata per il cinema selezionato" });
            }

            if (dto.PrezzoBase <= 0)
            {
                return Results.BadRequest(new { error = "Prezzo base non valido" });
            }

            var hasOverlap = await HasSalaOverlap(db, dto, salaId.Value, id);
            if (hasOverlap)
            {
                return Results.Conflict(new { error = "Conflitto orario sala: show sovrapposto" });
            }

            p.FilmId = dto.FilmId;
            p.CinemaId = dto.CinemaId;
            p.SalaId = salaId.Value;
            p.Data = dto.Data;
            p.Ora = dto.Ora;
            p.PrezzoBase = dto.PrezzoBase;

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Proiezione duplicata o vincolo violato", details = ex.Message });
            }

            return Results.NoContent();
        }).RequireAuthorization("AdminOrPowerUser");

        group.MapDelete("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var p = await db.Proiezioni.FindAsync(id);
            if (p is null)
            {
                return Results.NotFound();
            }

            db.Proiezioni.Remove(p);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOrPowerUser");

        group.MapPost("/{id:int}/cancel", async (int id, FilmDbContext db, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("ProiezioniEndpoints");
            var show = await db.Proiezioni
                .Include(p => p.Film)
                .Include(p => p.Cinema)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (show == null) return Results.NotFound();

            var bookings = await db.Prenotazioni
                .Include(b => b.Utente)
                .Where(b => b.ProiezioneId == id && b.Stato == "Confermata")
                .ToListAsync();

            var now = DateTime.UtcNow;
            foreach (var booking in bookings)
            {
                // Calculate total refund (what they actually paid, excluding gift card portion)
                var refundAmount = booking.TotalePrezzo;

                // Generate a gift card for the refund
                var code = "NFH-RF-" + Guid.NewGuid().ToString("N")[..6].ToUpper();
                db.GiftCards.Add(new GiftCard
                {
                    Codice = code, ImportoIniziale = refundAmount, SaldoResiduo = refundAmount,
                    UtenteAcquirenteId = booking.UtenteId, Messaggio = $"Rimborso per proiezione annullata: {show.Film.Titolo} del {show.Data:yyyy-MM-dd} presso {show.Cinema.Nome}",
                    Stato = "Active", CreatoIl = now
                });

                booking.Stato = "Annullata";

                // Release seats
                await db.SeatLocks
                    .Where(l => l.ProiezioneId == id && l.PostoCodice != null && booking.PostiSelezionati.Contains(l.PostoCodice))
                    .ExecuteDeleteAsync();
            }

            await db.SaveChangesAsync();
            logger.LogInformation("Proiezione {ShowId} cancellata. {Count} prenotazioni rimborsate con gift card.", id, bookings.Count);

            return Results.Ok(new { cancelled = true, bookingsRefunded = bookings.Count });
        }).RequireAuthorization("AdminOrPowerUser");

        return group;
    }

    private static ProiezioneDTO ToDto(Proiezione p)
    {
        return new ProiezioneDTO
        {
            Id = p.Id,
            Data = p.Data,
            Ora = p.Ora,
            FilmId = p.FilmId,
            CinemaId = p.CinemaId,
            SalaId = p.SalaId ?? 0,
            TipologiaSala = p.Sala?.Tipologia ?? "2D",
            PrezzoBase = p.PrezzoBase
        };
    }

    private static async Task<bool> HasSalaOverlap(FilmDbContext db, ProiezioneCreateDTO dto, int salaId, int? currentId)
    {
        var film = await db.Films.AsNoTracking().FirstOrDefaultAsync(f => f.Id == dto.FilmId);
        if (film is null)
        {
            return false;
        }

        var start = BuildShowStart(dto.Data, dto.Ora);
        var end = start.AddMinutes(film.Durata);

        var sameDay = dto.Data.Date;
        var query = db.Proiezioni
            .AsNoTracking()
            .Include(p => p.Film)
            .Where(p => p.SalaId == salaId && p.Data.Date == sameDay);

        if (currentId.HasValue)
        {
            query = query.Where(p => p.Id != currentId.Value);
        }

        var sameSalaShows = await query.ToListAsync();
        foreach (var existing in sameSalaShows)
        {
            var existingStart = BuildShowStart(existing.Data, existing.Ora);
            var existingEnd = existingStart.AddMinutes(existing.Film.Durata);
            if (start < existingEnd && end > existingStart)
            {
                return true;
            }
        }

        return false;
    }

    private static DateTime BuildShowStart(DateTime data, DateTime ora)
    {
        return new DateTime(data.Year, data.Month, data.Day, ora.Hour, ora.Minute, 0, DateTimeKind.Local);
    }

    private static async Task<int?> ResolveOrCreateSalaIdAsync(FilmDbContext db, int cinemaId, int requestedSalaId, int? fallbackSalaId = null)
    {
        if (requestedSalaId > 0)
        {
            var requested = await db.Sale.AsNoTracking().FirstOrDefaultAsync(s => s.Id == requestedSalaId && s.CinemaId == cinemaId);
            if (requested is not null)
            {
                return requested.Id;
            }
            return null;
        }

        if (fallbackSalaId.HasValue && fallbackSalaId.Value > 0)
        {
            var fallback = await db.Sale.AsNoTracking().FirstOrDefaultAsync(s => s.Id == fallbackSalaId.Value && s.CinemaId == cinemaId);
            if (fallback is not null)
            {
                return fallback.Id;
            }
        }

        var existing = await db.Sale
            .AsNoTracking()
            .Where(s => s.CinemaId == cinemaId)
            .OrderBy(s => s.NumeroProgressivo)
            .FirstOrDefaultAsync();
        if (existing is not null)
        {
            return existing.Id;
        }

        var sala = new Sala
        {
            CinemaId = cinemaId,
            NumeroProgressivo = 1,
            Tipologia = "2D",
            Nome = "SALA 1",
            NumeroFile = 10,
            PostiPerFila = 12,
            MappaPostiJson = string.Empty,
            Attiva = true
        };

        db.Sale.Add(sala);
        await db.SaveChangesAsync();
        return sala.Id;
    }
}
