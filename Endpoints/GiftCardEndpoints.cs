using System.Security.Claims;
using FilmAPI.Data;
using FilmAPI.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Endpoints;

public static class GiftCardEndpoints
{
    public static RouteGroupBuilder MapGiftCards(this RouteGroupBuilder group)
    {
        group.MapGet("/mine", async (ClaimsPrincipal user, FilmDbContext db) =>
        {
            if (!TryGetUserId(user, out var userId))
                return Results.Unauthorized();

            var cards = await db.GiftCards
                .Where(gc => gc.UtenteAcquirenteId == userId)
                .OrderByDescending(gc => gc.CreatoIl)
                .Select(gc => new
                {
                    gc.Id, gc.Codice, gc.ImportoIniziale, gc.SaldoResiduo,
                    gc.Stato, gc.Scadenza, gc.CreatoIl
                })
                .ToListAsync();

            return Results.Ok(cards);
        }).RequireAuthorization();

        group.MapGet("/{codice}/balance", async (string codice, FilmDbContext db) =>
        {
            var card = await db.GiftCards
                .FirstOrDefaultAsync(gc => gc.Codice == codice.ToUpperInvariant());

            if (card == null)
                return Results.Ok(new { valid = false, saldo = 0m, message = "Gift card non trovata" });

            var now = DateTime.UtcNow;
            if (card.Stato != "Active")
                return Results.Ok(new { valid = false, saldo = 0m, message = "Gift card non attiva" });

            if (card.Scadenza.HasValue && card.Scadenza.Value < now)
                return Results.Ok(new { valid = false, saldo = 0m, message = "Gift card scaduta" });

            return Results.Ok(new { valid = true, saldo = card.SaldoResiduo });
        });

        group.MapPost("/{codice}/redeem", async (string codice, GiftCardRedeemRequest req, ClaimsPrincipal user, FilmDbContext db) =>
        {
            if (!TryGetUserId(user, out var userId))
                return Results.Unauthorized();

            var card = await db.GiftCards
                .FirstOrDefaultAsync(gc => gc.Codice == codice.ToUpperInvariant() && gc.Stato == "Active");

            if (card == null)
                return Results.BadRequest(new { error = "Gift card non valida" });

            var now = DateTime.UtcNow;
            if (card.Scadenza.HasValue && card.Scadenza.Value < now)
                return Results.BadRequest(new { error = "Gift card scaduta" });

            var importo = req.Importo.HasValue && req.Importo.Value > 0
                ? Math.Min(req.Importo.Value, card.SaldoResiduo)
                : card.SaldoResiduo;

            var utente = await db.Utenti.FindAsync(userId);
            if (utente == null) return Results.NotFound();

            card.SaldoResiduo -= importo;
            if (card.SaldoResiduo <= 0)
            {
                card.SaldoResiduo = 0;
                card.Stato = "Consumed";
            }

            utente.CreditoPiattaforma += importo;

            db.GiftCardTransactions.Add(new GiftCardTransaction
            {
                GiftCardId = card.Id,
                Tipo = "Redemption",
                Importo = importo,
                SaldoDopo = card.SaldoResiduo
            });

            await db.SaveChangesAsync();

            return Results.Ok(new { card.SaldoResiduo, importoRiscattato = importo, creditoAttuale = utente.CreditoPiattaforma });
        }).RequireAuthorization();

        group.MapGet("/mine/transactions", async (ClaimsPrincipal user, FilmDbContext db) =>
        {
            if (!TryGetUserId(user, out var userId))
                return Results.Unauthorized();

            var transactions = await db.GiftCardTransactions
                .Where(gt => gt.GiftCard.UtenteAcquirenteId == userId)
                .OrderByDescending(gt => gt.CreatoIl)
                .Select(gt => new { gt.Id, gt.GiftCardId, gt.Tipo, gt.Importo, gt.SaldoDopo, gt.CreatoIl })
                .ToListAsync();

            return Results.Ok(transactions);
        }).RequireAuthorization();

        return group;
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out int userId)
    {
        var val = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(val, out userId);
    }

    public class GiftCardRedeemRequest
    {
        public decimal? Importo { get; set; }
    }
}
