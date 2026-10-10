using SkyJourney.Domain.Common;
using SkyJourney.Domain.Flights;

namespace SkyJourney.UnitTests.Domain;

public sealed class FlightScheduleTests
{
    private static FlightSchedule Schedule(OperatingDays days) => FlightSchedule.Create(
        "SJ1201", TestData.Airline(), TestData.Budapest(), TestData.London(), TestData.SmallAircraft(),
        new TimeOnly(7, 5), 150, days, new DateOnly(2026, 10, 19), new DateOnly(2026, 11, 1), new Money(40, "EUR"));

    [Fact]
    public void Generates_flights_only_on_operating_days_inside_validity()
    {
        var flights = Schedule(OperatingDays.Monday | OperatingDays.Friday)
            .GenerateFlights(new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31), new HashSet<DateOnly>())
            .ToList();

        Assert.Equal(
            [new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 23), new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 30)],
            flights.Select(f => f.DepartureLocalDate));
    }

    [Fact]
    public void Generation_is_idempotent_for_existing_dates()
    {
        var existing = new HashSet<DateOnly> { new(2026, 10, 19) };
        var flights = Schedule(OperatingDays.Monday).GenerateFlights(new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 26), existing).ToList();

        Assert.Single(flights);
        Assert.Equal(new DateOnly(2026, 10, 26), flights[0].DepartureLocalDate);
    }

    [Fact]
    public void Utc_departure_follows_daylight_saving_change()
    {
        var flights = Schedule(OperatingDays.Daily)
            .GenerateFlights(new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 26), new HashSet<DateOnly>())
            .ToDictionary(f => f.DepartureLocalDate);

        // Same 07:05 local departure; Budapest leaves summer time on 25 October.
        Assert.Equal(new TimeSpan(5, 5, 0), flights[new DateOnly(2026, 10, 24)].DepartureUtc.TimeOfDay);
        Assert.Equal(new TimeSpan(6, 5, 0), flights[new DateOnly(2026, 10, 26)].DepartureUtc.TimeOfDay);
        Assert.All(flights.Values, f => Assert.Equal(TimeSpan.FromMinutes(150), f.Duration));
    }

    [Fact]
    public void Generated_flights_offer_every_fare_family_in_ascending_price()
    {
        var flight = Schedule(OperatingDays.Daily)
            .GenerateFlights(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 20), new HashSet<DateOnly>())
            .Single();

        var prices = Enum.GetValues<FareFamily>().Select(f => flight.GetFare(f).Price.Amount).ToList();
        Assert.Equal(prices.Order(), prices);
        Assert.Equal(TestData.SmallAircraft().Capacity, flight.Capacity);
    }

    [Theory]
    [InlineData("XX123")]
    [InlineData("SJ12345")]
    [InlineData("SJ")]
    public void Rejects_flight_numbers_not_matching_the_airline(string flightNumber)
    {
        Assert.Throws<DomainException>(() => FlightSchedule.Create(flightNumber, TestData.Airline(), TestData.Budapest(),
            TestData.London(), TestData.SmallAircraft(), new TimeOnly(7, 0), 120, OperatingDays.Daily,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), new Money(40, "EUR")));
    }

    [Fact]
    public void Demo_pricing_is_deterministic()
    {
        var date = new DateOnly(2026, 11, 3);
        Assert.Equal(FarePricing.DemandFactor("SJ1201", date), FarePricing.DemandFactor("SJ1201", date));
        Assert.InRange(FarePricing.DemandFactor("SJ1201", date), 0.75m, 1.60m * 1.15m);
    }
}
