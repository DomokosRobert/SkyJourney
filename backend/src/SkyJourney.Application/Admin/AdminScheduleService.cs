using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SkyJourney.Application.Common;
using SkyJourney.Application.FlightData;
using SkyJourney.Application.Flights;
using SkyJourney.Domain.Common;
using SkyJourney.Domain.Flights;

namespace SkyJourney.Application.Admin;

/// <summary>
/// Timetable maintenance. Creating a schedule immediately generates its flights for the configured horizon; the
/// horizon job keeps generating further days as time passes. Schedules are not edited in place: changing a
/// timetable that already has sold flights is a re-accommodation problem, out of scope for this project (cancel
/// the affected flights instead, which refunds their bookings).
/// </summary>
public sealed class AdminScheduleService(
    ISkyJourneyDbContext db,
    FlightDataImporter importer,
    IOptions<FlightDataOptions> options,
    TimeProvider time)
{
    public async Task<PagedResult<AdminScheduleDto>> ListAsync(PageRequest page, CancellationToken cancellationToken)
    {
        var query = db.FlightSchedules.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var schedules = await query
            .Include(s => s.Airline)
            .Include(s => s.Origin)
            .Include(s => s.Destination)
            .Include(s => s.AircraftType)
            .OrderBy(s => s.FlightNumber).ThenBy(s => s.ValidFrom)
            .Skip(page.Skip).Take(page.Size)
            .ToListAsync(cancellationToken);
        return new PagedResult<AdminScheduleDto>(schedules.Select(ToDto).ToList(), Math.Max(page.Page, 1), page.Size, total);
    }

    public async Task<CreateScheduleResult> CreateAsync(CreateScheduleRequest request, CancellationToken cancellationToken)
    {
        var airlineCode = request.AirlineCode.Trim().ToUpperInvariant();
        var airline = await db.Airlines.SingleOrDefaultAsync(a => a.Code == airlineCode, cancellationToken)
                      ?? throw new UnprocessableException("airline_not_found", $"Airline '{airlineCode}' does not exist.");
        var origin = await FindAirportAsync(request.OriginIata, cancellationToken);
        var destination = await FindAirportAsync(request.DestinationIata, cancellationToken);
        var aircraftCode = request.AircraftCode.Trim().ToUpperInvariant();
        var aircraft = await db.AircraftTypes.SingleOrDefaultAsync(a => a.Code == aircraftCode, cancellationToken)
                       ?? throw new UnprocessableException("aircraft_not_found", $"Aircraft type '{aircraftCode}' does not exist.");

        var days = request.OperatingDays.Aggregate(OperatingDays.None, (all, day) => all | OperatingDaysExtensions.ToFlag(day));
        var schedule = FlightSchedule.Create(request.FlightNumber, airline, origin, destination, aircraft,
            request.DepartureLocalTime, request.DurationMinutes, days, request.ValidFrom, request.ValidTo,
            new Money(request.BaseFare, request.Currency));

        // Flights are unique per flight number and local date, so two schedules for one number must not overlap.
        var overlaps = await db.FlightSchedules.AnyAsync(s => s.FlightNumber == schedule.FlightNumber &&
                                                              s.ValidFrom <= schedule.ValidTo && s.ValidTo >= schedule.ValidFrom,
            cancellationToken);
        if (overlaps)
        {
            throw new ConflictException("schedule_overlap",
                $"Flight {schedule.FlightNumber} already has a schedule overlapping {schedule.ValidFrom:yyyy-MM-dd} to {schedule.ValidTo:yyyy-MM-dd}.");
        }

        db.FlightSchedules.Add(schedule);
        await db.SaveChangesAsync(cancellationToken);

        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        int generated;
        try
        {
            generated = await importer.GenerateFlightsAsync(today, today.AddDays(options.Value.HorizonDays), cancellationToken, schedule.Id);
        }
        catch (UniqueConstraintViolationException)
        {
            // The horizon job generated some of the same flights concurrently; it fills any remaining gap on its next run.
            generated = 0;
        }

        return new CreateScheduleResult(ToDto(schedule), generated);
    }

    private async Task<Domain.Airports.Airport> FindAirportAsync(string iataCode, CancellationToken cancellationToken)
    {
        var code = iataCode.Trim().ToUpperInvariant();
        return await db.Airports.SingleOrDefaultAsync(a => a.IataCode == code && a.IsActive, cancellationToken)
               ?? throw new UnprocessableException("airport_not_found", $"Active airport '{code}' does not exist.");
    }

    private static AdminScheduleDto ToDto(FlightSchedule s) => new(
        s.Id,
        s.FlightNumber,
        new AirlineDto(s.Airline.Code, s.Airline.Name),
        AirportDto.From(s.Origin),
        AirportDto.From(s.Destination),
        s.AircraftType.Code,
        s.DepartureLocalTime,
        s.DurationMinutes,
        Enum.GetValues<DayOfWeek>().Where(d => s.OperatingDays.Includes(d))
            .OrderBy(d => ((int)d + 6) % 7) // Monday first
            .ToList(),
        s.ValidFrom,
        s.ValidTo,
        MoneyDto.From(s.BaseFare));
}
