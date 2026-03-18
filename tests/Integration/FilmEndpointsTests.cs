using System.Net;
using System.Net.Http.Json;
using FilmAPI.DTOs;
using FilmAPI.Model;
using FluentAssertions;
using Xunit;

namespace FilmAPI.Tests.Integration;

public class FilmEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public FilmEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task F1_GetFilms_EmptyList_ReturnsOk()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/films");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<List<Film>>();
        content.Should().BeEmpty();
    }

    [Fact]
    public async Task F2_PostFilm_WithValidData_ReturnsCreated()
    {
        await _factory.ResetDatabaseAsync(async db =>
        {
            db.Registi.Add(new Regista { Id = 1, Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        });
        var client = _factory.CreateClient();

        var request = new FilmDTO 
        { 
            Titolo = "Inception", 
            DataProduzione = new DateTime(2010, 7, 16), 
            RegistaId = 1, 
            Durata = 148,
            CopertinaPath = "/media/inception.jpg",
            FilmatoPath = "/media/inception.mp4"
        };
        var response = await client.PostAsJsonAsync("/films", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<Film>();
        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(0);
        result.Titolo.Should().Be("Inception");
    }

    [Fact]
    public async Task F3_PostFilm_WithDefaultCover_ReturnsCreated()
    {
        await _factory.ResetDatabaseAsync(async db =>
        {
            db.Registi.Add(new Regista { Id = 1, Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        });
        var client = _factory.CreateClient();

        var request = new FilmDTO 
        { 
            Titolo = "Interstellar", 
            DataProduzione = new DateTime(2014, 11, 7), 
            RegistaId = 1, 
            Durata = 169
        };
        var response = await client.PostAsJsonAsync("/films", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<Film>();
        result.Should().NotBeNull();
        result!.CopertinaPath.Should().Be("/media/defaults/cover-default.jpg");
    }

    [Fact]
    public async Task F4_PostFilm_WithInvalidRegistaId_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var request = new FilmDTO 
        { 
            Titolo = "Inception", 
            DataProduzione = new DateTime(2010, 7, 16), 
            RegistaId = 999, 
            Durata = 148
        };
        var response = await client.PostAsJsonAsync("/films", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task F5_GetFilmById_Existing_ReturnsOk()
    {
        await _factory.ResetDatabaseAsync(async db =>
        {
            db.Registi.Add(new Regista { Id = 1, Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
            db.Films.Add(new Film { Id = 1, Titolo = "Inception", DataProduzione = new DateTime(2010, 7, 16), RegistaId = 1, Durata = 148 });
        });
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/films/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Film>();
        result!.Titolo.Should().Be("Inception");
    }

    [Fact]
    public async Task F6_PutFilm_Existing_ReturnsNoContent()
    {
        await _factory.ResetDatabaseAsync(async db =>
        {
            db.Registi.Add(new Regista { Id = 1, Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
            db.Films.Add(new Film { Id = 1, Titolo = "Inception", DataProduzione = new DateTime(2010, 7, 16), RegistaId = 1, Durata = 148 });
        });
        var client = _factory.CreateClient();

        var request = new FilmDTO 
        { 
            Titolo = "Inception Updated", 
            DataProduzione = new DateTime(2010, 7, 16), 
            RegistaId = 1, 
            Durata = 148
        };
        var response = await client.PutAsJsonAsync("/films/1", request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        var getResponse = await client.GetAsync("/films/1");
        var result = await getResponse.Content.ReadFromJsonAsync<Film>();
        result!.Titolo.Should().Be("Inception Updated");
    }

    [Fact]
    public async Task F7_PutFilm_WithInvalidRegistaId_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync(async db =>
        {
            db.Registi.Add(new Regista { Id = 1, Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
            db.Films.Add(new Film { Id = 1, Titolo = "Inception", DataProduzione = new DateTime(2010, 7, 16), RegistaId = 1, Durata = 148 });
        });
        var client = _factory.CreateClient();

        var request = new FilmDTO 
        { 
            Titolo = "Inception", 
            DataProduzione = new DateTime(2010, 7, 16), 
            RegistaId = 999, 
            Durata = 148
        };
        var response = await client.PutAsJsonAsync("/films/1", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task F8_DeleteFilm_Existing_ReturnsNoContent()
    {
        await _factory.ResetDatabaseAsync(async db =>
        {
            db.Registi.Add(new Regista { Id = 1, Nome = "Test", Cognome = "Director", Nazionalita = "IT" });
            db.Films.Add(new Film { Id = 1, Titolo = "Test Film", DataProduzione = new DateTime(2020, 1, 1), RegistaId = 1, Durata = 120 });
        });
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/films/1");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        var getResponse = await client.GetAsync("/films/1");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
