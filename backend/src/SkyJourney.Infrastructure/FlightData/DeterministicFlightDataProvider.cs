using SkyJourney.Application.Common;
using SkyJourney.Application.FlightData;
using SkyJourney.Domain.Common;
using SkyJourney.Domain.Flights;

namespace SkyJourney.Infrastructure.FlightData;

/// <summary>
/// Demo data set: real airports (public IATA codes, coordinates and time zones) served by the
/// fictional airline "SkyJourney Air" (SJ). Timetables, durations and prices are derived from
/// great-circle distance and a stable hash, so every run produces identical data. Nothing here
/// reflects real airline schedules or availability.
/// </summary>
public sealed class DeterministicFlightDataProvider : IFlightDataProvider
{
    public const string AirlineCode = "SJ";
    public const string Currency = "EUR";

    private static readonly DateOnly ValidFrom = new(2026, 1, 1);
    private static readonly DateOnly ValidTo = new(2035, 12, 31);

    private static readonly AirportData[] Airports =
    [
        new("BUD", "Budapest Ferenc Liszt International Airport", "Budapest", "HU", "Europe/Budapest", 47.4369, 19.2556),
        new("LHR", "London Heathrow Airport", "London", "GB", "Europe/London", 51.4700, -0.4543),
        new("LTN", "London Luton Airport", "London", "GB", "Europe/London", 51.8747, -0.3683),
        new("CDG", "Paris Charles de Gaulle Airport", "Paris", "FR", "Europe/Paris", 49.0097, 2.5479),
        new("AMS", "Amsterdam Airport Schiphol", "Amsterdam", "NL", "Europe/Amsterdam", 52.3105, 4.7683),
        new("FRA", "Frankfurt Airport", "Frankfurt", "DE", "Europe/Berlin", 50.0379, 8.5622),
        new("MUC", "Munich Airport", "Munich", "DE", "Europe/Berlin", 48.3537, 11.7750),
        new("BER", "Berlin Brandenburg Airport", "Berlin", "DE", "Europe/Berlin", 52.3667, 13.5033),
        new("VIE", "Vienna International Airport", "Vienna", "AT", "Europe/Vienna", 48.1103, 16.5697),
        new("ZRH", "Zurich Airport", "Zurich", "CH", "Europe/Zurich", 47.4582, 8.5555),
        new("FCO", "Rome Fiumicino Airport", "Rome", "IT", "Europe/Rome", 41.8003, 12.2389),
        new("MXP", "Milan Malpensa Airport", "Milan", "IT", "Europe/Rome", 45.6306, 8.7281),
        new("BCN", "Barcelona-El Prat Airport", "Barcelona", "ES", "Europe/Madrid", 41.2974, 2.0833),
        new("MAD", "Adolfo Suárez Madrid-Barajas Airport", "Madrid", "ES", "Europe/Madrid", 40.4983, -3.5676),
        new("LIS", "Lisbon Humberto Delgado Airport", "Lisbon", "PT", "Europe/Lisbon", 38.7742, -9.1342),
        new("ATH", "Athens International Airport", "Athens", "GR", "Europe/Athens", 37.9364, 23.9445),
        new("CPH", "Copenhagen Airport", "Copenhagen", "DK", "Europe/Copenhagen", 55.6180, 12.6508),
        new("ARN", "Stockholm Arlanda Airport", "Stockholm", "SE", "Europe/Stockholm", 59.6519, 17.9186),
        new("OSL", "Oslo Gardermoen Airport", "Oslo", "NO", "Europe/Oslo", 60.1976, 11.1004),
        new("HEL", "Helsinki-Vantaa Airport", "Helsinki", "FI", "Europe/Helsinki", 60.3172, 24.9633),
        new("DUB", "Dublin Airport", "Dublin", "IE", "Europe/Dublin", 53.4213, -6.2701),
        new("WAW", "Warsaw Chopin Airport", "Warsaw", "PL", "Europe/Warsaw", 52.1657, 20.9671),
        new("PRG", "Václav Havel Airport Prague", "Prague", "CZ", "Europe/Prague", 50.1008, 14.2600),
        new("OTP", "Henri Coandă International Airport", "Bucharest", "RO", "Europe/Bucharest", 44.5711, 26.0850),
        new("CLJ", "Cluj International Airport", "Cluj-Napoca", "RO", "Europe/Bucharest", 46.7852, 23.6862),
        new("SPU", "Split Airport", "Split", "HR", "Europe/Zagreb", 43.5389, 16.2980),
        new("IST", "Istanbul Airport", "Istanbul", "TR", "Europe/Istanbul", 41.2753, 28.7519),
        new("TLV", "Ben Gurion Airport", "Tel Aviv", "IL", "Asia/Jerusalem", 32.0114, 34.8867),
        new("DXB", "Dubai International Airport", "Dubai", "AE", "Asia/Dubai", 25.2532, 55.3657),
        new("KEF", "Keflavík International Airport", "Reykjavík", "IS", "Atlantic/Reykjavik", 63.9850, -22.6056),
    ];

