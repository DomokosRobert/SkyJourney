using SkyJourney.Domain.Airports;
using SkyJourney.Domain.Common;
using SkyJourney.Domain.Fleet;
using SkyJourney.Domain.Flights;
using SkyJourney.Infrastructure.FlightData;

namespace SkyJourney.UnitTests.FlightData;

public sealed class DeterministicFlightDataProviderTests
{
    [Fact]
    public void Data_set_is_identical_across_runs()
    {
        var first = DeterministicFlightDataProvider.Build();
        var second = DeterministicFlightDataProvider.Build();

        Assert.Equal(first.Schedules, second.Schedules);
    }

    [Fact]
    public void Flight_numbers_and_airport_codes_are_unique()
    {
        var data = DeterministicFlightDataProvider.Build();

        Assert.Equal(data.Schedules.Count, data.Schedules.Select(s => s.FlightNumber).Distinct().Count());
        Assert.Equal(data.Airports.Count, data.Airports.Select(a => a.IataCode).Distinct().Count());
    }

    [Fact]
    public void Every_route_is_served_in_both_directions()
    {
        var routes = DeterministicFlightDataProvider.Build().Schedules.Select(s => (s.OriginIata, s.DestinationIata)).ToHashSet();

        Assert.All(routes, r => Assert.Contains((r.DestinationIata, r.OriginIata), routes));
    }

    [Fact]
    public void Every_entry_passes_domain_validation()
    {
        var data = DeterministicFlightDataProvider.Build();

        var airports = data.Airports.ToDictionary(a => a.IataCode,
            a => Airport.Create(a.IataCode, a.Name, a.City, a.CountryCode, a.TimeZoneId, a.Latitude, a.Longitude));
        var airline = Airline.Create(data.Airlines[0].Code, data.Airlines[0].Name);
        var aircraft = data.AircraftTypes.ToDictionary(a => a.Code,
            a => AircraftType.Create(a.Code, a.Name, a.Rows, a.SeatLetters, a.FrontRows, a.ExtraLegroomRows));

        foreach (var s in data.Schedules)
        {
            var schedule = FlightSchedule.Create(s.FlightNumber, airline, airports[s.OriginIata], airports[s.DestinationIata],
                aircraft[s.AircraftCode], s.DepartureLocalTime, s.DurationMinutes, s.OperatingDays, s.ValidFrom, s.ValidTo,
                new Money(s.BaseFare, s.Currency));

            Assert.NotEmpty(schedule.GenerateFlights(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8), new HashSet<DateOnly>()));
        }
    }

    [Fact]
    public void Longer_routes_take_longer_and_cost_more()
    {
        var schedules = DeterministicFlightDataProvider.Build().Schedules;
        var vienna = schedules.First(s => s is { OriginIata: "BUD", DestinationIata: "VIE" });
        var dubai = schedules.First(s => s is { OriginIata: "BUD", DestinationIata: "DXB" });

        Assert.True(dubai.DurationMinutes > vienna.DurationMinutes * 3);
        Assert.True(dubai.BaseFare > vienna.BaseFare);
    }
}
