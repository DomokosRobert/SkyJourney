using SkyJourney.Domain.Airports;
using SkyJourney.Domain.Common;

namespace SkyJourney.Application.Common;

public sealed record MoneyDto(decimal Amount, string Currency)
{
    public static MoneyDto From(Money money) => new(money.Amount, money.Currency);
}

public sealed record AirportDto(string IataCode, string Name, string City, string CountryCode, string TimeZoneId)
{
    public static AirportDto From(Airport airport) =>
        new(airport.IataCode, airport.Name, airport.City, airport.CountryCode, airport.TimeZoneId);
}

/// <summary>Marks every flight-related response so clients can label data as non-real.</summary>
public static class DataSources
{
    public const string Demo = "demo";
}
