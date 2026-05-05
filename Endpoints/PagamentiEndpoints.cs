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
var total = decimal.Round(requestedSeats.Count * show.PrezzoBase, 2);

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

group.MapGet("/esito", async (string session_id, ClaimsPrincipal user, FilmDbContext db, TicketPdfService pdfService, TicketEmailService emailService, ILoggerFactory loggerFactory) =>
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
var total = decimal.Round(requestedSeats.Count * show.PrezzoBase, 2);

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
}
