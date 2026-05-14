using System.Security.Claims;
using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;
using FilmAPI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace FilmAPI.Endpoints;

public static class PagamentiEndpoints
{
public static RouteGroupBuilder MapPagamenti(this RouteGroupBuilder group)
{
var stripeSecret = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? string.Empty;
if (!string.IsNullOrWhiteSpace(stripeSecret))
{
StripeConfiguration.ApiKey = stripeSecret;
}

group.MapPost("/checkout-session", async (StripeCheckoutSessionCreateDTO dto, ClaimsPrincipal user, FilmDbContext db) =>
{
if (!TryGetUserId(user, out var userId))
{
return Results.Unauthorized();
}

if (string.IsNullOrWhiteSpace(stripeSecret))
{
return Results.BadRequest(new { error = "Stripe non configurato: manca STRIPE_SECRET_KEY" });
}

var validation = await ValidatePurchaseAsync(db, userId, dto.ProiezioneId, dto.PostiSelezionati, requireLocks: true, excludeBookingId: null);
if (!validation.Success)
{
return validation.ErrorResult!;
}

var show = validation.Show!;
var requestedSeats = validation.RequestedSeats!;
var now = DateTime.UtcNow;
var vipSeats = FilmAPI.Services.SeatPricingUtils.GetVipSeats(show.Sala?.NumeroFile ?? 10, show.Sala?.PostiPerFila ?? 12, show.Sala?.MappaPostiJson);
var total = FilmAPI.Services.SeatPricingUtils.CalculateTotal(show.PrezzoBase, requestedSeats, vipSeats);

var appBaseUrl = (Environment.GetEnvironmentVariable("APP_BASE_URL") ?? "http://localhost:5001").TrimEnd('/');
var seatsRawEncoded = Uri.EscapeDataString(string.Join(',', requestedSeats));
var cancelUrl = $"{appBaseUrl}/pagamento.html?idShow={show.Id}&idFilm={show.FilmId}&idCinema={show.CinemaId}&posti={seatsRawEncoded}&cancelled=1";
var successUrl = $"{appBaseUrl}/esito-pagamento.html?session_id={{CHECKOUT_SESSION_ID}}";

var sessionService = new SessionService();
var options = new SessionCreateOptions
{
Mode = "payment",
SuccessUrl = successUrl,
CancelUrl = cancelUrl,
ClientReferenceId = $"{userId}:{show.Id}",
CustomerEmail = validation.UserEmail,
Metadata = new Dictionary<string, string>
{
["userId"] = userId.ToString(),
["proiezioneId"] = show.Id.ToString(),
["postiSelezionati"] = string.Join(',', requestedSeats),
["numeroPosti"] = requestedSeats.Count.ToString()
},
LineItems = new List<SessionLineItemOptions>
{
new()
{
Quantity = 1,
PriceData = new SessionLineItemPriceDataOptions
{
Currency = "eur",
UnitAmount = (long)Math.Round(total * 100m, MidpointRounding.AwayFromZero),
ProductData = new SessionLineItemPriceDataProductDataOptions
{
Name = $"{show.Film.Titolo} - {show.Cinema.Nome}",
Description = $"{show.Data:yyyy-MM-dd} {show.Ora:HH:mm} | Posti: {string.Join(',', requestedSeats)}"
}
}
}
}
};

var session = await sessionService.CreateAsync(options);

var booking = new Prenotazione
{
UtenteId = userId,
ProiezioneId = show.Id,
NumeroPosti = requestedSeats.Count,
PostiSelezionati = string.Join(',', requestedSeats),
TotalePrezzo = total,
ImportoCartaUsato = total,
StripeSessionId = session.Id,
CodiceAcquisto = BuildPendingCodiceAcquisto(),
DataPrenotazione = now,
Stato = "PendingStripe"
};

db.Prenotazioni.Add(booking);
await db.SaveChangesAsync();

return Results.Ok(new
{
sessionId = session.Id,
url = session.Url
});
}).RequireAuthorization();

group.MapPost("/cart-checkout", async (CartCheckoutRequest req, ClaimsPrincipal user, FilmDbContext db, ILoggerFactory loggerFactory, EmailService emailService) =>
{
var logger = loggerFactory.CreateLogger("PagamentiEndpoints");
if (!TryGetUserId(user, out var userId))
    return Results.Unauthorized();

if (string.IsNullOrWhiteSpace(stripeSecret))
    return Results.BadRequest(new { error = "Stripe non configurato" });

var cart = await db.Carts
    .Include(c => c.CartItems)
    .FirstOrDefaultAsync(c => c.Id == req.CartId && c.UtenteId == userId && c.Stato == "Active");

if (cart == null) return Results.BadRequest(new { error = "Carrello non trovato o non attivo" });
if (cart.CartItems.Count == 0) return Results.BadRequest(new { error = "Carrello vuoto" });

var now = DateTime.UtcNow;
var appBaseUrl = (Environment.GetEnvironmentVariable("APP_BASE_URL") ?? "http://localhost:5001").TrimEnd('/');
var userEmail = (await db.Utenti.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId))?.Email ?? "";

// Validate ticket items
foreach (var item in cart.CartItems.Where(ci => ci.ItemType == "Ticket"))
{
    var seats = (item.DettaglioJson != null ? System.Text.Json.JsonSerializer.Deserialize<SeatInfo>(item.DettaglioJson) : null)?.Posti ?? new List<string>();
    var selezionati = string.Join(',', seats);
    var validation = await ValidatePurchaseAsync(db, userId, item.ItemId, selezionati, false, null);
    if (!validation.Success)
        return Results.BadRequest(new { error = $"Posti non piu disponibili per lo show {item.ItemId}" });
}

// Calculate totals
var subtotale = cart.CartItems.Sum(i => i.PrezzoUnitario * i.Quantita);
var sconto = cart.ScontoCoupon;
var totaleDopoCoupon = Math.Max(0, subtotale - sconto);
var importoGiftCard = cart.ImportoGiftCard;
var stripeAmount = Math.Max(0, totaleDopoCoupon - importoGiftCard);

cart.UpdatedAtUtc = now;

// Build line items for Stripe
var lineItems = new List<SessionLineItemOptions>();
foreach (var item in cart.CartItems)
{
    var desc = item.ItemType switch
    {
        "Ticket" => $"Biglietto (ID show: {item.ItemId})",
        "GiftCard" => $"Gift Card {item.PrezzoUnitario:C}",
        "Merchandise" => $"Prodotto ID {item.ItemId}",
        _ => item.ItemType
    };
    lineItems.Add(new SessionLineItemOptions
    {
        Quantity = item.Quantita,
        PriceData = new SessionLineItemPriceDataOptions
        {
            Currency = "eur",
            UnitAmount = (long)Math.Round(item.PrezzoUnitario * 100m, MidpointRounding.AwayFromZero),
            ProductData = new SessionLineItemPriceDataProductDataOptions
            {
                Name = desc
            }
        }
    });
    if (!string.IsNullOrWhiteSpace(item.DettaglioJson))
        lineItems[^1].PriceData.ProductData.Description = item.DettaglioJson;
}

// If gift card covers everything, stripe amount is 0; still create session for record
if (stripeAmount <= 0 && importoGiftCard >= totaleDopoCoupon)
{
    // Full gift card payment - finalize immediately without Stripe
    await FinalizeCartOrderAsync(db, cart, userId, logger, emailService);
    return Results.Ok(new { redirectToStripe = false, message = "Pagamento completato con gift card", cartId = cart.Id });
}

    if (sconto > 0 && lineItems.Count > 0)
    {
        var totalItems = lineItems.Sum(li => (decimal)(li.PriceData.UnitAmount ?? 0) * (li.Quantity ?? 1));
        var remaining = (long)Math.Round(sconto * 100m, MidpointRounding.AwayFromZero);
        for (int i = 0; i < lineItems.Count && remaining > 0; i++)
        {
            var li = lineItems[i];
            var ua = li.PriceData.UnitAmount ?? 0;
            var qty = li.Quantity ?? 1;
            var itemTotal = (decimal)ua * qty;
            var share = totalItems > 0 ? (long)Math.Round(remaining * (itemTotal / totalItems)) : 0;
            var maxShare = ua * qty;
            share = Math.Min(share, maxShare);
            li.PriceData.UnitAmount = Math.Max(0, ua - share / qty);
            remaining -= share;
        }
    }

var sessionService = new SessionService();
var options = new SessionCreateOptions
{
    Mode = "payment",
    SuccessUrl = $"{appBaseUrl}/esito-pagamento.html?session_id={{CHECKOUT_SESSION_ID}}&cart=1",
    CancelUrl = $"{appBaseUrl}/cart.html",
    ClientReferenceId = $"cart:{userId}:{cart.Id}",
    CustomerEmail = userEmail,
    Metadata = new Dictionary<string, string>
    {
        ["userId"] = userId.ToString(),
        ["cartId"] = cart.Id.ToString(),
        ["importoGiftCard"] = importoGiftCard.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["giftCardCode"] = cart.GiftCardCode ?? ""
    },
    LineItems = lineItems
};

try
{
    var session = await sessionService.CreateAsync(options);
    cart.StripeSessionId = session.Id;
    await db.SaveChangesAsync();
    return Results.Ok(new { sessionId = session.Id, url = session.Url });
}
catch (Exception ex)
{
    logger.LogError(ex, "Stripe session creation failed for cart {CartId}", cart.Id);
    return Results.BadRequest(new { error = $"Errore pagamento: {ex.Message}" });
}
}).RequireAuthorization();

group.MapGet("/esito", async (string session_id, ClaimsPrincipal user, FilmDbContext db, TicketPdfService pdfService, TicketEmailService emailService, ILoggerFactory loggerFactory, EmailService emailSvc) =>
{
var logger = loggerFactory.CreateLogger("PagamentiEndpoints");

if (!TryGetUserId(user, out var userId))
{
return Results.Unauthorized();
}

if (string.IsNullOrWhiteSpace(session_id))
{
return Results.BadRequest(new { error = "session_id mancante" });
}

var booking = await db.Prenotazioni
.Include(p => p.Proiezione)
.ThenInclude(pr => pr.Film)
.Include(p => p.Proiezione)
.ThenInclude(pr => pr.Cinema)
.FirstOrDefaultAsync(p => p.StripeSessionId == session_id && p.UtenteId == userId);

if (booking is null)
{
// Check if this is a cart checkout
var cart = await db.Carts
    .Include(c => c.CartItems)
    .FirstOrDefaultAsync(c => c.StripeSessionId == session_id && c.UtenteId == userId && c.Stato == "Checkout");

if (cart != null && !string.IsNullOrWhiteSpace(stripeSecret))
{
    try
    {
        var sessionService = new SessionService();
        var stripeSession = await sessionService.GetAsync(session_id);
        if (stripeSession.PaymentStatus == "paid")
        {
            await FinalizeCartOrderAsync(db, cart, userId, logger, emailSvc);
        }
        return Results.Ok(new { stato = cart.Stato, cartId = cart.Id, items = cart.CartItems.Count });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Errore verifica esito cart checkout {SessionId}", session_id);
        return Results.Ok(new { stato = "Errore" });
    }
}

return Results.NotFound();
}

if (booking.Stato == "PendingStripe" && !string.IsNullOrWhiteSpace(stripeSecret))
{
try
{
var sessionService = new SessionService();
var session = await sessionService.GetAsync(session_id);
if (session.PaymentStatus == "paid")
{
await FinalizeBookingAsync(db, booking, requireLocks: false, pdfService, emailService, logger);
await db.Entry(booking).Reference(p => p.Proiezione).LoadAsync();
await db.Entry(booking.Proiezione).Reference(p => p.Film).LoadAsync();
await db.Entry(booking.Proiezione).Reference(p => p.Cinema).LoadAsync();
}
}
catch (Exception ex)
{
logger.LogError(ex, "Errore durante la verifica esito pagamento per sessione {SessionId}", session_id);
}
}

return Results.Ok(new
{
booking.Stato,
booking.CodiceAcquisto,
booking.PostiSelezionati,
booking.TotalePrezzo,
film = booking.Proiezione.Film.Titolo,
cinema = booking.Proiezione.Cinema.Nome
});
}).RequireAuthorization();

group.MapPost("/conferma", async (PrenotazioneCreateDTO dto, ClaimsPrincipal user, FilmDbContext db) =>
{
if (!TryGetUserId(user, out var userId))
{
return Results.Unauthorized();
}

if (dto.ProiezioneId <= 0 || dto.NumeroPosti <= 0)
{
return Results.BadRequest(new { error = "Dati acquisto non validi" });
}

if (dto.NumeroPosti > 10)
{
return Results.BadRequest(new { error = "Numero posti massimo superato (10)" });
}

if (string.IsNullOrWhiteSpace(dto.PostiSelezionati))
{
return Results.BadRequest(new { error = "Selezionare almeno un posto" });
}

var request = new StripeCheckoutSessionCreateDTO
{
ProiezioneId = dto.ProiezioneId,
PostiSelezionati = dto.PostiSelezionati
};

var validation = await ValidatePurchaseAsync(db, userId, request.ProiezioneId, request.PostiSelezionati, requireLocks: true, excludeBookingId: null);
if (!validation.Success)
{
return validation.ErrorResult!;
}

if (string.IsNullOrWhiteSpace(stripeSecret))
{
return Results.BadRequest(new { error = "Stripe non configurato: manca STRIPE_SECRET_KEY" });
}

var show = validation.Show!;
var requestedSeats = validation.RequestedSeats!;
var now = DateTime.UtcNow;
var vipSeats = FilmAPI.Services.SeatPricingUtils.GetVipSeats(show.Sala?.NumeroFile ?? 10, show.Sala?.PostiPerFila ?? 12, show.Sala?.MappaPostiJson);
var total = FilmAPI.Services.SeatPricingUtils.CalculateTotal(show.PrezzoBase, requestedSeats, vipSeats);

var appBaseUrl = (Environment.GetEnvironmentVariable("APP_BASE_URL") ?? "http://localhost:5001").TrimEnd('/');
var seatsRawEncoded = Uri.EscapeDataString(string.Join(',', requestedSeats));
var cancelUrl = $"{appBaseUrl}/pagamento.html?idShow={show.Id}&idFilm={show.FilmId}&idCinema={show.CinemaId}&posti={seatsRawEncoded}&cancelled=1";
var successUrl = $"{appBaseUrl}/esito-pagamento.html?session_id={{CHECKOUT_SESSION_ID}}";

var sessionService = new SessionService();
var options = new SessionCreateOptions
{
Mode = "payment",
SuccessUrl = successUrl,
CancelUrl = cancelUrl,
ClientReferenceId = $"{userId}:{show.Id}",
CustomerEmail = validation.UserEmail,
Metadata = new Dictionary<string, string>
{
["userId"] = userId.ToString(),
["proiezioneId"] = show.Id.ToString(),
["postiSelezionati"] = string.Join(',', requestedSeats),
["numeroPosti"] = requestedSeats.Count.ToString()
},
LineItems = new List<SessionLineItemOptions>
{
new()
{
Quantity = 1,
PriceData = new SessionLineItemPriceDataOptions
{
Currency = "eur",
UnitAmount = (long)Math.Round(total * 100m, MidpointRounding.AwayFromZero),
ProductData = new SessionLineItemPriceDataProductDataOptions
{
Name = $"{show.Film.Titolo} - {show.Cinema.Nome}",
Description = $"{show.Data:yyyy-MM-dd} {show.Ora:HH:mm} | Posti: {string.Join(',', requestedSeats)}"
}
}
}
}
};

var session = await sessionService.CreateAsync(options);

var booking = new Prenotazione
{
UtenteId = userId,
ProiezioneId = show.Id,
NumeroPosti = requestedSeats.Count,
PostiSelezionati = string.Join(',', requestedSeats),
TotalePrezzo = total,
ImportoCartaUsato = total,
StripeSessionId = session.Id,
CodiceAcquisto = BuildPendingCodiceAcquisto(),
DataPrenotazione = now,
Stato = "PendingStripe"
};

db.Prenotazioni.Add(booking);
await db.SaveChangesAsync();

return Results.Ok(new
{
success = true,
redirectToStripe = true,
sessionId = session.Id,
stripeUrl = session.Url,
importoCartaUsato = total,
totale = total,
stato = "PendingStripe"
});
}).RequireAuthorization();

group.MapPost("/stripe/webhook", async (HttpRequest request, FilmDbContext db, TicketPdfService pdfService, TicketEmailService emailService, ILoggerFactory loggerFactory) =>
{
var logger = loggerFactory.CreateLogger("PagamentiEndpoints");

if (string.IsNullOrWhiteSpace(stripeSecret))
{
return Results.BadRequest(new { error = "Stripe non configurato" });
}

var webhookSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET") ?? string.Empty;
if (string.IsNullOrWhiteSpace(webhookSecret))
{
return Results.BadRequest(new { error = "Webhook Stripe non configurato" });
}

string json;
using (var reader = new StreamReader(request.Body))
{
json = await reader.ReadToEndAsync();
}

Event stripeEvent;
try
{
var signatureHeader = request.Headers["Stripe-Signature"];
stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, webhookSecret);
}
catch (Exception ex)
{
return Results.BadRequest(new { error = $"Firma webhook non valida: {ex.Message}" });
}

if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
{
var session = stripeEvent.Data.Object as Session;
if (session is null)
{
return Results.BadRequest(new { error = "Sessione Stripe non valida" });
}

var booking = await db.Prenotazioni
.Include(p => p.Proiezione)
.ThenInclude(pr => pr.Film)
.Include(p => p.Proiezione)
.ThenInclude(pr => pr.Cinema)
.FirstOrDefaultAsync(p => p.StripeSessionId == session.Id);

if (booking is null)
{
return Results.NotFound();
}

if (booking.Stato == "Confermata")
{
return Results.Ok(new { received = true, idempotent = true });
}

if (booking.Stato != "PendingStripe")
{
return Results.Conflict(new { error = "Stato prenotazione non valido per conferma" });
}

if (session.PaymentStatus != "paid")
{
return Results.Ok(new { received = true, ignored = true });
}

var finalized = await FinalizeBookingAsync(db, booking, requireLocks: false, pdfService, emailService, logger);
if (!finalized)
{
booking.Stato = "Fallita";
await db.SaveChangesAsync();
return Results.Conflict(new { error = "Validazione posti fallita durante conferma webhook" });
}
}

return Results.Ok(new { received = true });
}).AllowAnonymous();

