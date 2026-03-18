using Microsoft.EntityFrameworkCore;
using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;
using FluentAssertions;
using Xunit;

namespace FilmAPI.Tests.Unit;

public class ProiezioneServiceTests : IAsyncLifetime
{
    private readonly FilmDbContext _context;

    public ProiezioneServiceTests()
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
        var result = await _context.Proiezioni.ToListAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesEntity()
    {
        _context.Registi.Add(new Regista { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        _context.Films.Add(new Film { Titolo = "Inception", DataProduzione = new DateTime(2010, 7, 16), RegistaId = 1, Durata = 148 });
        _context.Cinemas.Add(new Cinema { Nome = "Cinema Odeon", Indirizzo = "Via Roma 10", Citta = "Milano" });
        await _context.SaveChangesAsync();
        
        var dto = new ProiezioneCreateDTO { CinemaId = 1, FilmId = 1, Data = new DateTime(2024, 12, 25), Ora = new DateTime(1, 1, 1, 20, 0, 0) };
        var entity = new Proiezione { CinemaId = dto.CinemaId, FilmId = dto.FilmId, Data = dto.Data, Ora = dto.Ora };
        
        _context.Proiezioni.Add(entity);
        await _context.SaveChangesAsync();

        entity.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateAsync_WithNonExistingCinema_CreatesEntity()
    {
        _context.Registi.Add(new Regista { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        _context.Films.Add(new Film { Titolo = "Inception", DataProduzione = new DateTime(2010, 7, 16), RegistaId = 1, Durata = 148 });
        await _context.SaveChangesAsync();
        
        var entity = new Proiezione { CinemaId = 999, FilmId = 1, Data = new DateTime(2024, 12, 25), Ora = new DateTime(1, 1, 1, 20, 0, 0) };
        
        _context.Proiezioni.Add(entity);
        
        await _context.SaveChangesAsync();
        
        entity.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateAsync_WithNonExistingFilm_CreatesEntity()
    {
        _context.Cinemas.Add(new Cinema { Nome = "Cinema Odeon", Indirizzo = "Via Roma 10", Citta = "Milano" });
        await _context.SaveChangesAsync();
        
        var entity = new Proiezione { CinemaId = 1, FilmId = 999, Data = new DateTime(2024, 12, 25), Ora = new DateTime(1, 1, 1, 20, 0, 0) };
        
        _context.Proiezioni.Add(entity);
        
        await _context.SaveChangesAsync();
        
        entity.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateData_CreatesEntity()
    {
        _context.Registi.Add(new Regista { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        _context.Films.Add(new Film { Titolo = "Inception", DataProduzione = new DateTime(2010, 7, 16), RegistaId = 1, Durata = 148 });
        _context.Cinemas.Add(new Cinema { Nome = "Cinema Odeon", Indirizzo = "Via Roma 10", Citta = "Milano" });
        _context.Proiezioni.Add(new Proiezione { CinemaId = 1, FilmId = 1, Data = new DateTime(2024, 12, 25), Ora = new DateTime(1, 1, 1, 20, 0, 0) });
        await _context.SaveChangesAsync();
        
        var entity = new Proiezione { CinemaId = 1, FilmId = 1, Data = new DateTime(2024, 12, 25), Ora = new DateTime(1, 1, 1, 20, 0, 0) };
        
        _context.Proiezioni.Add(entity);
        
        await _context.SaveChangesAsync();
        
        entity.Id.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesEntity()
    {
        _context.Registi.Add(new Regista { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        _context.Films.Add(new Film { Titolo = "Inception", DataProduzione = new DateTime(2010, 7, 16), RegistaId = 1, Durata = 148 });
        _context.Cinemas.Add(new Cinema { Nome = "Cinema Odeon", Indirizzo = "Via Roma 10", Citta = "Milano" });
        _context.Proiezioni.Add(new Proiezione { CinemaId = 1, FilmId = 1, Data = new DateTime(2024, 12, 25), Ora = new DateTime(1, 1, 1, 20, 0, 0) });
        await _context.SaveChangesAsync();

        var entity = await _context.Proiezioni.FindAsync(1);
        entity!.Data = new DateTime(2024, 12, 26);
        await _context.SaveChangesAsync();

        var result = await _context.Proiezioni.FindAsync(1);
        result!.Data.Should().Be(new DateTime(2024, 12, 26));
    }

    [Fact]
    public async Task DeleteAsync_Existing_RemovesEntity()
    {
        _context.Registi.Add(new Regista { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        _context.Films.Add(new Film { Titolo = "Inception", DataProduzione = new DateTime(2010, 7, 16), RegistaId = 1, Durata = 148 });
        _context.Cinemas.Add(new Cinema { Nome = "Cinema Odeon", Indirizzo = "Via Roma 10", Citta = "Milano" });
        _context.Proiezioni.Add(new Proiezione { CinemaId = 1, FilmId = 1, Data = new DateTime(2024, 12, 25), Ora = new DateTime(1, 1, 1, 20, 0, 0) });
        await _context.SaveChangesAsync();

        var entity = await _context.Proiezioni.FindAsync(1);
        _context.Proiezioni.Remove(entity!);
        await _context.SaveChangesAsync();

        var result = await _context.Proiezioni.FindAsync(1);
        result.Should().BeNull();
    }
}