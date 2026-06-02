using System.Security.Claims;
using FilmAPI.Data;
using FilmAPI.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Endpoints;

public static class RitiriEndpoints
{
    public static RouteGroupBuilder MapRitiri(this RouteGroupBuilder group)
    {
        group.MapGet("/validate/{codiceRitiro}", async (string codiceRitiro, FilmDbContext db) =>
        {
            var ritiro = await db.RitiriOrdine
                .AsNoTracking()
                .Include(r => r.Cart)
                .ThenInclude(c => c.CartItems)
                .FirstOrDefaultAsync(r => r.CodiceRitiro == codiceRitiro);

            if (ritiro is null)
                return Results.NotFound(new { error = "Codice ritiro non trovato" });

            if (ritiro.Stato == "Ritirato")
                return Results.Conflict(new { error = "Ordine gia ritirato", ritiratoIl = ritiro.RitiratoIl });

            var articoli = ritiro.Cart.CartItems
                .Where(ci => ci.ItemType == "Merchandise")
                .Select(ci =>
                {
                    var nome = "Prodotto";
                    if (!string.IsNullOrWhiteSpace(ci.DettaglioJson))
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(ci.DettaglioJson);
                            if (doc.RootElement.TryGetProperty("nome", out var n) && n.ValueKind == System.Text.Json.JsonValueKind.String)
                                nome = n.GetString() ?? nome;
                            if (doc.RootElement.TryGetProperty("taglia", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String)
                                nome += $" ({t.GetString()})";
                        }
                        catch { }
                    }
                    return new { nome, ci.Quantita, ci.PrezzoUnitario };
                })
                .ToList();

            return Results.Ok(new
            {
                ritiro.CodiceRitiro,
                ritiro.Stato,
                cartId = ritiro.CartId,
                creatoIl = ritiro.CreatoIl,
                articoli
            });
        }).AllowAnonymous();

        group.MapPost("/{codiceRitiro}/ritira", async (string codiceRitiro, ClaimsPrincipal user, FilmDbContext db) =>
        {
            if (!TryGetUserId(user, out var userId))
                return Results.Unauthorized();

            var operatore = await db.Utenti.FirstOrDefaultAsync(u => u.Id == userId);
            if (operatore is null)
                return Results.Unauthorized();

            var ritiro = await db.RitiriOrdine
                .Include(r => r.Cart)
                .FirstOrDefaultAsync(r => r.CodiceRitiro == codiceRitiro);

            if (ritiro is null)
                return Results.NotFound(new { error = "Codice ritiro non trovato" });

            if (ritiro.Stato == "Ritirato")
                return Results.Conflict(new { error = "Ordine gia ritirato" });

            if (!operatore.CinemaPreferitoId.HasValue)
                return Results.BadRequest(new { error = "Operatore senza cinema associato" });

            ritiro.Stato = "Ritirato";
            ritiro.RitiratoIl = DateTime.UtcNow;
            ritiro.RitiratoDaUtenteId = operatore.Id;

            await db.SaveChangesAsync();

            return Results.Ok(new { ritiro.CodiceRitiro, ritiro.Stato, ritiro.RitiratoIl });
        }).RequireAuthorization("AdminOrPowerUser");

        return group;
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out int userId)
    {
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out userId);
    }
}