return group;
}

private static async Task FinalizeCartOrderAsync(FilmDbContext db, Cart cart, int userId, ILogger logger, EmailService? emailService = null)
{
    var now = DateTime.UtcNow;

    // Deduct gift card balance if used
    if (!string.IsNullOrWhiteSpace(cart.GiftCardCode) && cart.ImportoGiftCard > 0)
    {
        var gc = await db.GiftCards.FirstOrDefaultAsync(g => g.Codice == cart.GiftCardCode && g.Stato == "Active");
        if (gc != null)
        {
            var deduct = Math.Min(cart.ImportoGiftCard, gc.SaldoResiduo);
            gc.SaldoResiduo -= deduct;
            if (gc.SaldoResiduo <= 0) { gc.SaldoResiduo = 0; gc.Stato = "Consumed"; }
            db.GiftCardTransactions.Add(new GiftCardTransaction
            {
                GiftCardId = gc.Id, CartId = cart.Id, Tipo = "Redemption", Importo = deduct, SaldoDopo = gc.SaldoResiduo
            });
        }
    }

    // Process ticket items -> Prenotazioni
    var ticketItems = await db.CartItems.Where(ci => ci.CartId == cart.Id && ci.ItemType == "Ticket").ToListAsync();
    var allBookings = new List<Prenotazione>();
    foreach (var item in ticketItems)
    {
        var seats = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.DettaglioJson))
        {
            try { var info = System.Text.Json.JsonSerializer.Deserialize<SeatInfo>(item.DettaglioJson); seats = info?.Posti ?? new List<string>(); }
            catch { }
        }
        var codice = BuildCodiceAcquisto();
        while (await db.Prenotazioni.AnyAsync(p => p.CodiceAcquisto == codice)) { codice = BuildCodiceAcquisto(); }
        var booking = new Prenotazione
        {
            UtenteId = userId, ProiezioneId = item.ItemId, NumeroPosti = Math.Max(1, seats.Count),
            PostiSelezionati = string.Join(',', seats), TotalePrezzo = item.PrezzoUnitario * item.Quantita,
            ImportoCartaUsato = item.PrezzoUnitario * item.Quantita, CodiceAcquisto = codice,
            DataPrenotazione = now, Stato = "Confermata", CartId = cart.Id
        };
        db.Prenotazioni.Add(booking);
        allBookings.Add(booking);
    }

    // Process gift card items -> generate GiftCard records
    var gcItems = await db.CartItems.Where(ci => ci.CartId == cart.Id && ci.ItemType == "GiftCard").ToListAsync();
    foreach (var item in gcItems)
    {
        string detailJson = item.DettaglioJson ?? "{}";
        string? destinatario = null; string? messaggio = null;
        try
        {
            var d = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(detailJson);
            if (d.TryGetProperty("emailDestinatario", out var e) && e.ValueKind == System.Text.Json.JsonValueKind.String) destinatario = e.GetString();
            if (d.TryGetProperty("messaggio", out var m) && m.ValueKind == System.Text.Json.JsonValueKind.String) messaggio = m.GetString();
        } catch { }
        for (int i = 0; i < item.Quantita; i++)
        {
            var code = "NFH-GC-" + Guid.NewGuid().ToString("N")[..8].ToUpper() + "-" + Guid.NewGuid().ToString("N")[..4].ToUpper();
            db.GiftCards.Add(new GiftCard
            {
                        Codice = code, ImportoIniziale = item.PrezzoUnitario, SaldoResiduo = item.PrezzoUnitario,
                        UtenteAcquirenteId = userId, EmailDestinatario = destinatario, Messaggio = messaggio, Stato = "Active", CreatoIl = now, Scadenza = now.AddYears(1)
            });
        }
    }

    // Process merchandise items -> decrement stock
    var merchItems = await db.CartItems.Where(ci => ci.CartId == cart.Id && ci.ItemType == "Merchandise").ToListAsync();
    foreach (var item in merchItems)
    {
        if (item.VariantId.HasValue)
        {
            var variant = await db.ProductVariants.FindAsync(item.VariantId.Value);
            if (variant != null) variant.Stock = Math.Max(0, variant.Stock - item.Quantita);
        }
    }

    // Track coupon usage if applied
    if (cart.CouponId.HasValue)
    {
        var coupon = await db.Coupons.FindAsync(cart.CouponId.Value);
        if (coupon != null)
        {
            coupon.UtilizziAttuali++;
            db.CouponUsages.Add(new CouponUsage
            {
                CouponId = coupon.Id,
                UtenteId = userId,
                CartId = cart.Id,
                ScontoApplicato = cart.ScontoCoupon
            });
        }
    }

    // Remove seat locks
    await db.SeatLocks.Where(l => l.CartId == cart.Id).ExecuteDeleteAsync();
    await db.InventoryReservations.Where(r => r.CartId == cart.Id).ExecuteDeleteAsync();

    cart.Stato = "Converted";
    cart.UpdatedAtUtc = now;
    await db.SaveChangesAsync();

    // Send gift card emails and balance notifications
    if (emailService != null)
    {
        var utente = await db.Utenti.FindAsync(userId);
        var userEmail = utente?.Email ?? "";

        // Collect all gift cards just created for this order
        var createdGc = await db.GiftCards
            .Where(g => g.UtenteAcquirenteId == userId && g.CreatoIl >= now.AddSeconds(-30) && g.CreatoIl <= now.AddSeconds(5))
            .ToListAsync();

        // Send one email per gift card code to the purchaser
        foreach (var gc in createdGc)
        {
            string? destinatario = gc.EmailDestinatario;
            await emailService.SendGiftCardEmail(userEmail, gc.Codice, gc.ImportoIniziale, gc.Messaggio);
            // If different recipient, also notify them
            if (!string.IsNullOrWhiteSpace(destinatario) && !string.Equals(destinatario, userEmail, StringComparison.OrdinalIgnoreCase))
                await emailService.SendGiftCardEmail(destinatario, gc.Codice, gc.ImportoIniziale, gc.Messaggio);
        }

        // Send order confirmation email to purchaser (summary, no sensitive codes)
        await emailService.SendOrderConfirmationEmail(userEmail, cart.Id, cart.Subtotale, cart.ScontoCoupon, cart.ImportoGiftCard, cart.Totale, createdGc.Count, cart.CartItems.Count(ci => ci.ItemType == "Ticket"));

        // Notify about gift card balance used
        if (!string.IsNullOrWhiteSpace(cart.GiftCardCode) && cart.ImportoGiftCard > 0)
        {
            var gcUsed = await db.GiftCards.FirstOrDefaultAsync(g => g.Codice == cart.GiftCardCode);
            if (gcUsed != null)
            {
                var gcOwner = await db.Utenti.FindAsync(gcUsed.UtenteAcquirenteId);
                if (gcOwner != null)
                    await emailService.SendGiftCardBalanceEmail(gcOwner.Email, gcUsed.Codice, gcUsed.SaldoResiduo);
            }
        }
    }
}

