using SkyJourney.Domain.Flights;

namespace SkyJourney.Application.FlightData;

/// <summary>
/// Source of reference data and timetables (ADR-0007). The only implementation today is a
/// deterministic demo provider; a licensed schedule feed could implement the same contract.
/// </summary>
public interface IFlightDataProvider
{
    /// <summary>Identifies the data origin; surfaced to clients as <c>dataSource</c>.</summary>
    string Source { get; }

    Task<FlightDataSet> GetDataSetAsync(CancellationToken cancellationToken);
}

public sealed record FlightDataSet(
    IReadOnlyList<AirportData> Airports,
    IReadOnlyList<AirlineData> Airlines,
    IReadOnlyList<AircraftTypeData> AircraftTypes,
    IReadOnlyList<ScheduleData> Schedules);

public sealed record AirportData(
    string IataCode, string Name, string City, string CountryCode, string TimeZoneId, double Latitude, double Longitude);

public sealed record AirlineData(string Code, string Name);

public sealed record AircraftTypeData(
    string Code, string Name, int Rows, string SeatLetters, int FrontRows, IReadOnlyList<int> ExtraLegroomRows);

public sealed record ScheduleData(
    string FlightNumber,
    string AirlineCode,
    string OriginIata,
    string DestinationIata,
    string AircraftCode,
    TimeOnly DepartureLocalTime,
    int DurationMinutes,
    OperatingDays OperatingDays,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    decimal BaseFare,
    string Currency);
