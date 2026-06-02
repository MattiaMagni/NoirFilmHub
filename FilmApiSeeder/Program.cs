using FilmAPI.Data;
using FilmAPI.Model;
using FilmAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Wait for MariaDB to be ready
var connectionString = BuildConnectionString();
Console.WriteLine($"[Seeder] Waiting for MariaDB at {Environment.GetEnvironmentVariable("DB_HOST") ?? "mariadb"}:{Environment.GetEnvironmentVariable("DB_PORT") ?? "3306"}...");

var dbReady = false;
for (var i = 0; i < 30; i++)
{
    try
    {
        using var testDb = CreateDbContext(connectionString);
        await testDb.Database.CanConnectAsync();
        dbReady = true;
        Console.WriteLine("[Seeder] MariaDB is ready.");
        break;
    }
    catch
    {
        Console.WriteLine($"[Seeder] Attempt {i + 1}/30 — waiting 2s...");
        await Task.Delay(2000);
    }
}

if (!dbReady)
{
    Console.WriteLine("[Seeder] ERROR: MariaDB not reachable after 30 attempts. Exiting.");
    Environment.Exit(1);
}

// Apply migrations
using var db = CreateDbContext(connectionString);
Console.WriteLine("[Seeder] Applying EF migrations...");
await db.Database.MigrateAsync();
Console.WriteLine("[Seeder] Migrations applied.");

// Create admin user
var adminEmail = Environment.GetEnvironmentVariable("DEFAULT_ADMIN_EMAIL") ?? "admin@noirfilmhub.local";
var adminPassword = Environment.GetEnvironmentVariable("DEFAULT_ADMIN_PASSWORD") ?? "Admin123!";
var adminExists = await db.Utenti.AnyAsync(u => u.Email == adminEmail);
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
    Console.WriteLine($"[Seeder] Admin user '{adminEmail}' created.");
}
else
{
    Console.WriteLine("[Seeder] Admin user already exists.");
}

// Import 3 films from TMDB
var tmdbToken = Environment.GetEnvironmentVariable("TMDB_API_READ_TOKEN");
if (!string.IsNullOrWhiteSpace(tmdbToken))
{
    var services = new ServiceCollection();
    services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
    services.AddHttpClient<TmdbService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    });
    services.AddDbContext<FilmDbContext>(o => o.UseMySql(connectionString, ServerVersion.Parse("10.11.0-mariadb")));
    var sp = services.BuildServiceProvider();
    var tmdbService = sp.GetRequiredService<TmdbService>();

    var tmdbMovies = new[] { 27205, 157336, 155 };
    Console.WriteLine($"[Seeder] Importing {tmdbMovies.Length} films from TMDB (Inception, Interstellar, The Dark Knight)...");
    var (created, skipped, failed, _) = await tmdbService.ImportMoviesAsync(tmdbMovies.ToList());
    Console.WriteLine($"[Seeder] TMDB import: {created} created, {skipped} skipped, {failed} failed.");
}
else
{
    Console.WriteLine("[Seeder] TMDB_API_READ_TOKEN not set — skipping film import.");
}

// Fix null normalized emails
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

// Seed directors if needed
if (!await db.Registi.AnyAsync())
{
    db.Registi.AddRange(
        new Regista { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "UK" },
        new Regista { Nome = "Denis", Cognome = "Villeneuve", Nazionalita = "CA" },
        new Regista { Nome = "Greta", Cognome = "Gerwig", Nazionalita = "US" }
    );
    await db.SaveChangesAsync();
    Console.WriteLine("[Seeder] Directors seeded.");
}

// Seed cinemas and halls
if (!await db.Cinemas.AnyAsync())
{
    db.Cinemas.AddRange(
        new Cinema { Nome = "Noir Cinema Milano", Citta = "Milano", Indirizzo = "Via Torino 10", Capienza = 260, CodiceLocale = "0131220507688", Latitudine = 45.4642, Longitudine = 9.1900, Attivo = true },
        new Cinema { Nome = "Noir Cinema Roma", Citta = "Roma", Indirizzo = "Via Nazionale 50", Capienza = 200, CodiceLocale = "0131220507689", Latitudine = 41.9028, Longitudine = 12.4964, Attivo = true }
    );
    await db.SaveChangesAsync();
    Console.WriteLine("[Seeder] Cinemas seeded.");
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
    Console.WriteLine("[Seeder] Halls seeded.");
}

