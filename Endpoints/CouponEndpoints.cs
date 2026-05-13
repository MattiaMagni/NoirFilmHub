using System.Security.Claims;
using FilmAPI.Data;
using FilmAPI.Model;
using FilmAPI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Endpoints;

public static class CouponEndpoints
{
    public static RouteGroupBuilder MapCoupons(this RouteGroupBuilder group)
    {
        group.MapPost("/validate", async (ValidateCouponRequest req, ClaimsPrincipal user, FilmDbContext db) =>
        {
            var userIdVal = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdVal, out var userId))
                return Results.Unauthorized();

            var coupon = await db.Coupons
                .FirstOrDefaultAsync(c => c.Codice == req.Codice.ToUpperInvariant() && c.Attivo);

            if (coupon == null)
                return Results.Ok(new { valid = false, message = "Coupon non valido o non applicabile" });

            var now = DateTime.UtcNow;
            if (now < coupon.ValidoDal || now > coupon.ValidoAl)
                return Results.Ok(new { valid = false, message = "Coupon non valido o non applicabile" });

            if (coupon.MaxUtilizzi > 0 && coupon.UtilizziAttuali >= coupon.MaxUtilizzi)
                return Results.Ok(new { valid = false, message = "Coupon non valido o non applicabile" });

            var userCount = await db.CouponUsages
                .CountAsync(cu => cu.CouponId == coupon.Id && cu.UtenteId == userId);
            if (userCount >= coupon.MaxPerUtente)
                return Results.Ok(new { valid = false, message = "Coupon non valido o non applicabile" });

            return Results.Ok(new
            {
                valid = true,
                coupon = new
                {
                    coupon.Id,
                    coupon.Codice,
                    coupon.TipoSconto,
                    coupon.ValoreSconto,
                    coupon.ScontoMassimo,
                    coupon.TipoTarget,
                    coupon.MinImportoCarrello
                }
            });
        }).RequireAuthorization();

        group.MapGet("/", async (FilmDbContext db) =>
        {
            var coupons = await db.Coupons
                .OrderByDescending(c => c.CreatoIl)
                .ToListAsync();

            var cinemaIds = coupons
                .Where(c => c.TipoTarget == "Cinema" && c.TargetId.HasValue)
                .Select(c => c.TargetId!.Value)
                .Distinct()
                .ToList();

            var cinemas = await db.Cinemas
                .Where(c => cinemaIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => $"{c.Nome} - {c.Citta}");

            return Results.Ok(coupons.Select(c => new
            {
                c.Id,
                c.Codice,
                c.TipoSconto,
                c.ValoreSconto,
                c.TipoTarget,
                c.TargetId,
                c.ValidoDal,
                c.ValidoAl,
                c.Attivo,
                c.MinImportoCarrello,
                CinemaNome = c.TipoTarget == "Cinema" && c.TargetId.HasValue && cinemas.ContainsKey(c.TargetId.Value)
                    ? cinemas[c.TargetId.Value]
                    : null
            }));
        }).AllowAnonymous();

        group.MapPost("/{id:int}/redeem", async (int id, ClaimsPrincipal user, FilmDbContext db, EmailService emailService) =>
        {
            var userIdVal = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdVal, out var userId))
                return Results.Unauthorized();

            var coupon = await db.Coupons.FindAsync(id);
            if (coupon == null || !coupon.Attivo)
                return Results.BadRequest(new { error = "Offerta non trovata" });

            var now = DateTime.UtcNow;
            if (now < coupon.ValidoDal || now > coupon.ValidoAl)
                return Results.BadRequest(new { error = "Offerta scaduta o non ancora valida" });

            if (coupon.MaxUtilizzi > 0 && coupon.UtilizziAttuali >= coupon.MaxUtilizzi)
                return Results.BadRequest(new { error = "Offerta esaurita" });

            var userCount = await db.CouponUsages
                .CountAsync(cu => cu.CouponId == coupon.Id && cu.UtenteId == userId);
            if (userCount >= coupon.MaxPerUtente)
                return Results.BadRequest(new { error = "Hai gia riscattato questa offerta" });

            var utente = await db.Utenti.FindAsync(userId);
            if (utente == null) return Results.Unauthorized();

            string? cinemaNome = null;
            if (coupon.TipoTarget == "Cinema" && coupon.TargetId.HasValue)
            {
                var cinema = await db.Cinemas.FindAsync(coupon.TargetId.Value);
                cinemaNome = cinema != null ? $"{cinema.Nome} - {cinema.Citta}" : null;
            }

            await emailService.SendCouponRedeemEmail(
                utente.Email, utente.Nome, coupon.Codice,
                coupon.TipoSconto, coupon.ValoreSconto, cinemaNome, coupon.ValidoAl);

            return Results.Ok(new
            {
                codice = coupon.Codice,
                tipoSconto = coupon.TipoSconto,
                valoreSconto = coupon.ValoreSconto,
                cinemaNome,
                scadenza = coupon.ValidoAl,
                messaggio = $"Codice {coupon.Codice} riscattato! Usalo entro il {coupon.ValidoAl:dd/MM/yyyy}."
            });
        }).RequireAuthorization();

        group.MapPost("/", async (Coupon coupon, FilmDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(coupon.Codice))
                return Results.BadRequest(new { error = "Codice obbligatorio" });

            coupon.Codice = coupon.Codice.ToUpperInvariant();
            db.Coupons.Add(coupon);
            await db.SaveChangesAsync();
            return Results.Created($"/coupons/{coupon.Id}", coupon);
        }).RequireAuthorization("AdminOnly");

        group.MapPut("/{id:int}", async (int id, Coupon updated, FilmDbContext db) =>
        {
            var coupon = await db.Coupons.FindAsync(id);
            if (coupon == null) return Results.NotFound();

            coupon.TipoSconto = updated.TipoSconto;
            coupon.ValoreSconto = updated.ValoreSconto;
            coupon.ScontoMassimo = updated.ScontoMassimo;
            coupon.TipoTarget = updated.TipoTarget;
            coupon.TargetId = updated.TargetId;
            coupon.QuantitaMinima = updated.QuantitaMinima;
            coupon.ValidoDal = updated.ValidoDal;
            coupon.ValidoAl = updated.ValidoAl;
            coupon.MaxUtilizzi = updated.MaxUtilizzi;
            coupon.MaxPerUtente = updated.MaxPerUtente;
            coupon.MinImportoCarrello = updated.MinImportoCarrello;
            coupon.Stackable = updated.Stackable;
            coupon.Attivo = updated.Attivo;

            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        group.MapDelete("/{id:int}", async (int id, FilmDbContext db) =>
        {
            var coupon = await db.Coupons.FindAsync(id);
            if (coupon == null) return Results.NotFound();
            coupon.Attivo = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        group.MapGet("/{id:int}/usage", async (int id, FilmDbContext db) =>
        {
            var usages = await db.CouponUsages
                .Where(cu => cu.CouponId == id)
                .OrderByDescending(cu => cu.CreatoIl)
                .Select(cu => new { cu.Id, cu.UtenteId, cu.CartId, cu.ScontoApplicato, cu.CreatoIl })
                .ToListAsync();

            return Results.Ok(usages);
        }).RequireAuthorization("AdminOnly");

        return group;
    }

    public class ValidateCouponRequest
    {
        public string Codice { get; set; } = string.Empty;
    }
}
