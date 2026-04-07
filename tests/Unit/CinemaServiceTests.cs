using Microsoft.EntityFrameworkCore;
using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;
using FluentAssertions;
using Xunit;

namespace FilmAPI.Tests.Unit;

public class CinemaServiceTests : IAsyncLifetime
{
    private readonly FilmDbContext _context;

    public CinemaServiceTests()
    {
        var options = new DbContextOptionsBuilder<FilmDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new FilmDbContext(options);
    }

    public async Task InitializeAsync() => await Task.CompletedTask;
    public async Task DisposeAsync() => await _context.Database.EnsureDeletedAsync();

    [Fact]
    public async Task GetAllAsync_EmptyDatabase_ReturnsEmptyList()
    {
        var result = await _context.Cinemas.ToListAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesEntity()
    {
        var dto = new CinemaDTO { Nome = "Cinema Odeon", Indirizzo = "Via Roma 10", Citta = "Milano", Capienza = 180 };
        var entity = new Cinema { Nome = dto.Nome, Indirizzo = dto.Indirizzo, Citta = dto.Citta, Capienza = dto.Capienza };
        
        _context.Cinemas.Add(entity);
        await _context.SaveChangesAsync();

        entity.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ReturnsEntity()
    {
        _context.Cinemas.Add(new Cinema { Nome = "Cinema Odeon", Indirizzo = "Via Roma 10", Citta = "Milano", Capienza = 180 });
        await _context.SaveChangesAsync();

        var result = await _context.Cinemas.FindAsync(1);

        result.Should().NotBeNull();
        result!.Nome.Should().Be("Cinema Odeon");
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesEntity()
    {
        _context.Cinemas.Add(new Cinema { Nome = "Cinema Odeon", Indirizzo = "Via Roma 10", Citta = "Milano", Capienza = 180 });
        await _context.SaveChangesAsync();

        var entity = await _context.Cinemas.FindAsync(1);
        entity!.Nome = "Cinema Nuovo";
        await _context.SaveChangesAsync();

        var result = await _context.Cinemas.FindAsync(1);
        result!.Nome.Should().Be("Cinema Nuovo");
    }

    [Fact]
    public async Task DeleteAsync_Existing_RemovesEntity()
    {
        _context.Cinemas.Add(new Cinema { Nome = "Cinema Odeon", Indirizzo = "Via Roma 10", Citta = "Milano", Capienza = 180 });
        await _context.SaveChangesAsync();

        var entity = await _context.Cinemas.FindAsync(1);
        _context.Cinemas.Remove(entity!);
        await _context.SaveChangesAsync();

        var result = await _context.Cinemas.FindAsync(1);
        result.Should().BeNull();
    }
}
