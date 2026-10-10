using SkyJourney.Domain.Airports;
using SkyJourney.Domain.Bookings;
using SkyJourney.Domain.Common;
using SkyJourney.Domain.Fleet;
using SkyJourney.Domain.Flights;

namespace SkyJourney.UnitTests.Domain;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    public static Airport Budapest() => Airport.Create("BUD", "Budapest Ferenc Liszt", "Budapest", "HU", "Europe/Budapest", 47.43, 19.26);

    public static Airport London() => Airport.Create("LTN", "London Luton", "London", "GB", "Europe/London", 51.87, -0.37);

    public static Airline Airline() => SkyJourney.Domain.Fleet.Airline.Create("SJ", "SkyJourney Air");

    public static AircraftType SmallAircraft() => AircraftType.Create("T100", "Test 100", rows: 5, seatLetters: "ABCD", frontRows: 1, extraLegroomRows: [3]);

    public static Flight Flight(Airport origin, Airport destination, DateOnly date, TimeOnly time, AircraftType? aircraft = null, decimal baseFare = 50m) =>
        SkyJourney.Domain.Flights.Flight.Create("SJ1201", Airline(), origin, destination, aircraft ?? SmallAircraft(), date, time,
            TimeSpan.FromMinutes(150), new Money(baseFare, "EUR"));

    /// <summary>A round trip departing 10 days after <see cref="Now"/>.</summary>
    public static (Flight Outbound, Flight Return) RoundTrip()
    {
        var bud = Budapest();
        var ltn = London();
        var aircraft = SmallAircraft();
        return (Flight(bud, ltn, new DateOnly(2026, 11, 11), new TimeOnly(7, 5), aircraft),
                Flight(ltn, bud, new DateOnly(2026, 11, 15), new TimeOnly(18, 30), aircraft));
    }

    public static IReadOnlyList<PassengerDetails> Passengers(int adults = 1, int children = 0) =>
        Enumerable.Range(1, adults).Select(i => new PassengerDetails($"Adult{i}", "Traveller", PassengerType.Adult))
            .Concat(Enumerable.Range(1, children).Select(i => new PassengerDetails($"Child{i}", "Traveller", PassengerType.Child)))
            .ToList();

    public static Booking Booking(Flight outbound, Flight? inbound = null, FareFamily family = FareFamily.Basic, int adults = 1, int children = 0)
    {
        var segments = new List<SegmentSelection> { new(outbound, JourneyDirection.Outbound, family) };
        if (inbound is not null)
        {
            segments.Add(new SegmentSelection(inbound, JourneyDirection.Return, family));
        }

        return SkyJourney.Domain.Bookings.Booking.Create(Guid.CreateVersion7(), BookingReference.Generate(), segments,
            Passengers(adults, children), "key-1", "hash", Now);
    }
}
