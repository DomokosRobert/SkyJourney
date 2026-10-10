using System.Net;
using Microsoft.EntityFrameworkCore;
using SkyJourney.Application.Common;
using SkyJourney.Application.Flights;
using SkyJourney.Domain.Flights;
using SkyJourney.IntegrationTests.Infrastructure;

namespace SkyJourney.IntegrationTests.Flights;

public sealed class FlightSearchTests(SkyJourneyApiFactory factory)
{
    private static readonly DateOnly SearchDate = new(2026, 11, 5);
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<FlightSearchResponse> SearchAsync(string query)
    {
        var response = await _client.GetAsync($"/api/v1/flights/search?{query}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsAsync<FlightSearchResponse>();
    }

    [Fact]
    public async Task One_way_search_returns_priced_demo_offers_sorted_by_price()
    {
        var result = await SearchAsync("origin=BUD&destination=lhr&departureDate=2026-11-05&adults=2&children=1");

        Assert.Equal(DataSources.Demo, result.DataSource);
        Assert.Null(result.Return);
        Assert.Equal(3, result.Outbound.Count);
        Assert.Equal(result.Outbound.OrderBy(o => o.LowestPricePerPassenger!.Amount), result.Outbound);

        Assert.All(result.Outbound, offer =>
        {
            Assert.Equal("BUD", offer.Origin.IataCode);
            Assert.Equal("LHR", offer.Destination.IataCode);
            Assert.Equal(SearchDate, DateOnly.FromDateTime(offer.DepartureLocal.DateTime));
            Assert.True(offer.IsBookable);
            Assert.Equal([FareFamily.Basic, FareFamily.Plus, FareFamily.Flex], offer.Fares.Select(f => f.Family));
            Assert.All(offer.Fares, fare => Assert.Equal(fare.PricePerPassenger.Amount * 3, fare.TotalPrice.Amount));
        });
    }

    [Fact]
    public async Task Times_are_returned_in_airport_local_time_and_utc()
    {
        var offer = (await SearchAsync("origin=BUD&destination=LHR&departureDate=2026-11-05")).Outbound[0];

        Assert.Equal(TimeSpan.FromHours(1), offer.DepartureLocal.Offset); // CET
        Assert.Equal(TimeSpan.Zero, offer.ArrivalLocal.Offset); // GMT
        Assert.Equal(offer.DepartureUtc, offer.DepartureLocal.ToUniversalTime());
        Assert.Equal(offer.DurationMinutes, (int)(offer.ArrivalUtc - offer.DepartureUtc).TotalMinutes);
    }

    [Fact]
    public async Task Return_search_returns_both_directions()
    {
        var result = await SearchAsync("origin=BUD&destination=CDG&departureDate=2026-11-05&returnDate=2026-11-09");

        Assert.NotEmpty(result.Outbound);
        Assert.NotNull(result.Return);
        Assert.NotEmpty(result.Return);
        Assert.All(result.Return, o => Assert.Equal(("CDG", "BUD"), (o.Origin.IataCode, o.Destination.IataCode)));
        Assert.All(result.Return, o => Assert.Equal(new DateOnly(2026, 11, 9), DateOnly.FromDateTime(o.DepartureLocal.DateTime)));
    }

    [Fact]
    public async Task Sorting_and_departure_window_are_applied()
    {
        var all = await SearchAsync("origin=BUD&destination=FRA&departureDate=2026-11-05&sort=departure");
        Assert.Equal(all.Outbound.OrderBy(o => o.DepartureUtc), all.Outbound);

        var afternoon = await SearchAsync("origin=BUD&destination=FRA&departureDate=2026-11-05&departAfter=12:00&departBefore=23:59");
        Assert.NotEmpty(afternoon.Outbound);
        Assert.True(afternoon.Outbound.Count < all.Outbound.Count);
        Assert.All(afternoon.Outbound, o => Assert.True(o.DepartureLocal.Hour >= 12));
    }

    [Fact]
    public async Task Flights_inside_the_sales_cutoff_are_not_offered()
    {
        var today = await SearchAsync("origin=BUD&destination=LHR&departureDate=2026-11-02");

        Assert.All(today.Outbound, o => Assert.True(o.DepartureUtc >= SkyJourneyApiFactory.StartTime + Flight.SalesCutoff));
        Assert.True(today.Outbound.Count < 3, "The early departure should be excluded by the 2-hour sales cutoff.");
    }

    [Fact]
    public async Task Flights_without_enough_seats_for_the_party_are_excluded()
    {
        var date = new DateOnly(2026, 11, 10);
        await factory.WithDbAsync(db => db.Flights
            .Where(f => f.Origin.IataCode == "BUD" && f.Destination.IataCode == "MAD" && f.DepartureLocalDate == date)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.SeatsSold, f => f.Capacity - 2), Ct));

