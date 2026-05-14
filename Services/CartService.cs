using FilmAPI.Data;
using FilmAPI.Model;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Services;

public class CartService
{
    private readonly FilmDbContext _db;

    public CartService(FilmDbContext db)
    {
        _db = db;
    }

    public async Task<Cart> GetOrCreateCartAsync(int? userId, string? guestToken)
    {
        Cart? cart = null;

        if (userId > 0)
        {
            cart = await _db.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.UtenteId == userId && (c.Stato == "Active" || c.Stato == "Checkout"));
        }
        else if (!string.IsNullOrWhiteSpace(guestToken))
        {
            cart = await _db.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.GuestToken == guestToken && (c.Stato == "Active" || c.Stato == "Checkout") && c.UtenteId == null);
        }

        if (cart != null)
        {
            var now = DateTime.UtcNow;
            // If the cart was in checkout state (user returned from Stripe without paying), revert to active.
            // BUT: if checkout is recent (<30s) and has a StripeSessionId, keep it — payment is still in progress.
            if (cart.Stato == "Checkout")
            {
                if (cart.StripeSessionId != null && cart.UpdatedAtUtc > now.AddSeconds(-30))
                {
                    // Payment in progress, leave unchanged
                }
                else
                {
                    cart.Stato = "Active";
                    cart.StripeSessionId = null;
                }
            }
            // Clean up expired ticket items (seat locks released)
            await RemoveExpiredTicketItemsAsync(cart);
            // Extend active locks by 5 minutes to prevent expiry while user is on cart page
            var extendUntil = now.AddMinutes(5);
            await _db.SeatLocks
                .Where(l => l.CartId == cart.Id && l.ExpiresAtUtc > now && l.ExpiresAtUtc < extendUntil)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.ExpiresAtUtc, extendUntil));
        }

        if (cart == null)
        {
            cart = new Cart
            {
                UtenteId = userId > 0 ? userId : null,
                GuestToken = userId == null ? (guestToken ?? Guid.NewGuid().ToString("N")) : null,
                CartType = "Mixed",
                Stato = "Active",
                ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
            };
            _db.Carts.Add(cart);
            await _db.SaveChangesAsync();
            await _db.Entry(cart).Collection(c => c.CartItems).LoadAsync();
        }
        else
        {
            await RecalculateAsync(cart);
        }

        return cart;
    }

    public async Task RecalculateAsync(Cart cart)
    {
        var items = await _db.CartItems
            .Where(ci => ci.CartId == cart.Id)
            .ToListAsync();

        cart.Subtotale = items.Sum(i => i.PrezzoUnitario * i.Quantita);
        cart.ScontoCoupon = 0;

        if (cart.CouponId.HasValue)
        {
            var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Id == cart.CouponId.Value && c.Attivo);
            var valid = coupon != null;

            if (valid)
            {
                var today = DateTime.Today;
                if (today < coupon!.ValidoDal.Date || today > coupon.ValidoAl.Date)
                    valid = false;

                if (valid && coupon.MaxUtilizzi > 0 && coupon.UtilizziAttuali >= coupon.MaxUtilizzi)
                    valid = false;

                if (valid && coupon.TipoTarget != "Carrello" && coupon.TargetId.HasValue)
                {
                    bool targetMatch = coupon.TipoTarget switch
                    {
                        "Film" => items.Any(ci => ci.ItemType == "Ticket" &&
                            _db.Proiezioni.Any(p => p.Id == ci.ItemId && p.FilmId == coupon.TargetId.Value)),
                        "Cinema" => items.Any(ci => ci.ItemType == "Ticket" &&
                            _db.Proiezioni.Any(p => p.Id == ci.ItemId && p.CinemaId == coupon.TargetId.Value)),
                        _ => false
                    };
                    if (!targetMatch) valid = false;
                }

                if (valid && coupon.MinImportoCarrello.HasValue && cart.Subtotale < coupon.MinImportoCarrello.Value)
                    valid = false;
            }

            if (valid)
            {
                cart.ScontoCoupon = CalculateDiscount(coupon!, cart.Subtotale);
            }
            else
            {
                cart.CouponId = null;
            }
        }

        var dopoCoupon = Math.Max(0, cart.Subtotale - cart.ScontoCoupon);
        if (!string.IsNullOrWhiteSpace(cart.GiftCardCode) && cart.ImportoGiftCard > dopoCoupon)
        {
            cart.ImportoGiftCard = dopoCoupon;
        }
        cart.Totale = Math.Max(0, dopoCoupon - cart.ImportoGiftCard);

        cart.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task RemoveExpiredTicketItemsAsync(Cart cart)
    {
        var now = DateTime.UtcNow;

        var expiredLockSeats = await _db.SeatLocks
            .Where(l => l.CartId == cart.Id && l.ExpiresAtUtc < now)
            .Select(l => l.PostoCodice)
            .ToListAsync();

        if (expiredLockSeats.Count > 0)
        {
            // Remove expired seat locks
            await _db.SeatLocks
                .Where(l => l.CartId == cart.Id && l.ExpiresAtUtc < now)
                .ExecuteDeleteAsync();
        }

        // Refresh ticket items: remove if no active locks, trim if some expired
        var ticketItems = await _db.CartItems
            .Where(ci => ci.CartId == cart.Id && ci.ItemType == "Ticket")
            .ToListAsync();

        var modified = false;
        foreach (var item in ticketItems)
        {
            var activeLockSeats = await _db.SeatLocks
                .Where(l => l.CartId == cart.Id && l.ProiezioneId == item.ItemId)
                .Select(l => l.PostoCodice)
                .ToListAsync();

            if (activeLockSeats.Count == 0)
            {
                _db.CartItems.Remove(item);
                modified = true;
            }
            else if (expiredLockSeats.Count > 0 && !string.IsNullOrWhiteSpace(item.DettaglioJson))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(item.DettaglioJson);
                    if (doc.RootElement.TryGetProperty("posti", out var postiEl) && postiEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var remaining = postiEl.EnumerateArray()
                            .Select(e => e.GetString())
                            .Where(s => s != null && activeLockSeats.Contains(s))
                            .ToList();
                        if (remaining.Count < postiEl.GetArrayLength())
                        {
                            item.DettaglioJson = System.Text.Json.JsonSerializer.Serialize(new { posti = remaining, tipo = doc.RootElement.TryGetProperty("tipo", out var t) ? t.GetString() : null });
                            item.Quantita = remaining.Count;
                            modified = true;
                        }
                    }
                }
                catch { }
            }
        }

        if (modified || expiredLockSeats.Count > 0)
        {
            await _db.SaveChangesAsync();
            await RecalculateAsync(cart);
        }
    }

    public async Task MergeGuestCartAsync(int userId, string guestToken)
    {
        var guestCart = await _db.Carts
            .Include(c => c.CartItems)
            .FirstOrDefaultAsync(c => c.GuestToken == guestToken && c.Stato == "Active" && c.UtenteId == null);

        if (guestCart == null) return;

        var userCart = await _db.Carts
            .Include(c => c.CartItems)
            .FirstOrDefaultAsync(c => c.UtenteId == userId && c.Stato == "Active");

        if (userCart != null)
        {
            foreach (var guestItem in guestCart.CartItems)
            {
                var existing = userCart.CartItems.FirstOrDefault(ci =>
                    ci.ItemType == guestItem.ItemType &&
                    ci.ItemId == guestItem.ItemId &&
                    ci.VariantId == guestItem.VariantId);

                if (existing != null)
                {
                    existing.Quantita += guestItem.Quantita;
                }
                else
                {
                    guestItem.CartId = userCart.Id;
                    userCart.CartItems.Add(guestItem);
                }
            }

            await _db.SeatLocks
                .Where(l => l.CartId == guestCart.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.CartId, userCart.Id));

            _db.Carts.Remove(guestCart);
        }
        else
        {
            guestCart.UtenteId = userId;
            guestCart.GuestToken = null;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<bool> ApplyCouponAsync(Cart cart, string codice, int userId)
    {
        cart.Subtotale = await _db.CartItems
            .Where(ci => ci.CartId == cart.Id)
            .SumAsync(ci => ci.PrezzoUnitario * ci.Quantita);

        var coupon = await _db.Coupons
            .FirstOrDefaultAsync(c => c.Codice == codice.ToUpperInvariant() && c.Attivo);

        if (coupon == null) return false;

        var today = DateTime.Today;
        if (today < coupon.ValidoDal.Date || today > coupon.ValidoAl.Date) return false;
        if (coupon.MaxUtilizzi > 0)
        {
            var usedCount = coupon.UtilizziAttuali;
            var pendingCount = await _db.Carts.CountAsync(c => c.CouponId == coupon.Id && c.Stato == "Active" && c.Id != cart.Id);
            if (usedCount + pendingCount >= coupon.MaxUtilizzi) return false;
        }

        var userUsageCount = await _db.CouponUsages
            .CountAsync(cu => cu.CouponId == coupon.Id && cu.UtenteId == userId);
        if (userUsageCount >= coupon.MaxPerUtente) return false;

        if (cart.CouponId != null && !coupon.Stackable) return false;

        if (coupon.MinImportoCarrello.HasValue && cart.Subtotale < coupon.MinImportoCarrello.Value) return false;

        if (coupon.QuantitaMinima > 1)
        {
            var hasMinQty = await _db.CartItems
                .AnyAsync(ci => ci.CartId == cart.Id && ci.Quantita >= coupon.QuantitaMinima);
            if (!hasMinQty) return false;
        }

        if (coupon.TipoTarget != "Carrello" && coupon.TargetId.HasValue)
        {
            bool targetMatch = coupon.TipoTarget switch
            {
                "Film" => await _db.CartItems.AnyAsync(ci =>
                    ci.CartId == cart.Id && ci.ItemType == "Ticket" &&
                    _db.Proiezioni.Any(p => p.Id == ci.ItemId && p.FilmId == coupon.TargetId.Value)),
                "Cinema" => await _db.CartItems.AnyAsync(ci =>
                    ci.CartId == cart.Id && ci.ItemType == "Ticket" &&
                    _db.Proiezioni.Any(p => p.Id == ci.ItemId && p.CinemaId == coupon.TargetId.Value)),
                _ => false
            };
            if (!targetMatch) return false;
        }

        var sconto = CalculateDiscount(coupon, cart.Subtotale);

        cart.CouponId = coupon.Id;
        await RecalculateAsync(cart);
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task RemoveCouponAsync(Cart cart)
    {
        cart.CouponId = null;
        cart.ScontoCoupon = 0;
        await RecalculateAsync(cart);
        await _db.SaveChangesAsync();
    }

    private static decimal CalculateDiscount(Coupon coupon, decimal subtotale)
    {
        if (coupon.TipoSconto == "Percentuale")
        {
            var sconto = subtotale * (coupon.ValoreSconto / 100m);
            if (coupon.ScontoMassimo.HasValue && sconto > coupon.ScontoMassimo.Value)
                sconto = coupon.ScontoMassimo.Value;
            return Math.Round(sconto, 2);
        }

        if (coupon.TipoSconto == "Fisso")
        {
            return Math.Min(coupon.ValoreSconto, subtotale);
        }

        return 0;
    }
}
