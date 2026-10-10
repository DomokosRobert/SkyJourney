using System.Text.RegularExpressions;
using SkyJourney.Domain.Common;

namespace SkyJourney.Domain.Airports;

public sealed partial class Airport : Entity
{
    private Airport()
    {
    }

    public string IataCode { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string City { get; private set; } = null!;

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public string CountryCode { get; private set; } = null!;

    /// <summary>IANA time zone identifier, e.g. <c>Europe/Budapest</c>.</summary>
    public string TimeZoneId { get; private set; } = null!;

    public double Latitude { get; private set; }

    public double Longitude { get; private set; }

    public bool IsActive { get; private set; }

    public TimeZoneInfo TimeZone => TimeZoneConversion.Find(TimeZoneId);

    public static Airport Create(string iataCode, string name, string city, string countryCode, string timeZoneId,
        double latitude, double longitude)
    {
        var airport = new Airport { IataCode = NormalizeIata(iataCode), IsActive = true };
        airport.Update(name, city, countryCode, timeZoneId, latitude, longitude);
        return airport;
    }

    public void Update(string name, string city, string countryCode, string timeZoneId, double latitude, double longitude)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(city))
        {
            throw new DomainException("invalid_airport", "Airport name and city are required.");
        }

        if (!CountryRegex().IsMatch(countryCode))
        {
            throw new DomainException("invalid_airport", "Country code must be an ISO 3166-1 alpha-2 code.");
        }

        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw new DomainException("invalid_airport", "Coordinates are out of range.");
        }

        _ = TimeZoneConversion.Find(timeZoneId);

        Name = name.Trim();
        City = city.Trim();
        CountryCode = countryCode;
        TimeZoneId = timeZoneId;
        Latitude = latitude;
        Longitude = longitude;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>Great-circle distance in kilometres (haversine).</summary>
    public double DistanceKmTo(Airport other)
    {
        const double earthRadiusKm = 6371;
        var dLat = ToRadians(other.Latitude - Latitude);
        var dLon = ToRadians(other.Longitude - Longitude);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(ToRadians(Latitude)) * Math.Cos(ToRadians(other.Latitude)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    public static string NormalizeIata(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return IataRegex().IsMatch(normalized)
            ? normalized
            : throw new DomainException("invalid_iata_code", $"'{code}' is not a valid 3-letter IATA airport code.");
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex IataRegex();

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex CountryRegex();
}