        var forTwo = await SearchAsync("origin=BUD&destination=MAD&departureDate=2026-11-10&adults=2");
        var forThree = await SearchAsync("origin=BUD&destination=MAD&departureDate=2026-11-10&adults=3");

        Assert.Equal(2, Assert.Single(forTwo.Outbound).SeatsLeft);
        Assert.Empty(forThree.Outbound);
    }

    [Fact]
    public async Task Cancelled_flights_are_excluded()
    {
        var date = new DateOnly(2026, 11, 11);
        await factory.WithDbAsync(db => db.Flights
            .Where(f => f.Origin.IataCode == "BUD" && f.Destination.IataCode == "LIS" && f.DepartureLocalDate == date)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Status, FlightStatus.Cancelled), Ct));

        Assert.Empty((await SearchAsync("origin=BUD&destination=LIS&departureDate=2026-11-11")).Outbound);
    }

    [Fact]
    public async Task Unavailable_fares_are_hidden()
    {
        var date = new DateOnly(2026, 11, 12);
        await factory.WithDbAsync(async db =>
        {
            var flightIds = await db.Flights
                .Where(f => f.Origin.IataCode == "BUD" && f.Destination.IataCode == "OSL" && f.DepartureLocalDate == date)
                .Select(f => f.Id)
                .ToListAsync(Ct);
            await db.Fares.Where(f => flightIds.Contains(f.FlightId) && f.Family == FareFamily.Basic)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsAvailable, false), Ct);
        });

        var offer = Assert.Single((await SearchAsync("origin=BUD&destination=OSL&departureDate=2026-11-12")).Outbound);

        Assert.DoesNotContain(offer.Fares, f => f.Family == FareFamily.Basic);
        Assert.Equal(offer.Fares.Single(f => f.Family == FareFamily.Plus).PricePerPassenger, offer.LowestPricePerPassenger);
    }

    [Fact]
    public async Task Unknown_airport_returns_not_found_problem()
    {
        var response = await _client.GetAsync("/api/v1/flights/search?origin=XXX&destination=LHR&departureDate=2026-11-05", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("not_found", (await response.ReadAsAsync<ProblemResponse>()).Code);
    }

    [Theory]
    [InlineData("origin=BUD&destination=LHR&departureDate=2026-11-01", "departureDate")] // yesterday in Budapest
    [InlineData("origin=BUD&destination=LHR&departureDate=2027-12-01", "departureDate")] // beyond the booking horizon
    [InlineData("origin=BUD&destination=LHR", "departureDate")]
    [InlineData("origin=BUD&destination=LHR&departureDate=2026-11-05&adults=10", "adults")]
    [InlineData("origin=BUD&destination=LHR&departureDate=2026-11-05&sort=cheapest", "sort")]
    public async Task Invalid_searches_return_validation_problems(string query, string field)
    {
        var response = await _client.GetAsync($"/api/v1/flights/search?{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadAsAsync<ProblemResponse>();
        Assert.Equal("validation_failed", problem.Code);
        Assert.Contains(field, problem.Errors!.Keys);
    }

    [Fact]
    public async Task Flight_details_are_returned_by_id()
    {
        var offer = (await SearchAsync("origin=BUD&destination=AMS&departureDate=2026-11-06")).Outbound[0];

        var response = await _client.GetAsync($"/api/v1/flights/{offer.FlightId}?passengers=2", Ct);
        var details = await response.ReadAsAsync<FlightOfferDto>();

        Assert.Equal(offer.FlightNumber, details.FlightNumber);
        Assert.Equal(offer.LowestPricePerPassenger!.Amount * 2, details.Fares.Min(f => f.TotalPrice.Amount));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/flights/{Guid.NewGuid()}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Lowest_fares_list_only_operating_days_with_the_cheapest_fare()
    {
        var response = await _client.GetAsync("/api/v1/flights/lowest-fares?origin=AMS&destination=KEF&from=2026-11-03&to=2026-11-16", Ct);
        var calendar = await response.ReadAsAsync<LowestFaresResponse>();

        // AMS-KEF operates four days a week in the demo data set.
        Assert.Equal(8, calendar.Days.Count);
        foreach (var day in calendar.Days.Take(2))
        {
            var search = await SearchAsync($"origin=AMS&destination=KEF&departureDate={day.Date:yyyy-MM-dd}");
            Assert.Equal(search.Outbound.Min(o => o.LowestPricePerPassenger!.Amount), day.PricePerPassenger.Amount);
        }
    }

    [Fact]
    public async Task Lowest_fares_reject_ranges_longer_than_the_limit()
    {
        var response = await _client.GetAsync("/api/v1/flights/lowest-fares?origin=AMS&destination=KEF&from=2026-11-03&to=2027-02-01", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
