using SkyJourney.Application.Common;
using SkyJourney.Domain.Flights;

namespace SkyJourney.Application.Flights;

public enum FlightSortOrder
{
    Price,
    Departure,
    Arrival,
    Duration,
}

/// <summary>Query-string parameters of <c>GET /api/v1/flights/search</c>.</summary>
public sealed class FlightSearchQuery
{
    public string? Origin { get; init; }

    public string? Destination { get; init; }

    /// <summary>Departure date, local to the origin airport.</summary>
    public DateOnly? DepartureDate { get; init; }

    /// <summary>Return departure date, local to the destination airport. Omit for one-way.</summary>
    public DateOnly? ReturnDate { get; init; }

    public int? Adults { get; init; }

    public int? Children { get; init; }

    /// <summary><c>price</c> (default), <c>departure</c>, <c>arrival</c> or <c>duration</c>; case-insensitive.</summary>
    public string? Sort { get; init; }

    /// <summary>Only flights departing at or after this local time.</summary>
    public TimeOnly? DepartAfter { get; init; }

    /// <summary>Only flights departing at or before this local time.</summary>
    public TimeOnly? DepartBefore { get; init; }

    /// <summary>Maximum price per passenger of the cheapest available fare.</summary>
    public decimal? MaxPrice { get; init; }

    internal int AdultCount => Adults ?? 1;

    internal int ChildCount => Children ?? 0;

    internal int PassengerCount => AdultCount + ChildCount;

    internal FlightSortOrder? SortOrder =>
        string.IsNullOrEmpty(Sort) ? FlightSortOrder.Price
        : Enum.TryParse<FlightSortOrder>(Sort, ignoreCase: true, out var order) && Enum.IsDefined(order) ? order
        : null;
}

public sealed record AirlineDto(string Code, string Name);

public sealed record FareConditionsDto(bool IncludesCabinTrolley, int CheckedBaggageKg, bool IncludesSeatSelection, int RefundPercentage)
{
    public static FareConditionsDto From(FareConditions c) =>
        new(c.IncludesCabinTrolley, c.CheckedBaggageKg, c.IncludesSeatSelection, c.RefundPercentage);
}

public sealed record FareOfferDto(FareFamily Family, MoneyDto PricePerPassenger, MoneyDto TotalPrice, FareConditionsDto Conditions);

public sealed record FlightOfferDto(
    Guid FlightId,
    string FlightNumber,
    AirlineDto Airline,
    AirportDto Origin,
    AirportDto Destination,
    DateTimeOffset DepartureLocal,
    DateTimeOffset DepartureUtc,
    DateTimeOffset ArrivalLocal,
    DateTimeOffset ArrivalUtc,
    int DurationMinutes,
    string AircraftName,
    bool IsBookable,
    int? SeatsLeft,
    MoneyDto? LowestPricePerPassenger,
    IReadOnlyList<FareOfferDto> Fares);

public sealed record PassengerCountDto(int Adults, int Children);

public sealed record FlightSearchResponse(
    AirportDto Origin,
    AirportDto Destination,
    DateOnly DepartureDate,
    DateOnly? ReturnDate,
    PassengerCountDto Passengers,
    IReadOnlyList<FlightOfferDto> Outbound,
    IReadOnlyList<FlightOfferDto>? Return,
    string DataSource);

public sealed record LowestFareDto(DateOnly Date, MoneyDto PricePerPassenger);

public sealed record LowestFaresResponse(string Origin, string Destination, IReadOnlyList<LowestFareDto> Days, string DataSource);
