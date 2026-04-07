using DotNetEnv;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FilmAPI.Data;
using FilmAPI.Endpoints;
using FilmAPI.Model;
using FilmAPI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.InMemory;

// load .env
Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFilmFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5001", "http://127.0.0.1:5001")
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
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// build connection string from env
var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "3306";
var name = Environment.GetEnvironmentVariable("DB_NAME") ?? "film-api-db";
var user = Environment.GetEnvironmentVariable("DB_USER") ?? "root";
var pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "root";
var connectionString = $"Server={host};Port={port};Database={name};User Id={user};Password={pass};";

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
    // Pomelo.EntityFrameworkCore.MySql uses UseMySql extension
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

// apply migrations automatically (best-effort)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FilmDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        db.Database.Migrate();
        var adminEmail = Environment.GetEnvironmentVariable("DEFAULT_ADMIN_EMAIL") ?? "admin@filmapi.local";
        var adminPassword = Environment.GetEnvironmentVariable("DEFAULT_ADMIN_PASSWORD") ?? "Admin123!";

        var adminExists = await db.Utenti.AnyAsync(x => x.Email == adminEmail);
        if (!adminExists)
        {
            db.Utenti.Add(new Utente
            {
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                Nome = "Admin",
                Cognome = "Sistema",
                Telefono = string.Empty,
                Ruolo = RuoloUtente.Admin
            });
            await db.SaveChangesAsync();
        }
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        // log and continue - in some MariaDB versions provider may fail to acquire lock
        logger.LogError(ex, "Automatic database migration failed; the application will continue. If the database is not initialized, run 'dotnet ef database update' or apply migrations manually.");
    }
}

app.Use(async (context, next) =>
{
    await next();

    if (context.Response.StatusCode == StatusCodes.Status401Unauthorized)
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "Non autenticato" });
    }
    else if (context.Response.StatusCode == StatusCodes.Status403Forbidden)
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "Permessi insufficienti" });
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

app.Run();

public partial class Program;
