using System.Security.Claims;
using FilmAPI.Data;
using FilmAPI.Model;
using FilmAPI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace FilmAPI.Endpoints;

public static class CartEndpoints
{
    public static RouteGroupBuilder MapCart(this RouteGroupBuilder group)
    {
        group.MapPost("/", async (HttpRequest request, ClaimsPrincipal user, FilmDbContext db, CartService cartService) =>
        {
            TryGetUserId(user, out var userId);
            var guestToken = request.Headers["X-Guest-Token"].FirstOrDefault();
            var cart = await cartService.GetOrCreateCartAsync(userId > 0 ? userId : null, guestToken);
            return Results.Ok(MapCartToDto(cart));
        });

        group.MapGet("/{cartId:int}", async (int cartId, ClaimsPrincipal user, FilmDbContext db) =>
        {
            TryGetUserId(user, out var userId);
            var cart = await db.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.Id == cartId);

            if (cart == null) return Results.NotFound();
            if (cart.UtenteId != null && cart.UtenteId != userId) return Results.Forbid();
            if (cart.Stato == "Expired") return Results.Ok(MapCartToDto(cart));

            return Results.Ok(MapCartToDto(cart));
        });

        group.MapPost("/{cartId:int}/items", async (int cartId, AddCartItemRequest req, ClaimsPrincipal user, FilmDbContext db, CartService cartService) =>
        {
            TryGetUserId(user, out var userId);
            var cart = await db.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.Id == cartId && c.Stato == "Active");

            if (cart == null) return Results.NotFound();
            if (cart.UtenteId != null && cart.UtenteId != userId) return Results.Forbid();

            if (req.ItemType == "Ticket")
            {
                var existingLock = await db.SeatLocks
                    .AnyAsync(l => l.ProiezioneId == req.ItemId && l.UtenteId == userId && l.ExpiresAtUtc > DateTime.UtcNow);
                if (!existingLock)
                    return Results.BadRequest(new { error = "Nessun lock attivo per i posti selezionati. Seleziona i posti prima di aggiungere al carrello." });
            }

            if (req.ItemType == "Merchandise" && req.VariantId.HasValue)
            {
                var variant = await db.ProductVariants.FindAsync(req.VariantId.Value);
                if (variant == null || !variant.Attivo || variant.Stock < req.Quantita)
                    return Results.BadRequest(new { error = "Prodotto non disponibile" });
            }

            var existingItem = cart.CartItems.FirstOrDefault(ci =>
                ci.ItemType == req.ItemType && ci.ItemId == req.ItemId && ci.VariantId == req.VariantId);

            if (existingItem != null)
            {
                existingItem.Quantita += req.Quantita;
                // Merge seat lists for ticket items
                if (req.ItemType == "Ticket" && !string.IsNullOrWhiteSpace(req.DettaglioJson) && !string.IsNullOrWhiteSpace(existingItem.DettaglioJson))
                {
                    try
                    {
                        var existingSeats = System.Text.Json.JsonSerializer.Deserialize<SeatList>(existingItem.DettaglioJson)?.Posti ?? new List<string>();
                        var newSeats = System.Text.Json.JsonSerializer.Deserialize<SeatList>(req.DettaglioJson)?.Posti ?? new List<string>();
                        var merged = existingSeats.Union(newSeats).Distinct().ToList();
                        existingItem.DettaglioJson = System.Text.Json.JsonSerializer.Serialize(new SeatList { Posti = merged, Tipo = existingSeats.Any() ? "standard" : "vip" });
                    }
                    catch { }
                }
            }
            else
            {
                // Validate no duplicate seats across existing ticket items
                if (req.ItemType == "Ticket" && !string.IsNullOrWhiteSpace(req.DettaglioJson))
                {
                    try
                    {
                        var newSeats = System.Text.Json.JsonSerializer.Deserialize<SeatList>(req.DettaglioJson)?.Posti ?? new List<string>();
                        foreach (var ci in cart.CartItems.Where(ci => ci.ItemType == "Ticket"))
                        {
                            if (!string.IsNullOrWhiteSpace(ci.DettaglioJson))
                            {
                                var existingSeats = System.Text.Json.JsonSerializer.Deserialize<SeatList>(ci.DettaglioJson)?.Posti ?? new List<string>();
                                var duplicates = newSeats.Intersect(existingSeats).ToList();
                                if (duplicates.Any())
                                    return Results.Conflict(new { error = $"Posti gia nel carrello: {string.Join(", ", duplicates)}" });
                            }
                        }
                    }
                    catch { }
                }

                cart.CartItems.Add(new CartItem
                {
                    CartId = cart.Id,
                    ItemType = req.ItemType,
                    ItemId = req.ItemId,
                    VariantId = req.VariantId,
                    Quantita = req.Quantita,
                    PrezzoUnitario = req.PrezzoUnitario,
                    DettaglioJson = req.DettaglioJson
                });
            }

            if (req.ItemType == "Ticket")
            {
                await db.SeatLocks
                    .Where(l => l.ProiezioneId == req.ItemId && l.UtenteId == userId && l.CartId == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.CartId, cart.Id));

                var lockExpiry = await db.SeatLocks
                    .Where(l => l.ProiezioneId == req.ItemId && l.UtenteId == userId)
                    .MinAsync(l => (DateTime?)l.ExpiresAtUtc);

                if (lockExpiry.HasValue && lockExpiry.Value < cart.ExpiresAtUtc)
                    cart.ExpiresAtUtc = lockExpiry.Value;
            }

