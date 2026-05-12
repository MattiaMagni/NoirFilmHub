using System.Security.Claims;
using System.Text;

using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using FilmAPI.Data;
using FilmAPI.Endpoints;
using FilmAPI.Model;
using FilmAPI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using System.Text.Json.Serialization;
using QuestPDF.Infrastructure;

Env.Load();

var builder = WebApplication.CreateBuilder(args);
QuestPDF.Settings.License = LicenseType.Community;

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFilmFrontend", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
                var host = uri.Host.ToLowerInvariant();
                return host == "localhost" || host == "127.0.0.1";
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var dbUseAutoDetect = (Environment.GetEnvironmentVariable("DB_USE_AUTODETECT") ?? "true")
    .Equals("true", StringComparison.OrdinalIgnoreCase);
var dbServerVersion = Environment.GetEnvironmentVariable("DB_SERVER_VERSION") ?? "10.11.0-mariadb";
var dbProvider = Environment.GetEnvironmentVariable("DB_PROVIDER") ?? "MySql";

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TmdbService>();
builder.Services.AddScoped<TicketPdfService>();
builder.Services.AddScoped<TicketEmailService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<SecurityAuditService>();
builder.Services.AddScoped<SocialAuthService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<TmdbSyncHostedService>();
builder.Services.AddHostedService<CleanupHostedService>();

var authEnabled = (Environment.GetEnvironmentVariable("AUTH_ENABLED") ?? "true")
    .Equals("true", StringComparison.OrdinalIgnoreCase);

if (authEnabled)
{
    var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? "dev-secret-key-change-in-production-123456";
    var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "FilmAPI";
    var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "FilmFrontend";

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ClockSkew = TimeSpan.FromSeconds(30),
                RoleClaimType = ClaimTypes.Role,
                NameClaimType = ClaimTypes.NameIdentifier
            };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    var authVersionClaim = context.Principal?.FindFirst("auth_version")?.Value;

                    if (userIdClaim == null || authVersionClaim == null)
                    {
                        context.Fail("Token claims incompleti");
                        return;
                    }

                    var dbContext = context.HttpContext.RequestServices.GetRequiredService<FilmDbContext>();
                    var utente = await dbContext.Utenti
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Id == int.Parse(userIdClaim));

                    if (utente == null)
                    {
                        context.Fail("Utente non trovato");
                        return;
                    }

                    if (utente.IsDisabled)
                    {
                        context.Fail("Account disabilitato");
                        return;
                    }

                    if (utente.AuthVersion.ToString() != authVersionClaim)
                    {
                        context.Fail("Token invalidato - sessione scaduta");
                        return;
                    }

                    var iatClaim = context.Principal?.FindFirst("iat")?.Value;
                    if (iatClaim != null && utente.PasswordChangedAtUtc.HasValue)
                    {
                        var iat = DateTimeOffset.FromUnixTimeSeconds(long.Parse(iatClaim)).UtcDateTime;
                        if (utente.PasswordChangedAtUtc.Value > iat)
                        {
                            context.Fail("Password cambiata - token invalidato");
                            return;
                        }
                    }
                }
            };
        });
}
else
{
    builder.Services
        .AddAuthentication("TestAuth")
        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestAuth", _ => { });
}

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole(RuoloUtente.Admin))
    .AddPolicy("AdminOrPowerUser", policy => policy.RequireRole(RuoloUtente.Admin, RuoloUtente.PowerUser));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var host2 = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "3306";
var name = Environment.GetEnvironmentVariable("DB_NAME") ?? "film-api-db";
var user = Environment.GetEnvironmentVariable("DB_USER") ?? "root";
var pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "root";
var connectionString = $"Server={host2};Port={port};Database={name};User Id={user};Password={pass};";

var serverVersion = dbUseAutoDetect
    ? ServerVersion.AutoDetect(connectionString)
    : ServerVersion.Parse(dbServerVersion);
var testDbName = Environment.GetEnvironmentVariable("TEST_DB_NAME") ?? "FilmApiTests";

if (dbProvider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContext<FilmDbContext>(dbOptions => dbOptions
        .UseInMemoryDatabase(testDbName)
        .EnableSensitiveDataLogging()
        .EnableDetailedErrors());
}
else
{
    builder.Services.AddDbContext<FilmDbContext>(dbOptions => dbOptions
        .UseMySql(connectionString, serverVersion)
        .LogTo(Console.WriteLine, LogLevel.Information)
        .EnableSensitiveDataLogging()
        .EnableDetailedErrors());
}

var app = builder.Build();

