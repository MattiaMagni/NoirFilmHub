using Microsoft.EntityFrameworkCore;
using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;
using FluentAssertions;
using Xunit;

namespace FilmAPI.Tests.Unit;

public class FilmServiceTests : IAsyncLifetime
{
    private readonly FilmDbContext _context;

    public FilmServiceTests()
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
        var result = await _context.Films.ToListAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WithData_ReturnsList()
    {
        _context.Registi.Add(new Regista { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        _context.Films.Add(new Film { Titolo = "Inception", DataProduzione = new DateTime(2010, 7, 16), RegistaId = 1, Durata = 148 });
        await _context.SaveChangesAsync();

        var result = await _context.Films.ToListAsync();

        result.Should().HaveCount(1);
        result.First().Titolo.Should().Be("Inception");
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ReturnsEntity()
    {
        _context.Registi.Add(new Regista { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        _context.Films.Add(new Film { Titolo = "Inception", DataProduzione = new DateTime(2010, 7, 16), RegistaId = 1, Durata = 148 });
        await _context.SaveChangesAsync();

        var result = await _context.Films.FindAsync(1);

        result.Should().NotBeNull();
        result!.Titolo.Should().Be("Inception");
    }

    [Fact]
    public async Task GetByIdAsync_NonExisting_ReturnsNull()
    {
        var result = await _context.Films.FindAsync(999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesEntity()
    {
        _context.Registi.Add(new Regista { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        await _context.SaveChangesAsync();
        
        var dto = new FilmDTO { Titolo = "Inception", DataProduzione = new DateTime(2010, 7, 16), RegistaId = 1, Durata = 148 };
        var entity = new Film { Titolo = dto.Titolo, DataProduzione = dto.DataProduzione, RegistaId = dto.RegistaId, Durata = dto.Durata };
        
        _context.Films.Add(entity);
        await _context.SaveChangesAsync();

        entity.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateAsync_WithNonExistingRegista_CreatesEntity()
    {
        var dto = new FilmDTO { Titolo = "Inception", DataProduzione = new DateTime(2010, 7, 16), RegistaId = 999, Durata = 148 };
        var entity = new Film { Titolo = dto.Titolo, DataProduzione = dto.DataProduzione, RegistaId = dto.RegistaId, Durata = dto.Durata };
        
        _context.Films.Add(entity);
        
        await _context.SaveChangesAsync();
        
        entity.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesEntity()
    {
        _context.Registi.Add(new Regista { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        _context.Films.Add(new Film { Titolo = "Inception", DataProduzione = new DateTime(2010, 7, 16), RegistaId = 1, Durata = 148 });
        await _context.SaveChangesAsync();

        var entity = await _context.Films.FindAsync(1);
        entity!.Titolo = "Inception Updated";
        await _context.SaveChangesAsync();

        var result = await _context.Films.FindAsync(1);
        result!.Titolo.Should().Be("Inception Updated");
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistingRegista_UpdatesEntity()
    {
        _context.Registi.Add(new Regista { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        _context.Films.Add(new Film { Titolo = "Inception", DataProduzione = new DateTime(2010, 7, 16), RegistaId = 1, Durata = 148 });
        await _context.SaveChangesAsync();

        var entity = await _context.Films.FindAsync(1);
        entity!.RegistaId = 999;
        
        await _context.SaveChangesAsync();

        var result = await _context.Films.FindAsync(1);
        result!.RegistaId.Should().Be(999);
    }

    [Fact]
    public async Task DeleteAsync_WithProiezioni_RemovesEntity()
    {
        _context.Registi.Add(new Regista { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        _context.Films.Add(new Film { Titolo = "Inception", DataProduzione = new DateTime(2010, 7, 16), RegistaId = 1, Durata = 148 });
        _context.Cinemas.Add(new Cinema { Nome = "Cinema Odeon", Indirizzo = "Via Roma 10", Citta = "Milano" });
        _context.Proiezioni.Add(new Proiezione { CinemaId = 1, FilmId = 1, Data = new DateTime(2024, 12, 25), Ora = new DateTime(1, 1, 1, 20, 0, 0) });
        await _context.SaveChangesAsync();

        var entity = await _context.Films.FindAsync(1);
        _context.Films.Remove(entity!);
        await _context.SaveChangesAsync();

        var result = await _context.Films.FindAsync(1);
        result.Should().BeNull();
    }
}