using Microsoft.EntityFrameworkCore;
using SkyJourney.Application.Common;
using SkyJourney.Domain.Airports;

namespace SkyJourney.Application.Airports;

public sealed class AirportService(ISkyJourneyDbContext db, TimeProvider time)
{
    public const int MaxResults = 50;

    /// <summary>
    /// Finds active airports by IATA code, city or name. Exact code matches rank first, then
    /// prefix matches on code or city, then any other match.
    /// </summary>
    public async Task<IReadOnlyList<AirportDto>> SearchAsync(string? query, int? limit, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(limit ?? 20, 1, MaxResults);
        var term = query?.Trim().ToLowerInvariant() ?? string.Empty;

        var airports = db.Airports.AsNoTracking().Where(a => a.IsActive);
        if (term.Length > 0)
        {
            airports = airports.Where(a =>
                a.IataCode.ToLower().Contains(term) || a.City.ToLower().Contains(term) || a.Name.ToLower().Contains(term));
        }

        var matches = await airports.ToListAsync(cancellationToken);
        return matches
            .OrderBy(a => Rank(a, term))
            .ThenBy(a => a.City, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.IataCode, StringComparer.Ordinal)
            .Take(take)
            .Select(AirportDto.From)
            .ToList();
    }

    public async Task<AirportDto> GetAsync(string iataCode, CancellationToken cancellationToken) =>
        AirportDto.From(await FindActiveAsync(iataCode, cancellationToken));

    /// <summary>Airports with at least one upcoming flight from <paramref name="iataCode"/>.</summary>
    public async Task<IReadOnlyList<AirportDto>> GetDestinationsAsync(string iataCode, CancellationToken cancellationToken)
    {
        var origin = await FindActiveAsync(iataCode, cancellationToken);
        var now = time.GetUtcNow();

        var destinations = await db.Flights.AsNoTracking()
            .Where(f => f.OriginAirportId == origin.Id && f.DepartureUtc > now && f.Destination.IsActive)
            .Select(f => f.Destination)
            .Distinct()
            .ToListAsync(cancellationToken);

        return destinations.OrderBy(a => a.City, StringComparer.OrdinalIgnoreCase).Select(AirportDto.From).ToList();
    }

    internal async Task<Airport> FindActiveAsync(string iataCode, CancellationToken cancellationToken)
    {
        var code = iataCode.Trim().ToUpperInvariant();
        return await db.Airports.AsNoTracking().SingleOrDefaultAsync(a => a.IataCode == code && a.IsActive, cancellationToken)
               ?? throw new NotFoundException("Airport", code);
    }

    private static int Rank(Airport airport, string term)
    {
        if (term.Length == 0)
        {
            return 0;
        }

        if (airport.IataCode.Equals(term, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (airport.City.StartsWith(term, StringComparison.OrdinalIgnoreCase) ||
            airport.IataCode.StartsWith(term, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }
}
