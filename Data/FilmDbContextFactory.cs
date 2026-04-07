using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FilmAPI.Data;

public class FilmDbContextFactory : IDesignTimeDbContextFactory<FilmDbContext>
{
    public FilmDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FilmDbContext>();

        var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "3306";
        var name = Environment.GetEnvironmentVariable("DB_NAME") ?? "film-api-db";
        var user = Environment.GetEnvironmentVariable("DB_USER") ?? "root";
        var pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "root";
        var version = Environment.GetEnvironmentVariable("DB_SERVER_VERSION") ?? "10.11.0-mariadb";

        var connectionString = $"Server={host};Port={port};Database={name};User Id={user};Password={pass};";
        optionsBuilder.UseMySql(connectionString, ServerVersion.Parse(version));

        return new FilmDbContext(optionsBuilder.Options);
    }
}
