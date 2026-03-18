using System.Net;
using System.Net.Http.Json;
using FilmAPI.DTOs;
using FilmAPI.Model;
using FluentAssertions;
using Xunit;

namespace FilmAPI.Tests.Integration;

public class RegistiEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RegistiEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task R1_GetRegisti_EmptyList_ReturnsOk()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/registi");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<List<Regista>>();
        content.Should().BeEmpty();
    }

    [Fact]
    public async Task R2_PostRegisti_WithValidData_ReturnsCreated()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var request = new RegistaDTO { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" };
        var response = await client.PostAsJsonAsync("/registi", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<Regista>();
        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(0);
        result.Nome.Should().Be("Christopher");
        result.Cognome.Should().Be("Nolan");
    }

    [Fact]
    public async Task R3_GetRegistaById_Existing_ReturnsOk()
    {
        await _factory.ResetDatabaseAsync(async db =>
        {
            db.Registi.Add(new Regista { Id = 1, Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        });
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/registi/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Regista>();
        result!.Nome.Should().Be("Christopher");
    }

    [Fact]
    public async Task R4_GetRegistaById_NonExisting_ReturnsNotFound()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/registi/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task R5_PutRegista_Existing_ReturnsNoContent()
    {
        await _factory.ResetDatabaseAsync(async db =>
        {
            db.Registi.Add(new Regista { Id = 1, Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Britannica" });
        });
        var client = _factory.CreateClient();

        var request = new RegistaDTO { Nome = "Christopher", Cognome = "Nolan", Nazionalita = "Statunitense" };
        var response = await client.PutAsJsonAsync("/registi/1", request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        var getResponse = await client.GetAsync("/registi/1");
        var result = await getResponse.Content.ReadFromJsonAsync<Regista>();
        result!.Nazionalita.Should().Be("Statunitense");
    }

    [Fact]
    public async Task R6_PutRegista_NonExisting_ReturnsNotFound()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var request = new RegistaDTO { Nome = "Test", Cognome = "User", Nazionalita = "IT" };
        var response = await client.PutAsJsonAsync("/registi/99999", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task R7_DeleteRegista_Existing_ReturnsNoContent()
    {
        await _factory.ResetDatabaseAsync(async db =>
        {
            db.Registi.Add(new Regista { Id = 1, Nome = "Test", Cognome = "Director", Nazionalita = "IT" });
        });
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/registi/1");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        var getResponse = await client.GetAsync("/registi/1");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task R8_DeleteRegista_NonExisting_ReturnsNotFound()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/registi/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task R9_PostRegista_WithMissingData_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var request = new { Nome = "Christopher" };
        var response = await client.PostAsJsonAsync("/registi", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