private static async Task<(bool Success, IResult? ErrorResult, Proiezione? Show, List<string>? RequestedSeats, string? UserEmail)> ValidatePurchaseAsync(
FilmDbContext db,
int userId,
int proiezioneId,
string postiSelezionati,
bool requireLocks,
int? excludeBookingId)
{
if (proiezioneId <= 0)
{
return (false, Results.BadRequest(new { error = "Dati acquisto non validi" }), null, null, null);
}

var show = await db.Proiezioni
.Include(p => p.Cinema)
.Include(p => p.Film)
.Include(p => p.Sala)
.FirstOrDefaultAsync(p => p.Id == proiezioneId);
if (show is null)
{
return (false, Results.BadRequest(new { error = "Show non trovato" }), null, null, null);
}

var utente = await db.Utenti.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
if (utente is null)
{
return (false, Results.Unauthorized(), null, null, null);
}

var requestedSeats = (postiSelezionati ?? string.Empty)
.Split(',', StringSplitOptions.RemoveEmptyEntries)
.Select(x => x.Trim().ToUpperInvariant())
.Where(x => !string.IsNullOrWhiteSpace(x))
.Distinct()
.ToList();

if (requestedSeats.Count == 0 || requestedSeats.Count > 10)
{
return (false, Results.BadRequest(new { error = "Numero posti non valido" }), null, null, null);
}

if (requireLocks)
{
var now = DateTime.UtcNow;
var validLocks = await db.SeatLocks
.AsNoTracking()
.Where(l => l.ProiezioneId == proiezioneId && l.UtenteId == userId && l.ExpiresAtUtc > now)
.ToListAsync();

var lockSet = validLocks.Select(l => l.PostoCodice).ToHashSet();
var missingLocks = requestedSeats.Where(seat => !lockSet.Contains(seat)).ToList();
if (missingLocks.Count > 0)
{
return (false, Results.Conflict(new { error = "Lock posti non valido o scaduto", posti = missingLocks }), null, null, null);
}
}

var soldQuery = db.Prenotazioni
.AsNoTracking()
.Where(p => p.ProiezioneId == proiezioneId && p.Stato != "Annullata" && p.Stato != "Fallita" && p.Stato != "PendingStripe");

if (excludeBookingId.HasValue)
{
soldQuery = soldQuery.Where(p => p.Id != excludeBookingId.Value);
}

var soldRows = await soldQuery
.Select(p => p.PostiSelezionati)
.ToListAsync();

var soldSet = ExpandSeats(soldRows).ToHashSet();
var conflicts = requestedSeats.Where(seat => soldSet.Contains(seat)).ToList();
if (conflicts.Count > 0)
{
return (false, Results.Conflict(new { error = "Uno o piu posti risultano gia acquistati", posti = conflicts }), null, null, null);
}

return (true, null, show, requestedSeats, utente.Email);
}

