using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkyJourney.Application.Common;
using SkyJourney.Domain.Airports;
using SkyJourney.Domain.Common;
using SkyJourney.Domain.Fleet;
using SkyJourney.Domain.Flights;

namespace SkyJourney.Application.FlightData;

public sealed record ImportResult(int Airports, int Airlines, int AircraftTypes, int Schedules);

/// <summary>
/// Loads a provider's data set into the database and turns schedules into dated flights.
/// Both operations are idempotent: existing rows (matched by natural key) are left untouched,
/// so re-running never overwrites changes an administrator made.
/// </summary>
public sealed partial class FlightDataImporter(
    ISkyJourneyDbContext db,
    IFlightDataProvider provider,
    ILogger<FlightDataImporter> logger)
{
    public async Task<ImportResult> ImportReferenceDataAsync(CancellationToken cancellationToken = default)
    {
        var data = await provider.GetDataSetAsync(cancellationToken);

        var airports = await db.Airports.ToDictionaryAsync(a => a.IataCode, cancellationToken);
        var newAirports = data.Airports.Where(a => !airports.ContainsKey(a.IataCode))
            .Select(a => Airport.Create(a.IataCode, a.Name, a.City, a.CountryCode, a.TimeZoneId, a.Latitude, a.Longitude))
            .ToList();
        db.Airports.AddRange(newAirports);
        newAirports.ForEach(a => airports[a.IataCode] = a);

        var airlines = await db.Airlines.ToDictionaryAsync(a => a.Code, cancellationToken);
        var newAirlines = data.Airlines.Where(a => !airlines.ContainsKey(a.Code))
            .Select(a => Airline.Create(a.Code, a.Name))
            .ToList();
        db.Airlines.AddRange(newAirlines);
        newAirlines.ForEach(a => airlines[a.Code] = a);

        var aircraft = await db.AircraftTypes.ToDictionaryAsync(a => a.Code, cancellationToken);
        var newAircraft = data.AircraftTypes.Where(a => !aircraft.ContainsKey(a.Code))
            .Select(a => AircraftType.Create(a.Code, a.Name, a.Rows, a.SeatLetters, a.FrontRows, a.ExtraLegroomRows))
            .ToList();
        db.AircraftTypes.AddRange(newAircraft);
        newAircraft.ForEach(a => aircraft[a.Code] = a);

        var existingSchedules = (await db.FlightSchedules.Select(s => s.FlightNumber).ToListAsync(cancellationToken)).ToHashSet();
        var newSchedules = data.Schedules.Where(s => !existingSchedules.Contains(s.FlightNumber))
            .Select(s => FlightSchedule.Create(s.FlightNumber, airlines[s.AirlineCode], airports[s.OriginIata],
                airports[s.DestinationIata], aircraft[s.AircraftCode], s.DepartureLocalTime, s.DurationMinutes,
                s.OperatingDays, s.ValidFrom, s.ValidTo, new Money(s.BaseFare, s.Currency)))
            .ToList();
        db.FlightSchedules.AddRange(newSchedules);

        await db.SaveChangesAsync(cancellationToken);

        var result = new ImportResult(newAirports.Count, newAirlines.Count, newAircraft.Count, newSchedules.Count);
        LogImported(logger, provider.Source, result.Airports, result.Airlines, result.AircraftTypes, result.Schedules);
        return result;
    }

    /// <summary>Creates flights for every schedule operating between <paramref name="from"/> and <paramref name="to"/> (inclusive).</summary>
    public async Task<int> GenerateFlightsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var schedules = await db.FlightSchedules
            .Include(s => s.Airline)
            .Include(s => s.Origin)
            .Include(s => s.Destination)
            .Include(s => s.AircraftType)
            .Where(s => s.ValidFrom <= to && s.ValidTo >= from)
            .OrderBy(s => s.FlightNumber)
            .ToListAsync(cancellationToken);

        var existing = (await db.Flights
                .Where(f => f.DepartureLocalDate >= from && f.DepartureLocalDate <= to)
                .Select(f => new { f.FlightNumber, f.DepartureLocalDate })
                .ToListAsync(cancellationToken))
            .GroupBy(f => f.FlightNumber)
            .ToDictionary(g => g.Key, g => (IReadOnlySet<DateOnly>)g.Select(f => f.DepartureLocalDate).ToHashSet());

        var created = 0;
        foreach (var schedule in schedules)
        {
            var flights = schedule.GenerateFlights(from, to, existing.GetValueOrDefault(schedule.FlightNumber) ?? new HashSet<DateOnly>()).ToList();
            if (flights.Count == 0)
            {
                continue;
            }

            db.Flights.AddRange(flights);
            await db.SaveChangesAsync(cancellationToken);
            created += flights.Count;

            // Keep the change tracker small: generated flights are not needed after they are saved.
            foreach (var flight in flights)
            {
                foreach (var fare in flight.Fares)
                {
                    db.Entry(fare).State = EntityState.Detached;
                }

                db.Entry(flight).State = EntityState.Detached;
            }
        }

        LogGenerated(logger, created, from, to);
        return created;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Imported flight data from {Source}: {Airports} airports, {Airlines} airlines, {AircraftTypes} aircraft types, {Schedules} schedules")]
    private static partial void LogImported(ILogger logger, string source, int airports, int airlines, int aircraftTypes, int schedules);

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated {Count} flights for {From} to {To}")]
    private static partial void LogGenerated(ILogger logger, int count, DateOnly from, DateOnly to);
}