// Fix any halls with empty seat maps
var hallsWithEmptyMap = await db.Sale.Where(s => string.IsNullOrWhiteSpace(s.MappaPostiJson)).ToListAsync();
foreach (var sala in hallsWithEmptyMap)
{
    sala.MappaPostiJson = BuildSeatMapJson(sala.NumeroFile, sala.PostiPerFila, 2);
}
if (hallsWithEmptyMap.Count > 0) await db.SaveChangesAsync();

// Generate projections for 14 days
if (!await db.Proiezioni.AnyAsync())
{
    var films = await db.Films.AsNoTracking().ToListAsync();
    var sale = await db.Sale.AsNoTracking().ToListAsync();
    if (films.Count > 0 && sale.Count > 0)
    {
        var today = DateTime.Today;
        var random = new Random(42);
        var timeSlots = new[] { 13, 15, 16, 18, 20, 21, 22 };
        var totalDays = 14;

        foreach (var s in sale)
        {
            for (var d = 0; d < totalDays; d++)
            {
                var date = today.AddDays(d);
                var shift = s.Id + d;
                foreach (var startHour in timeSlots)
                {
                    var filmIndex = (shift + startHour) % films.Count;
                    var film = films[filmIndex];
                    var minute = random.Next(0, 4) * 15;
                    var prezzo = s.Tipologia switch
                    {
                        "ISENSE" => 12.90m,
                        "XL" => 11.90m,
                        "3D" => 10.90m,
                        _ => 8.90m
                    };

                    db.Proiezioni.Add(new Proiezione
                    {
                        CinemaId = s.CinemaId,
                        SalaId = s.Id,
                        FilmId = film.Id,
                        Data = date,
                        Ora = new DateTime(date.Year, date.Month, date.Day, startHour, minute, 0),
                        PrezzoBase = prezzo
                    });
                }
            }
        }
        await db.SaveChangesAsync();
        Console.WriteLine("[Seeder] Projections seeded.");
    }
    else
    {
        Console.WriteLine("[Seeder] Skipping projections — no films or halls available.");
    }
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

if (!await db.Prodotti.AnyAsync(p => p.Sku == "NFH-FLP-GR"))
{
    db.Prodotti.Add(new Product { Sku = "NFH-FLP-GR", Nome = "Felpa Logo Noir", Descrizione = "Felpa con stampa logo Noir Film Hub frontale. Disponibile in grigio melange.", Categoria = "Abbigliamento", PrezzoBase = 39.99m });
}

if (!await db.Prodotti.AnyAsync())
{
    db.Prodotti.AddRange(
        new Product { Sku = "NFH-POP-L", Nome = "Ciotola Popcorn Grande", Descrizione = "Ciotola riutilizzabile per popcorn con logo Noir Film Hub. Capacita 2L.", Categoria = "Food", PrezzoBase = 8.90m },
        new Product { Sku = "NFH-POP-S", Nome = "Ciotola Popcorn Piccola", Descrizione = "Ciotola compatta per popcorn, perfetta per i bambini.", Categoria = "Food", PrezzoBase = 5.90m },
        new Product { Sku = "NFH-BOR-500", Nome = "Boraccia Noir 500ml", Descrizione = "Boraccia termica in acciaio con logo Noir Film Hub. 500ml.", Categoria = "Accessori", PrezzoBase = 14.90m },
        new Product { Sku = "NFH-TSH-M", Nome = "T-Shirt Noir Film Hub", Descrizione = "T-shirt nera in cotone organico con stampa logo frontale.", Categoria = "Abbigliamento", PrezzoBase = 19.90m },
        new Product { Sku = "NFH-HOD-BK", Nome = "Felpa con Cappuccio Noir", Descrizione = "Felpa nera con cappuccio e stampa logo. Cotone misto 80%.", Categoria = "Abbigliamento", PrezzoBase = 39.90m },
        new Product { Sku = "NFH-PIN-SET", Nome = "Set Spille da Collezione", Descrizione = "Set di 3 spille smaltate con icone cinema. Collezione limitata.", Categoria = "Gadget", PrezzoBase = 12.90m },
        new Product { Sku = "NFH-TOTE-BK", Nome = "Tote Bag Noir Film Hub", Descrizione = "Borsa in tela con stampa Noir Film Hub. 40x35cm.", Categoria = "Accessori", PrezzoBase = 7.90m }
    );
}

if (!await db.Coupons.AnyAsync(c => c.Codice == "NFH-BENVENUTO"))
{
    var today = DateTime.Today;
    db.Coupons.AddRange(
        new Coupon { Codice = "NFH-BENVENUTO", TipoSconto = "Percentuale", ValoreSconto = 10m, TipoTarget = "Carrello", MinImportoCarrello = 15m, ValidoDal = today.AddDays(-30), ValidoAl = today.AddYears(1), MaxUtilizzi = 500, MaxPerUtente = 1, Stackable = false, Attivo = true },
        new Coupon { Codice = "NFH-LISSN20", TipoSconto = "Percentuale", ValoreSconto = 20m, TipoTarget = "Cinema", TargetId = 2, MinImportoCarrello = 20m, ValidoDal = today.AddDays(-7), ValidoAl = today, MaxUtilizzi = 100, MaxPerUtente = 1, Stackable = false, Attivo = true },
        new Coupon { Codice = "NFH-FLASH5", TipoSconto = "Fisso", ValoreSconto = 5m, TipoTarget = "Carrello", MinImportoCarrello = 10m, ValidoDal = today, ValidoAl = today.AddDays(7), MaxUtilizzi = 200, MaxPerUtente = 1, Stackable = false, Attivo = true },
        new Coupon { Codice = "NFH-VIP15", TipoSconto = "Percentuale", ValoreSconto = 15m, TipoTarget = "Carrello", MinImportoCarrello = 30m, ValidoDal = today, ValidoAl = today.AddMonths(3), MaxUtilizzi = 300, MaxPerUtente = 2, Stackable = false, Attivo = true }
    );
}

await db.SaveChangesAsync();

if (!await db.ProductVariants.AnyAsync(v => v.Sku == "NFH-TSH-M-S"))
{
    var clothing = await db.Prodotti.Where(p => p.Categoria == "Abbigliamento").ToListAsync();
    var sizes = new[] { "S", "M", "L", "XL" };
    foreach (var p in clothing)
    {
        foreach (var size in sizes)
        {
            db.ProductVariants.Add(new ProductVariant
            {
                ProductId = p.Id,
                Nome = size,
                Sku = $"{p.Sku}-{size}",
                PrezzoExtra = 0,
                Stock = 50,
                Attivo = true
            });
        }
    }
}

await db.SaveChangesAsync();

Console.WriteLine("[Seeder] Shop data seeded.");
Console.WriteLine("[Seeder] Done.");

// --- Helper methods ---

static FilmDbContext CreateDbContext(string connectionString)
{
    var options = new DbContextOptionsBuilder<FilmDbContext>()
        .UseMySql(connectionString, ServerVersion.Parse("10.11.0-mariadb"))
        .Options;
    return new FilmDbContext(options);
}

static string BuildConnectionString()
{
    var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "mariadb";
    var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "3306";
    var name = Environment.GetEnvironmentVariable("DB_NAME") ?? "film-api-db";
    var user = Environment.GetEnvironmentVariable("DB_USER") ?? "root";
    var pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "root";
    return $"Server={host};Port={port};Database={name};User Id={user};Password={pass};";
}

static string BuildSeatMapJson(int rows, int cols, int aisleWidth)
{
    var safeRows = Math.Clamp(rows, 1, 26);
    var safeCols = Math.Clamp(cols, 4, 50);
    var safeAisle = Math.Clamp(aisleWidth, 0, 4);
    var centerStart = safeAisle > 0 ? Math.Max(0, safeCols / 2 - safeAisle / 2) : -1;
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
    return System.Text.Json.JsonSerializer.Serialize(seats);
}
