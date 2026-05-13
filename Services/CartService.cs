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
                .FirstOrDefaultAsync(c => c.UtenteId == userId && c.Stato == "Active");
        }
        else if (!string.IsNullOrWhiteSpace(guestToken))
        {
            cart = await _db.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.GuestToken == guestToken && c.Stato == "Active" && c.UtenteId == null);
        }

        if (cart != null)
        {
            // Clean up expired ticket items (seat locks released)
            await RemoveExpiredTicketItemsAsync(cart);
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
            if (coupon != null)
            {
                cart.ScontoCoupon = CalculateDiscount(coupon, cart.Subtotale);
            }
        }

        cart.Totale = cart.Subtotale - cart.ScontoCoupon - cart.ImportoGiftCard;
        if (cart.Totale < 0) cart.Totale = 0;

        cart.UpdatedAtUtc = DateTime.UtcNow;
    }

    public async Task RemoveExpiredTicketItemsAsync(Cart cart)
    {
        var now = DateTime.UtcNow;

        var expiredLockSeats = await _db.SeatLocks
            .Where(l => l.CartId == cart.Id && l.ExpiresAtUtc < now)
            .Select(l => l.PostoCodice)
            .ToListAsync();

        if (expiredLockSeats.Count == 0) return;

        // Remove expired seat locks
        await _db.SeatLocks
            .Where(l => l.CartId == cart.Id && l.ExpiresAtUtc < now)
            .ExecuteDeleteAsync();

        // Remove ticket items whose locks have all expired
        var ticketItems = await _db.CartItems
            .Where(ci => ci.CartId == cart.Id && ci.ItemType == "Ticket")
            .ToListAsync();

        foreach (var item in ticketItems)
        {
            var hasActiveLocks = await _db.SeatLocks
                .AnyAsync(l => l.CartId == cart.Id && l.ProiezioneId == item.ItemId);
            if (!hasActiveLocks)
            {
                _db.CartItems.Remove(item);
            }
        }

        await _db.SaveChangesAsync();
        await RecalculateAsync(cart);
        await _db.SaveChangesAsync();
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
        var coupon = await _db.Coupons
            .FirstOrDefaultAsync(c => c.Codice == codice.ToUpperInvariant() && c.Attivo);

        if (coupon == null) return false;

        var now = DateTime.UtcNow;
        if (now < coupon.ValidoDal || now > coupon.ValidoAl) return false;
        if (coupon.MaxUtilizzi > 0 && coupon.UtilizziAttuali >= coupon.MaxUtilizzi) return false;

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

        coupon.UtilizziAttuali++;
        _db.CouponUsages.Add(new CouponUsage
        {
            CouponId = coupon.Id,
            UtenteId = userId,
            CartId = cart.Id,
            ScontoApplicato = sconto
        });

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
