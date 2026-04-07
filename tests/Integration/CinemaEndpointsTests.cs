using System.Net;
using System.Net.Http.Json;
using FilmAPI.DTOs;
using FilmAPI.Model;
using FluentAssertions;
using Xunit;

namespace FilmAPI.Tests.Integration;

public class CinemaEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CinemaEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task C1_GetCinemas_EmptyList_ReturnsOk()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/cinemas");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<List<Cinema>>();
        content.Should().BeEmpty();
    }

    [Fact]
    public async Task C2_PostCinema_WithValidData_ReturnsCreated()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var request = new CinemaDTO { Nome = "Cinema Odeon", Indirizzo = "Via Roma 10", Citta = "Milano", Capienza = 180 };
        var response = await client.PostAsJsonAsync("/cinemas", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<Cinema>();
        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(0);
        result.Nome.Should().Be("Cinema Odeon");
    }

    [Fact]
    public async Task C3_GetCinemaById_Existing_ReturnsOk()
    {
        await _factory.ResetDatabaseAsync(async db =>
        {
            db.Cinemas.Add(new Cinema { Id = 1, Nome = "Cinema Odeon", Indirizzo = "Via Roma 10", Citta = "Milano", Capienza = 180 });
        });
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/cinemas/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Cinema>();
        result!.Nome.Should().Be("Cinema Odeon");
    }

    [Fact]
    public async Task C4_PutCinema_Existing_ReturnsNoContent()
    {
        await _factory.ResetDatabaseAsync(async db =>
        {
            db.Cinemas.Add(new Cinema { Id = 1, Nome = "Cinema Odeon", Indirizzo = "Via Roma 10", Citta = "Milano" });
        });
        var client = _factory.CreateClient();

        var request = new CinemaDTO { Nome = "Cinema Nuovo", Indirizzo = "Via Milano 5", Citta = "Roma", Capienza = 190 };
        var response = await client.PutAsJsonAsync("/cinemas/1", request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        var getResponse = await client.GetAsync("/cinemas/1");
        var result = await getResponse.Content.ReadFromJsonAsync<Cinema>();
        result!.Nome.Should().Be("Cinema Nuovo");
    }

    [Fact]
    public async Task C5_DeleteCinema_Existing_ReturnsNoContent()
    {
        await _factory.ResetDatabaseAsync(async db =>
        {
            db.Cinemas.Add(new Cinema { Id = 1, Nome = "Cinema Test", Indirizzo = "Via Test 1", Citta = "Test City", Capienza = 150 });
        });
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/cinemas/1");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        var getResponse = await client.GetAsync("/cinemas/1");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
