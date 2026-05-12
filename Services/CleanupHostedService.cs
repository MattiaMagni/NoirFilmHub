using FilmAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Services;

public class CleanupHostedService : IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private Timer? _timer;

    public CleanupHostedService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(async _ => await DoCleanup(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60));
        return Task.CompletedTask;
    }

    private async Task DoCleanup()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FilmDbContext>();
            var now = DateTime.UtcNow;

            // Remove ticket items whose seat locks have expired
            var activeCartsWithExpiredLocks = await db.SeatLocks
                .Where(l => l.ExpiresAtUtc < now && l.CartId != null)
                .Select(l => l.CartId!.Value)
                .Distinct()
                .ToListAsync();

            foreach (var cartId in activeCartsWithExpiredLocks)
            {
                // Delete expired locks
                await db.SeatLocks
                    .Where(l => l.CartId == cartId && l.ExpiresAtUtc < now)
                    .ExecuteDeleteAsync();

                // Remove ticket items that have no remaining active locks
                var ticketItems = await db.CartItems
                    .Where(ci => ci.CartId == cartId && ci.ItemType == "Ticket")
                    .ToListAsync();

                foreach (var item in ticketItems)
                {
                    var hasActiveLocks = await db.SeatLocks
                        .AnyAsync(l => l.CartId == cartId && l.ProiezioneId == item.ItemId);
                    if (!hasActiveLocks)
                    {
                        db.CartItems.Remove(item);
                    }
                }

                // If cart is now empty of items, mark as expired
                var hasItems = await db.CartItems.AnyAsync(ci => ci.CartId == cartId);
                if (!hasItems)
                {
                    var cart = await db.Carts.FindAsync(cartId);
                    if (cart != null && cart.Stato == "Active")
                        cart.Stato = "Expired";
                }
            }

            // Expire fully expired carts (past their cart-level TTL of 7 days)
            var expiredCarts = await db.Carts
                .Where(c => c.Stato == "Active" && c.ExpiresAtUtc < now)
                .ToListAsync();

            foreach (var cart in expiredCarts)
            {
                cart.Stato = "Expired";
                await db.SeatLocks
                    .Where(l => l.CartId == cart.Id)
                    .ExecuteDeleteAsync();
                await db.InventoryReservations
                    .Where(r => r.CartId == cart.Id)
                    .ExecuteDeleteAsync();
            }

            // Orphan seat locks (expired, no cart)
            await db.SeatLocks
                .Where(l => l.ExpiresAtUtc < now.AddMinutes(-5) && l.CartId == null)
                .ExecuteDeleteAsync();

            // Expired inventory reservations
            await db.InventoryReservations
                .Where(r => r.ExpiresAtUtc < now)
                .ExecuteDeleteAsync();

            await db.ExternalAuthStates
                .Where(s => s.ExpiresAtUtc < now)
                .ExecuteDeleteAsync();

            await db.AccountActionTokens
                .Where(t => t.ExpiresAtUtc < now.AddHours(-1) && t.ConsumedAtUtc != null)
                .ExecuteDeleteAsync();

            await db.AccountActionTokens
                .Where(t => t.ExpiresAtUtc < now.AddDays(-1))
                .ExecuteDeleteAsync();

            await db.SaveChangesAsync();
        }
        catch
        {
            // best-effort cleanup
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
