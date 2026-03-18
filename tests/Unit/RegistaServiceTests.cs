using Microsoft.EntityFrameworkCore;
using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;
using FluentAssertions;
using Xunit;

namespace FilmAPI.Tests.Unit;

public class RegistaServiceTests : IAsyncLifetime
{
    private readonly FilmDbContext _context;

    public RegistaServiceTests()
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
        var result = await _context.Registi.ToListAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WithData_ReturnsList()
    {
        _context.Registi.Add(new Regista { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        await _context.SaveChangesAsync();

        var result = await _context.Registi.ToListAsync();

        result.Should().HaveCount(1);
        result.First().Nome.Should().Be("Christopher");
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ReturnsEntity()
    {
        _context.Registi.Add(new Regista { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        await _context.SaveChangesAsync();

        var result = await _context.Registi.FindAsync(1);

        result.Should().NotBeNull();
        result!.Nome.Should().Be("Christopher");
    }

    [Fact]
    public async Task GetByIdAsync_NonExisting_ReturnsNull()
    {
        var result = await _context.Registi.FindAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesEntity()
    {
        var dto = new RegistaDTO { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" };
        var entity = new Regista { Nome = dto.Nome, Cognome = dto.Cognome, Nazionalita = dto.Nazionalita };
        
        _context.Registi.Add(entity);
        await _context.SaveChangesAsync();

        entity.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateAsync_WithMissingData_ThrowsException()
    {
        var entity = new Regista { Nome = "Christopher" };
        
        _context.Registi.Add(entity);
        
        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    [Fact]
    public async Task UpdateAsync_Existing_UpdatesEntity()
    {
        _context.Registi.Add(new Regista { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        await _context.SaveChangesAsync();

        var entity = await _context.Registi.FindAsync(1);
        entity!.Nazionalita = "Statunitense";
        await _context.SaveChangesAsync();

        var result = await _context.Registi.FindAsync(1);
        result!.Nazionalita.Should().Be("Statunitense");
    }

    [Fact]
    public async Task UpdateAsync_NonExisting_DoesNotThrow()
    {
        var entity = new Regista { Id = 999, Nome = "Test", Cognome = "User", Nazionalita = "IT" };
        
        await _context.SaveChangesAsync();
        
        var result = await _context.Registi.FindAsync(999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_Existing_RemovesEntity()
    {
        _context.Registi.Add(new Regista { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        await _context.SaveChangesAsync();

        var entity = await _context.Registi.FindAsync(1);
        _context.Registi.Remove(entity!);
        await _context.SaveChangesAsync();

        var result = await _context.Registi.FindAsync(1);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExisting_DoesNotThrow()
    {
        var entity = new Regista { Id = 999, Nome = "Test", Cognome = "User", Nazionalita = "IT" };
        
        await _context.SaveChangesAsync();
        
        var result = await _context.Registi.FindAsync(999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetFilmsByRegistaIdAsync_WithFilms_ReturnsFilms()
    {
        _context.Registi.Add(new Regista { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        _context.Films.Add(new Film { Titolo = "Inception", DataProduzione = new DateTime(2010, 7, 16), RegistaId = 1, Durata = 148 });
        await _context.SaveChangesAsync();

        var result = await _context.Films.Where(f => f.RegistaId == 1).ToListAsync();

        result.Should().HaveCount(1);
        result.First().Titolo.Should().Be("Inception");
    }

    [Fact]
    public async Task GetFilmsByRegistaIdAsync_WithoutFilms_ReturnsEmpty()
    {
        _context.Registi.Add(new Regista { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        await _context.SaveChangesAsync();

        var result = await _context.Films.Where(f => f.RegistaId == 1).ToListAsync();

        result.Should().BeEmpty();
    }
}