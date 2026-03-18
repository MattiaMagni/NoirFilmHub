using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FilmAPI.Data;
using FilmAPI.Endpoints;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.InMemory;

// load .env
Env.Load();

var builder = WebApplication.CreateBuilder(args);

var dbUseAutoDetect = (Environment.GetEnvironmentVariable("DB_USE_AUTODETECT") ?? "true")
    .Equals("true", StringComparison.OrdinalIgnoreCase);
var dbServerVersion = Environment.GetEnvironmentVariable("DB_SERVER_VERSION") ?? "10.11.0-mariadb";
var dbProvider = Environment.GetEnvironmentVariable("DB_PROVIDER") ?? "MySql";

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        // log and continue - in some MariaDB versions provider may fail to acquire lock
        logger.LogError(ex, "Automatic database migration failed; the application will continue. If the database is not initialized, run 'dotnet ef database update' or apply migrations manually.");
    }
}

app.MapGet("/", () => Results.Ok("FilmAPI running"));

app.MapGroup("/registi").MapRegisti();
app.MapGroup("/films").MapFilms();
app.MapGroup("/cinemas").MapCinemas();
app.MapGroup("/proiezioni").MapProiezioni();

app.Run();

public partial class Program;