app.UseCors("AllowFilmFrontend");
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FilmAPI v1");
        c.RoutePrefix = "swagger";
    });
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FilmDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        db.Database.Migrate();

        // Cleanup: delete all existing users, then create ONLY the admin
        var resetUsers = (Environment.GetEnvironmentVariable("RESET_USERS") ?? "false")
            .Equals("true", StringComparison.OrdinalIgnoreCase);
        if (resetUsers)
        {
            await db.UserSecurityAuditLogs.ExecuteDeleteAsync();
            await db.AccountActionTokens.ExecuteDeleteAsync();
            await db.UserExternalLogins.ExecuteDeleteAsync();
            await db.ExternalAuthExchangeCodes.ExecuteDeleteAsync();
            await db.ExternalAuthStates.ExecuteDeleteAsync();
            await db.Prenotazioni.ExecuteDeleteAsync();
            await db.Utenti.ExecuteDeleteAsync();
            logger.LogInformation("All users and related data deleted for reset.");
        }

        var usersWithNullNormalized = await db.Utenti
            .Where(u => u.NormalizedEmail == null || u.NormalizedEmail == "")
            .ToListAsync();
        foreach (var u in usersWithNullNormalized)
        {
            u.NormalizedEmail = u.Email.ToUpperInvariant();
            u.LocalCredentialsEnabled = true;
            u.AuthVersion = 1;
            u.SecurityStamp = Guid.NewGuid().ToString("N");
            u.CreatedAtUtc = DateTime.UtcNow;
            u.EmailVerified = true;
        }
        if (usersWithNullNormalized.Count > 0)
            await db.SaveChangesAsync();

        var adminEmail = Environment.GetEnvironmentVariable("DEFAULT_ADMIN_EMAIL") ?? "admin@filmapi.local";
        var adminPassword = Environment.GetEnvironmentVariable("DEFAULT_ADMIN_PASSWORD") ?? "Admin123!";

        var adminExists = await db.Utenti.AnyAsync(x => x.Email == adminEmail);
        if (!adminExists)
        {
            db.Utenti.Add(new Utente
            {
                Email = adminEmail,
                NormalizedEmail = adminEmail.ToUpperInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                Nome = "Admin",
                Cognome = "Sistema",
                Telefono = string.Empty,
                Ruolo = RuoloUtente.Admin,
                LocalCredentialsEnabled = true,
                AuthVersion = 1,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                EmailVerified = true,
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        if (!await db.Registi.AnyAsync())
        {
            db.Registi.AddRange(
                new Regista { Nome = "Denis", Cognome = "Villeneuve", Nazionalita = "CA" },
                new Regista { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "UK" },
                new Regista { Nome = "Greta", Cognome = "Gerwig", Nazionalita = "US" }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.Cinemas.AnyAsync())
        {
            db.Cinemas.AddRange(
                new Cinema { Nome = "Noir Cinemas Milano", Citta = "Milano", Indirizzo = "Via Torino 10", Capienza = 260, CodiceLocale = "0131220507688", Latitudine = 45.4642, Longitudine = 9.1900, Attivo = true },
                new Cinema { Nome = "Noir Cinemas Lissone", Citta = "Lissone", Indirizzo = "Viale Martiri 20", Capienza = 220, CodiceLocale = "0131220507689", Latitudine = 45.6160, Longitudine = 9.2400, Attivo = true }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.Sale.AnyAsync())
        {
            var cinemas = await db.Cinemas.OrderBy(c => c.Id).ToListAsync();
            foreach (var cinema in cinemas)
            {
                db.Sale.AddRange(
                    new Sala { CinemaId = cinema.Id, NumeroProgressivo = 1, Tipologia = "ISENSE", Nome = "SALA 1", NumeroFile = 11, PostiPerFila = 18, MappaPostiJson = BuildSeatMapJson(11, 18, 2), Attiva = true },
                    new Sala { CinemaId = cinema.Id, NumeroProgressivo = 2, Tipologia = "XL", Nome = "SALA 2", NumeroFile = 12, PostiPerFila = 20, MappaPostiJson = BuildSeatMapJson(12, 20, 2), Attiva = true },
                    new Sala { CinemaId = cinema.Id, NumeroProgressivo = 3, Tipologia = "3D", Nome = "SALA 3", NumeroFile = 10, PostiPerFila = 16, MappaPostiJson = BuildSeatMapJson(10, 16, 2), Attiva = true },
                    new Sala { CinemaId = cinema.Id, NumeroProgressivo = 4, Tipologia = "2D", Nome = "SALA 4", NumeroFile = 10, PostiPerFila = 14, MappaPostiJson = BuildSeatMapJson(10, 14, 2), Attiva = true }
                );
            }
            await db.SaveChangesAsync();
        }

        var existingSale = await db.Sale.Where(s => string.IsNullOrWhiteSpace(s.MappaPostiJson)).ToListAsync();
        if (existingSale.Count > 0)
        {
            foreach (var sala in existingSale)
                sala.MappaPostiJson = BuildSeatMapJson(sala.NumeroFile, sala.PostiPerFila, 2);
            await db.SaveChangesAsync();
        }

        if (!await db.Films.AnyAsync())
        {
            var registi = await db.Registi.OrderBy(r => r.Id).ToListAsync();
            var categories = await db.Categorie.AsNoTracking().ToListAsync();
            var filmList = new List<Film>
            {
                new Film { Titolo = "Dune: Parte Due", TitoloOriginale = "Dune: Part Two", DataProduzione = new DateTime(2024, 1, 1), DataUscita = new DateTime(2024, 2, 28), RegistaId = registi[0].Id, Durata = 166, DescrizioneLunga = "Paul Atreides affronta la guerra su Arrakis mentre il suo destino si compie.", CastPrincipale = "Timothee Chalamet, Zendaya, Rebecca Ferguson", CopertinaPath = "https://image.tmdb.org/t/p/w500/8b8R8l88Qje9dn9OE8PY05Nxl1X.jpg", FilmatoPath = "https://www.youtube.com/watch?v=Way9Dexny3w", TmdbSyncStato = "Seeded" },
                new Film { Titolo = "Oppenheimer", TitoloOriginale = "Oppenheimer", DataProduzione = new DateTime(2023, 1, 1), DataUscita = new DateTime(2023, 8, 23), RegistaId = registi[1].Id, Durata = 180, DescrizioneLunga = "La storia dello scienziato che ha guidato il progetto Manhattan.", CastPrincipale = "Cillian Murphy, Emily Blunt, Robert Downey Jr.", CopertinaPath = "https://image.tmdb.org/t/p/w500/ptpr0kGAckfQkJeJIt8st5dglvd.jpg", FilmatoPath = "https://www.youtube.com/watch?v=uYPbbksJxIg", TmdbSyncStato = "Seeded" },
                new Film { Titolo = "Barbie", TitoloOriginale = "Barbie", DataProduzione = new DateTime(2023, 1, 1), DataUscita = new DateTime(2023, 7, 20), RegistaId = registi[2].Id, Durata = 114, DescrizioneLunga = "Barbie entra nel mondo reale e scopre se stessa.", CastPrincipale = "Margot Robbie, Ryan Gosling", CopertinaPath = "https://image.tmdb.org/t/p/w500/iuFNMS8U5cb6xfzi51Dbkovj7vM.jpg", FilmatoPath = "https://www.youtube.com/watch?v=pBk4NYhWNMM", TmdbSyncStato = "Seeded" }
            };
            db.Films.AddRange(filmList);
            await db.SaveChangesAsync();

            if (categories.Count > 0)
            {
                foreach (var film in filmList)
                {
                    var categoriaId = film.Titolo.Contains("Barbie", StringComparison.OrdinalIgnoreCase)
                        ? categories.FirstOrDefault(x => x.Nome == "Commedia")?.Id
                        : categories.FirstOrDefault(x => x.Nome == "Azione")?.Id;
                    if (categoriaId.HasValue)
                        db.FilmCategorie.Add(new FilmCategoria { FilmId = film.Id, CategoriaId = categoriaId.Value });
                }
                await db.SaveChangesAsync();
            }
        }

        if (!await db.Proiezioni.AnyAsync())
        {
            var films = await db.Films.AsNoTracking().ToListAsync();
            var sale = await db.Sale.AsNoTracking().ToListAsync();
            var today = DateTime.Today;
            var random = new Random(42);
            foreach (var s in sale)
            {
                for (var d = 0; d < 7; d++)
                {
                    var date = today.AddDays(d);
                    var film = films[(s.Id + d) % films.Count];
                    var starts = new[] { 16, 19, 21 };
                    foreach (var startHour in starts)
                    {
                        db.Proiezioni.Add(new Proiezione
                        {
                            CinemaId = s.CinemaId,
                            SalaId = s.Id,
                            FilmId = film.Id,
                            Data = date,
                            Ora = new DateTime(date.Year, date.Month, date.Day, startHour, random.Next(0, 2) * 30, 0),
                            PrezzoBase = s.Tipologia == "ISENSE" ? 12.90m : s.Tipologia == "XL" ? 11.90m : s.Tipologia == "3D" ? 10.90m : 8.90m
                        });
                    }
                }
            }
            await db.SaveChangesAsync();
        }

        // Seed shop data
        if (!await db.GiftCardTemplates.AnyAsync())
        {
            db.GiftCardTemplates.AddRange(
                new GiftCardTemplate { Nome = "Gift Card 10 EUR", Importo = 10m, Attivo = true },
                new GiftCardTemplate { Nome = "Gift Card 20 EUR", Importo = 20m, Attivo = true },
                new GiftCardTemplate { Nome = "Gift Card 30 EUR", Importo = 30m, Attivo = true },
                new GiftCardTemplate { Nome = "Gift Card 50 EUR", Importo = 50m, Attivo = true }
            );
        }

        if (!await db.Prodotti.AnyAsync())
        {
            db.Prodotti.AddRange(
                new Product { Sku = "NFH-POP-L", Nome = "Ciotola Popcorn Grande", Descrizione = "Ciotola riutilizzabile per popcorn con logo Noir Film Hub. Capacità 2L.", Categoria = "Food", PrezzoBase = 8.90m },
                new Product { Sku = "NFH-POP-S", Nome = "Ciotola Popcorn Piccola", Descrizione = "Ciotola compatta per popcorn, perfetta per i bambini.", Categoria = "Food", PrezzoBase = 5.90m },
                new Product { Sku = "NFH-BOR-500", Nome = "Boraccia Noir 500ml", Descrizione = "Boraccia termica in acciaio con logo Noir Film Hub. 500ml.", Categoria = "Accessori", PrezzoBase = 14.90m },
                new Product { Sku = "NFH-TSH-M", Nome = "T-Shirt Noir Film Hub", Descrizione = "T-shirt nera in cotone organico con stampa logo frontale.", Categoria = "Abbigliamento", PrezzoBase = 19.90m },
                new Product { Sku = "NFH-HOD-BK", Nome = "Felpa con Cappuccio Noir", Descrizione = "Felpa nera con cappuccio e stampa logo. Cotone misto 80%.", Categoria = "Abbigliamento", PrezzoBase = 39.90m },
                new Product { Sku = "NFH-PIN-SET", Nome = "Set Spille da Collezione", Descrizione = "Set di 3 spille smaltate con icone cinema. Collezione limitata.", Categoria = "Gadget", PrezzoBase = 12.90m },
                new Product { Sku = "NFH-TOTE-BK", Nome = "Tote Bag Noir Film Hub", Descrizione = "Borsa in tela con stampa Noir Film Hub. 40x35cm.", Categoria = "Accessori", PrezzoBase = 7.90m }
            );
        }

        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Automatic database migration failed; the application will continue.");
    }
}

app.UseStatusCodePages(async statusContext =>
{
    var response = statusContext.HttpContext.Response;
    if (response.HasStarted) return;
    if (response.StatusCode == StatusCodes.Status401Unauthorized)
    {
        response.ContentType = "application/json";
        await response.WriteAsJsonAsync(new { error = "Non autenticato" });
    }
    else if (response.StatusCode == StatusCodes.Status403Forbidden)
    {
        response.ContentType = "application/json";
        await response.WriteAsJsonAsync(new { error = "Permessi insufficienti" });
    }
});

app.MapGet("/", () => Results.Ok("FilmAPI running"));

app.MapGroup("/registi").MapRegisti();
app.MapGroup("/films").MapFilms();
app.MapGroup("/cinemas").MapCinemas();
app.MapGroup("/proiezioni").MapProiezioni();
app.MapGroup("/auth").MapAuth();
app.MapGroup("/categorie").MapCategorie();
app.MapGroup("/prenotazioni").MapPrenotazioni();
app.MapGroup("/sale").MapSale();
app.MapGroup("/programmazione").MapProgrammazione();
app.MapGroup("/my-cinemas").MapMyCinemas();
app.MapGroup("/checkout").MapCheckout();
app.MapGroup("/pagamenti").MapPagamenti();
app.MapGroup("/tickets").MapBiglietti();
app.MapGroup("/tmdb").MapTmdb();
app.MapGroup("/cart").MapCart();
app.MapGroup("/shop").MapShop();
app.MapGroup("/coupons").MapCoupons();
app.MapGroup("/giftcards").MapGiftCards();

app.Run();

static string BuildSeatMapJson(int rows, int cols, int aisleWidth)
{
    var safeRows = Math.Clamp(rows, 1, 26);
    var safeCols = Math.Clamp(cols, 4, 50);
    var safeAisle = Math.Clamp(aisleWidth, 0, 4);
    var centerStart = safeAisle > 0 ? Math.Max(0, (safeCols / 2) - (safeAisle / 2)) : -1;
    var centerEnd = safeAisle > 0 ? Math.Min(safeCols - 1, centerStart + safeAisle - 1) : -1;
    var seats = new List<string>(safeRows * safeCols);
    for (var r = 0; r < safeRows; r++)
    {
        var rowCode = ((char)('A' + r)).ToString();
        for (var c = 0; c < safeCols; c++)
        {
            if (safeAisle > 0 && c >= centerStart && c <= centerEnd) continue;
            seats.Add($"{rowCode}{c + 1}");
        }
    }
    return JsonSerializer.Serialize(new { rows = safeRows, cols = safeCols, seats });
}

public partial class Program;
