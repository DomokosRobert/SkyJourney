using Microsoft.EntityFrameworkCore;
using SkyJourney.Application.Common;
using SkyJourney.Domain.Airports;

namespace SkyJourney.Application.Admin;

/// <summary>
/// Airport maintenance. Airports are never deleted: flights and bookings reference them. Deactivating one hides it
/// from search and autocomplete; existing flights and bookings are unaffected.
/// </summary>
public sealed class AdminAirportService(ISkyJourneyDbContext db)
{
    internal const string IataIndex = "ix_airports_iata_code";

    /// <summary>All airports, including inactive ones, ordered by code.</summary>
    public async Task<IReadOnlyList<AdminAirportDto>> ListAsync(CancellationToken cancellationToken) =>
        (await db.Airports.AsNoTracking().OrderBy(a => a.IataCode).ToListAsync(cancellationToken))
        .Select(AdminAirportDto.From)
        .ToList();

    public async Task<AdminAirportDto> CreateAsync(CreateAirportRequest request, CancellationToken cancellationToken)
    {
        var airport = Airport.Create(request.IataCode, request.Name, request.City, request.CountryCode, request.TimeZoneId,
            request.Latitude, request.Longitude);
        if (await db.Airports.AnyAsync(a => a.IataCode == airport.IataCode, cancellationToken))
        {
            throw AlreadyExists(airport.IataCode);
        }

        db.Airports.Add(airport);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == IataIndex)
        {
            throw AlreadyExists(airport.IataCode);
        }

        return AdminAirportDto.From(airport);
    }

    public async Task<AdminAirportDto> UpdateAsync(string iataCode, UpdateAirportRequest request, CancellationToken cancellationToken)
    {
        var airport = await FindAsync(iataCode, cancellationToken);
        airport.Update(request.Name, request.City, request.CountryCode, request.TimeZoneId, request.Latitude, request.Longitude);
        await db.SaveChangesAsync(cancellationToken);
        return AdminAirportDto.From(airport);
    }

    public async Task<AdminAirportDto> SetActiveAsync(string iataCode, bool active, CancellationToken cancellationToken)
    {
        var airport = await FindAsync(iataCode, cancellationToken);
        if (active)
        {
            airport.Activate();
        }
        else
        {
            airport.Deactivate();
        }

        await db.SaveChangesAsync(cancellationToken);
        return AdminAirportDto.From(airport);
    }

    private async Task<Airport> FindAsync(string iataCode, CancellationToken cancellationToken)
    {
        var code = iataCode.Trim().ToUpperInvariant();
        return await db.Airports.SingleOrDefaultAsync(a => a.IataCode == code, cancellationToken)
               ?? throw new NotFoundException("Airport", code);
    }

    private static ConflictException AlreadyExists(string code) =>
        new("airport_exists", $"An airport with code {code} already exists.");
}