private static async Task<bool> FinalizeBookingAsync(
FilmDbContext db,
Prenotazione booking,
bool requireLocks,
TicketPdfService pdfService,
TicketEmailService emailService,
ILogger logger)
{
if (booking.Stato == "Confermata")
{
return true;
}

var validation = await ValidatePurchaseAsync(
db,
booking.UtenteId,
booking.ProiezioneId,
booking.PostiSelezionati,
requireLocks,
booking.Id);

if (!validation.Success)
{
return false;
}

var codice = BuildCodiceAcquisto();
while (await db.Prenotazioni.AnyAsync(p => p.CodiceAcquisto == codice))
{
codice = BuildCodiceAcquisto();
}

booking.CodiceAcquisto = codice;
booking.Stato = "Confermata";
booking.ImportoCartaUsato = booking.TotalePrezzo;

var requestedSeats = validation.RequestedSeats!;
var locks = await db.SeatLocks
.Where(l => l.ProiezioneId == booking.ProiezioneId && requestedSeats.Contains(l.PostoCodice))
.ToListAsync();
db.SeatLocks.RemoveRange(locks);

await db.SaveChangesAsync();

try
{
await db.Entry(booking).Reference(p => p.Utente).LoadAsync();
await db.Entry(booking).Reference(p => p.Proiezione).LoadAsync();
await db.Entry(booking.Proiezione).Reference(p => p.Film).LoadAsync();
await db.Entry(booking.Proiezione).Reference(p => p.Cinema).LoadAsync();
await db.Entry(booking.Proiezione).Reference(p => p.Sala!).LoadAsync();

var validateBaseUrl = Environment.GetEnvironmentVariable("APP_BASE_URL") ?? "http://localhost:5001";
var pdfBytes = pdfService.GenerateOrderPdf(booking, validateBaseUrl);
var emailSent = await emailService.SendTicketEmailAsync(booking.Utente.Email, booking.CodiceAcquisto, pdfBytes);
if (!emailSent)
{
logger.LogWarning("Email di conferma non inviata per ordine {CodiceAcquisto} a {Email}", booking.CodiceAcquisto, booking.Utente.Email);
}
}
catch (Exception ex)
{
logger.LogError(ex, "Errore durante generazione PDF o invio email per ordine {CodiceAcquisto}", booking.CodiceAcquisto);
}

return true;
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

private static string BuildCodiceAcquisto()
{
var random = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
return $"NFH-{DateTime.UtcNow:yyyyMMddHHmmss}-{random}";
}

private static string BuildPendingCodiceAcquisto()
{
var random = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
return $"PENDING-{random}";
}

private static bool TryGetUserId(ClaimsPrincipal user, out int userId)
{
var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
return int.TryParse(userIdValue, out userId);
}

public class CartCheckoutRequest
{
    public int CartId { get; set; }
}

public class SeatInfo
{
    public List<string> Posti { get; set; } = new();
}
}
