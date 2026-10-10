using System.Net;
using SkyJourney.Application.Common;
using SkyJourney.Application.FlightData;
using SkyJourney.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace SkyJourney.IntegrationTests.Flights;

public sealed class AirportTests(SkyJourneyApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Search_ranks_exact_codes_and_city_prefixes_first()
    {
        var byCode = await (await _client.GetAsync("/api/v1/airports?query=bud", Ct)).ReadAsAsync<List<AirportDto>>();
        var byCity = await (await _client.GetAsync("/api/v1/airports?query=lon", Ct)).ReadAsAsync<List<AirportDto>>();

        Assert.Equal("BUD", byCode[0].IataCode);
        Assert.Equal(["LHR", "LTN"], byCity.Take(2).Select(a => a.IataCode).Order());
        Assert.Contains(byCity, a => a.IataCode == "BCN"); // "Barce-lon-a" is a weaker match
        Assert.Equal("BCN", byCity[^1].IataCode);
    }

    [Fact]
    public async Task Search_without_query_lists_airports_up_to_the_limit()
    {
        var airports = await (await _client.GetAsync("/api/v1/airports?limit=5", Ct)).ReadAsAsync<List<AirportDto>>();

        Assert.Equal(5, airports.Count);
    }

    [Fact]
    public async Task Destinations_are_derived_from_upcoming_flights()
    {
        var destinations = await (await _client.GetAsync("/api/v1/airports/LTN/destinations", Ct)).ReadAsAsync<List<AirportDto>>();

        Assert.Equal(["BUD", "CLJ"], destinations.Select(a => a.IataCode).Order());
    }

    [Fact]
    public async Task Unknown_airport_returns_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/v1/airports/ZZZ", Ct)).StatusCode);
    }

    [Fact]
    public async Task Re_running_the_import_creates_nothing_new()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var importer = scope.ServiceProvider.GetRequiredService<FlightDataImporter>();

        var imported = await importer.ImportReferenceDataAsync(Ct);
        var generated = await importer.GenerateFlightsAsync(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 23), Ct);

        Assert.Equal(new ImportResult(0, 0, 0, 0), imported);
        Assert.Equal(0, generated);
    }
}
