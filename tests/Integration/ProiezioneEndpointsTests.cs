using System.Net;
using System.Net.Http.Json;
using FilmAPI.DTOs;
using FilmAPI.Model;
using FluentAssertions;
using Xunit;

namespace FilmAPI.Tests.Integration;

public class ProiezioneEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ProiezioneEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task P1_GetProiezioni_EmptyList_ReturnsOk()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/proiezioni");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<List<Proiezione>>();
        content.Should().BeEmpty();
    }

    [Fact]
    public async Task P2_PostProiezione_WithValidData_ReturnsCreated()
    {
        await _factory.ResetDatabaseAsync(async db =>
        {
            db.Registi.Add(new Regista { Id = 1, Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
            db.Films.Add(new Film { Id = 1, Titolo = "Inception", DataProduzione = new DateTime(2010, 7, 16), RegistaId = 1, Durata = 148 });
            db.Cinemas.Add(new Cinema { Id = 1, Nome = "Cinema Odeon", Indirizzo = "Via Roma 10", Citta = "Milano" });
        });
        var client = _factory.CreateClient();

        var request = new ProiezioneCreateDTO 
        { 
            CinemaId = 1, 
            FilmId = 1, 
            Data = new DateTime(2024, 12, 25), 
            Ora = new DateTime(1, 1, 1, 20, 0, 0) 
        };
        var response = await client.PostAsJsonAsync("/proiezioni", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<Proiezione>();
        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task P3_PostProiezione_WithInvalidCinemaId_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync(async db =>
        {
            db.Registi.Add(new Regista { Id = 1, Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
            db.Films.Add(new Film { Id = 1, Titolo = "Inception", DataProduzione = new DateTime(2010, 7, 16), RegistaId = 1, Durata = 148 });
        });
        var client = _factory.CreateClient();

        var request = new ProiezioneCreateDTO 
        { 
            CinemaId = 999, 
            FilmId = 1, 
            Data = new DateTime(2024, 12, 25), 
            Ora = new DateTime(1, 1, 1, 20, 0, 0) 
        };
        var response = await client.PostAsJsonAsync("/proiezioni", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task P4_PostProiezione_WithInvalidFilmId_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync(async db =>
        {
            db.Cinemas.Add(new Cinema { Id = 1, Nome = "Cinema Odeon", Indirizzo = "Via Roma 10", Citta = "Milano" });
        });
        var client = _factory.CreateClient();

        var request = new ProiezioneCreateDTO 
        { 
            CinemaId = 1, 
            FilmId = 999, 
            Data = new DateTime(2024, 12, 25), 
            Ora = new DateTime(1, 1, 1, 20, 0, 0) 
        };
        var response = await client.PostAsJsonAsync("/proiezioni", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task P6_GetProiezioneById_Existing_ReturnsOk()
    {
        await _factory.ResetDatabaseAsync(async db =>
        {
            db.Registi.Add(new Regista { Id = 1, Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
            db.Films.Add(new Film { Id = 1, Titolo = "Inception", DataProduzione = new DateTime(2010, 7, 16), RegistaId = 1, Durata = 148 });
            db.Cinemas.Add(new Cinema { Id = 1, Nome = "Cinema Odeon", Indirizzo = "Via Roma 10", Citta = "Milano" });
            db.Proiezioni.Add(new Proiezione { Id = 1, CinemaId = 1, FilmId = 1, Data = new DateTime(2024, 12, 25), Ora = new DateTime(1, 1, 1, 20, 0, 0) });
        });
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/proiezioni/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Proiezione>();
        result!.FilmId.Should().Be(1);
    }

    [Fact]
    public async Task P7_PutProiezione_Existing_ReturnsNoContent()
    {
        await _factory.ResetDatabaseAsync(async db =>
        {
            db.Registi.Add(new Regista { Id = 1, Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
            db.Films.Add(new Film { Id = 1, Titolo = "Inception", DataProduzione = new DateTime(2010, 7, 16), RegistaId = 1, Durata = 148 });
            db.Cinemas.Add(new Cinema { Id = 1, Nome = "Cinema Odeon", Indirizzo = "Via Roma 10", Citta = "Milano" });
            db.Proiezioni.Add(new Proiezione { Id = 1, CinemaId = 1, FilmId = 1, Data = new DateTime(2024, 12, 25), Ora = new DateTime(1, 1, 1, 20, 0, 0) });
        });
        var client = _factory.CreateClient();

        var request = new ProiezioneCreateDTO 
        { 
            CinemaId = 1, 
            FilmId = 1, 
            Data = new DateTime(2024, 12, 26), 
            Ora = new DateTime(1, 1, 1, 21, 0, 0) 
        };
        var response = await client.PutAsJsonAsync("/proiezioni/1", request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task P8_DeleteProiezione_Existing_ReturnsNoContent()
    {
        await _factory.ResetDatabaseAsync(async db =>
        {
            db.Registi.Add(new Regista { Id = 1, Nome = "Test", Cognome = "Director", Nazionalita = "IT" });
            db.Films.Add(new Film { Id = 1, Titolo = "Test Film", DataProduzione = new DateTime(2020, 1, 1), RegistaId = 1, Durata = 120 });
            db.Cinemas.Add(new Cinema { Id = 1, Nome = "Test Cinema", Indirizzo = "Via Test 1", Citta = "Test City" });
            db.Proiezioni.Add(new Proiezione { Id = 1, CinemaId = 1, FilmId = 1, Data = new DateTime(2024, 12, 25), Ora = new DateTime(1, 1, 1, 20, 0, 0) });
        });
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/proiezioni/1");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        var getResponse = await client.GetAsync("/proiezioni/1");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