            await cartService.RecalculateAsync(cart);
            await db.SaveChangesAsync();

            return Results.Ok(MapCartToDto(cart));
        }).RequireAuthorization();

        group.MapPut("/{cartId:int}/items/{itemId:int}", async (int cartId, int itemId, UpdateCartItemRequest req, ClaimsPrincipal user, FilmDbContext db, CartService cartService) =>
        {
            TryGetUserId(user, out var userId);
            var cart = await db.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.Id == cartId && c.Stato == "Active");

            if (cart == null) return Results.NotFound();
            if (cart.UtenteId != null && cart.UtenteId != userId) return Results.Forbid();

            var item = cart.CartItems.FirstOrDefault(ci => ci.Id == itemId);
            if (item == null) return Results.NotFound();

            if (req.Quantita <= 0)
            {
                cart.CartItems.Remove(item);
                if (item.ItemType == "Ticket")
                {
                    await db.SeatLocks
                        .Where(l => l.CartId == cart.Id && l.ProiezioneId == item.ItemId)
                        .ExecuteDeleteAsync();
                }
            }
            else
            {
                item.Quantita = req.Quantita;
                if (!string.IsNullOrWhiteSpace(req.DettaglioJson))
                    item.DettaglioJson = req.DettaglioJson;
            }

            await cartService.RecalculateAsync(cart);
            await db.SaveChangesAsync();

            return Results.Ok(MapCartToDto(cart));
        }).RequireAuthorization();

        group.MapDelete("/{cartId:int}/items/{itemId:int}", async (int cartId, int itemId, ClaimsPrincipal user, FilmDbContext db, CartService cartService) =>
        {
            TryGetUserId(user, out var userId);
            var cart = await db.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.Id == cartId && c.Stato == "Active");

            if (cart == null) return Results.NotFound();
            if (cart.UtenteId != null && cart.UtenteId != userId) return Results.Forbid();

            var item = cart.CartItems.FirstOrDefault(ci => ci.Id == itemId);
            if (item == null) return Results.NotFound();

            if (item.ItemType == "Ticket")
            {
                await db.SeatLocks
                    .Where(l => l.CartId == cart.Id && l.ProiezioneId == item.ItemId)
                    .ExecuteDeleteAsync();
            }

            cart.CartItems.Remove(item);
            await cartService.RecalculateAsync(cart);
            await db.SaveChangesAsync();

            return Results.NoContent();
        }).RequireAuthorization();

        group.MapPost("/{cartId:int}/apply-coupon", async (int cartId, ApplyCouponRequest req, ClaimsPrincipal user, FilmDbContext db, CartService cartService) =>
        {
            TryGetUserId(user, out var userId);
            var cart = await db.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.Id == cartId && c.Stato == "Active");

            if (cart == null) return Results.NotFound();
            if (cart.UtenteId != null && cart.UtenteId != userId) return Results.Forbid();

            var success = await cartService.ApplyCouponAsync(cart, req.Codice, userId);
            if (!success)
                return Results.BadRequest(new { error = "Coupon non valido o non applicabile" });

            return Results.Ok(new { sconto = cart.ScontoCoupon, totale = cart.Totale });
        }).RequireAuthorization();

        group.MapDelete("/{cartId:int}/coupon", async (int cartId, ClaimsPrincipal user, FilmDbContext db, CartService cartService) =>
        {
            TryGetUserId(user, out var userId);
            var cart = await db.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.Id == cartId && c.Stato == "Active");

            if (cart == null) return Results.NotFound();
            if (cart.UtenteId != null && cart.UtenteId != userId) return Results.Forbid();

            await cartService.RemoveCouponAsync(cart);
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapPost("/{cartId:int}/apply-giftcard", async (int cartId, ApplyGiftCardRequest req, ClaimsPrincipal user, FilmDbContext db) =>
        {
            TryGetUserId(user, out var userId);
            var cart = await db.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.Id == cartId && c.Stato == "Active");
            if (cart == null) return Results.NotFound();
            if (cart.UtenteId != null && cart.UtenteId != userId) return Results.Forbid();

            var gc = await db.GiftCards
                .FirstOrDefaultAsync(g => g.Codice == req.Codice.ToUpperInvariant() && g.Stato == "Active");
            if (gc == null) return Results.BadRequest(new { error = "Gift card non valida o gia utilizzata" });
            if (gc.Scadenza.HasValue && gc.Scadenza.Value < DateTime.UtcNow)
                return Results.BadRequest(new { error = "Gift card scaduta" });

            // Verify ownership: gift card is tied to the purchasing user or recipient email
            var utente = await db.Utenti.FindAsync(userId);
            if (gc.UtenteAcquirenteId != userId &&
                (string.IsNullOrWhiteSpace(gc.EmailDestinatario) || !string.Equals(gc.EmailDestinatario, utente?.Email, StringComparison.OrdinalIgnoreCase)))
                return Results.BadRequest(new { error = "Gift card non associata al tuo account" });

            cart.Subtotale = await db.CartItems.Where(ci => ci.CartId == cart.Id).SumAsync(ci => ci.PrezzoUnitario * ci.Quantita);
            var dopoCoupon = Math.Max(0, cart.Subtotale - cart.ScontoCoupon);
            var importo = Math.Min(dopoCoupon, gc.SaldoResiduo);

            cart.GiftCardCode = gc.Codice;
            cart.ImportoGiftCard = importo;
            cart.Totale = Math.Max(0, dopoCoupon - importo);
            cart.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new { importo, saldoResiduo = gc.SaldoResiduo, nuovoTotale = cart.Totale });
        }).RequireAuthorization();

        group.MapPost("/merge", async (MergeCartRequest req, ClaimsPrincipal user, FilmDbContext db, CartService cartService) =>
        {
            if (!TryGetUserId(user, out var userId))
                return Results.Unauthorized();

            await cartService.MergeGuestCartAsync(userId, req.GuestToken);
            var cart = await db.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.UtenteId == userId && c.Stato == "Active");

            return Results.Ok(cart != null ? MapCartToDto(cart) : null);
        }).RequireAuthorization();

        return group;
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out int userId)
    {
        var val = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(val, out userId);
    }

    private static object MapCartToDto(Cart cart)
    {
        return new
        {
            cart.Id,
            cart.UtenteId,
            cart.GuestToken,
            cart.CartType,
            cart.Stato,
            cart.Subtotale,
            cart.ScontoCoupon,
            cart.Totale,
            cart.CouponId,
            cart.ImportoGiftCard,
            cart.GiftCardCode,
            cart.ExpiresAtUtc,
            cart.CreatedAtUtc,
            Items = cart.CartItems.Select(ci => new
            {
                ci.Id,
                ci.ItemType,
                ci.ItemId,
                ci.VariantId,
                ci.Quantita,
                ci.PrezzoUnitario,
                ci.DettaglioJson
            })
        };
    }

    public class AddCartItemRequest
    {
        public string ItemType { get; set; } = string.Empty;
        public int ItemId { get; set; }
        public int? VariantId { get; set; }
        public int Quantita { get; set; } = 1;
        public decimal PrezzoUnitario { get; set; }
        public string? DettaglioJson { get; set; }
    }

    public class UpdateCartItemRequest
    {
        public int Quantita { get; set; }
        public string? DettaglioJson { get; set; }
    }

    public class ApplyCouponRequest
    {
        public string Codice { get; set; } = string.Empty;
    }

    public class ApplyGiftCardRequest
    {
        public string Codice { get; set; } = string.Empty;
    }

    public class MergeCartRequest
    {
        public string GuestToken { get; set; } = string.Empty;
    }

    public class SeatList
    {
        [JsonPropertyName("posti")]
        public List<string> Posti { get; set; } = new();
        [JsonPropertyName("tipo")]
        public string? Tipo { get; set; }
    }
}