    private static readonly AircraftTypeData[] AircraftTypes =
    [
        new("BCS3", "Airbus A220-300", 27, "ABCDE", 3, [11]),
        new("A320", "Airbus A320", 30, "ABCDEF", 3, [1, 12, 13]),
        new("A21N", "Airbus A321neo", 37, "ABCDEF", 4, [1, 15, 16]),
    ];

    /// <summary>Routes flown in both directions, with daily frequency per direction.</summary>
    private static readonly (string A, string B, int Frequency)[] Routes =
    [
        ("BUD", "LHR", 3), ("BUD", "CDG", 3), ("BUD", "AMS", 3), ("BUD", "FRA", 3),
        ("BUD", "LTN", 2), ("BUD", "MUC", 2), ("BUD", "BER", 2), ("BUD", "FCO", 2), ("BUD", "BCN", 2),
        ("BUD", "MAD", 1), ("BUD", "LIS", 1), ("BUD", "ATH", 2), ("BUD", "VIE", 1), ("BUD", "ZRH", 1),
        ("BUD", "MXP", 1), ("BUD", "CPH", 1), ("BUD", "ARN", 1), ("BUD", "OSL", 1), ("BUD", "HEL", 1),
        ("BUD", "DUB", 1), ("BUD", "WAW", 1), ("BUD", "PRG", 1), ("BUD", "OTP", 1), ("BUD", "CLJ", 1),
        ("BUD", "SPU", 1), ("BUD", "IST", 2), ("BUD", "TLV", 1), ("BUD", "DXB", 1), ("BUD", "KEF", 1),
        ("LHR", "AMS", 0), ("CDG", "BCN", 0), ("FRA", "LIS", 0), ("VIE", "ATH", 0), ("LTN", "CLJ", 0), ("AMS", "KEF", 0),
    ];

    /// <summary>Departure banks per daily frequency (0 = a few days a week).</summary>
    private static readonly TimeOnly[][] Banks =
    [
        [new(10, 20)],
        [new(9, 40)],
        [new(7, 5), new(17, 30)],
        [new(6, 40), new(12, 15), new(19, 25)],
    ];

    private static readonly OperatingDays[] WeeklyPatterns =
    [
        OperatingDays.Monday | OperatingDays.Wednesday | OperatingDays.Friday | OperatingDays.Sunday,
        OperatingDays.Tuesday | OperatingDays.Thursday | OperatingDays.Saturday | OperatingDays.Sunday,
        OperatingDays.Monday | OperatingDays.Thursday | OperatingDays.Friday | OperatingDays.Sunday,
    ];

    public string Source => DataSources.Demo;

    public Task<FlightDataSet> GetDataSetAsync(CancellationToken cancellationToken) => Task.FromResult(Build());

    public static FlightDataSet Build()
    {
        var airports = Airports.ToDictionary(a => a.IataCode);
        var schedules = new List<ScheduleData>();

        for (var routeIndex = 0; routeIndex < Routes.Length; routeIndex++)
        {
            var (a, b, frequency) = Routes[routeIndex];
            var distanceKm = DistanceKm(airports[a], airports[b]);
            var duration = RoundTo5(30 + distanceKm / 800 * 60);
            var baseFare = Math.Round(25 + (decimal)distanceKm * 0.045m);
            var aircraft = distanceKm < 900 ? "BCS3" : distanceKm < 2500 ? "A320" : "A21N";

            for (var direction = 0; direction < 2; direction++)
            {
                var (origin, destination) = direction == 0 ? (a, b) : (b, a);
                var hash = StableHash.Fnv1A($"{origin}-{destination}");
                var days = frequency == 0 ? WeeklyPatterns[hash % WeeklyPatterns.Length] : OperatingDays.Daily;
                var bank = Banks[frequency];

                for (var slot = 0; slot < bank.Length; slot++)
                {
                    // Spread departures within ±50 minutes of the bank so routes don't all leave at once.
                    var jitter = (int)(StableHash.Fnv1A($"{origin}-{destination}-{slot}") % 21) * 5 - 50;
                    var departure = bank[slot].AddMinutes(jitter + (direction == 1 ? 35 : 0));
                    var flightNumber = $"{AirlineCode}{100 + routeIndex * 10 + slot * 2 + direction}";

                    schedules.Add(new ScheduleData(flightNumber, AirlineCode, origin, destination, aircraft, departure,
                        duration, days, ValidFrom, ValidTo, baseFare, Currency));
                }
            }
        }

        return new FlightDataSet(Airports, [new AirlineData(AirlineCode, "SkyJourney Air")], AircraftTypes, schedules);
    }

    private static int RoundTo5(double minutes) => (int)Math.Round(minutes / 5) * 5;

    private static double DistanceKm(AirportData a, AirportData b)
    {
        const double earthRadiusKm = 6371;
        static double Rad(double degrees) => degrees * Math.PI / 180;
        var dLat = Rad(b.Latitude - a.Latitude);
        var dLon = Rad(b.Longitude - a.Longitude);
        var h = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(Rad(a.Latitude)) * Math.Cos(Rad(b.Latitude)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
    }
}
