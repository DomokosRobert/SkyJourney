using System.Text.RegularExpressions;
using SkyJourney.Domain.Airports;
using SkyJourney.Domain.Common;
using SkyJourney.Domain.Fleet;

namespace SkyJourney.Domain.Flights;

/// <summary>
/// A recurring timetable entry ("SJ1201 BUD→LTN 07:05 local, Mon/Wed/Fri") from which dated
/// <see cref="Flight"/> instances are generated.
/// </summary>
public sealed partial class FlightSchedule : Entity
{
    private FlightSchedule()
    {
    }

    public string FlightNumber { get; private set; } = null!;

    public Guid AirlineId { get; private set; }

    public Airline Airline { get; private set; } = null!;

    public Guid OriginAirportId { get; private set; }

    public Airport Origin { get; private set; } = null!;

    public Guid DestinationAirportId { get; private set; }

    public Airport Destination { get; private set; } = null!;

    public Guid AircraftTypeId { get; private set; }

    public AircraftType AircraftType { get; private set; } = null!;

    public TimeOnly DepartureLocalTime { get; private set; }

    public int DurationMinutes { get; private set; }

    public OperatingDays OperatingDays { get; private set; }

    public DateOnly ValidFrom { get; private set; }

    public DateOnly ValidTo { get; private set; }

    /// <summary>Reference price of the Basic fare before demand adjustment.</summary>
    public Money BaseFare { get; private set; } = null!;

    public static FlightSchedule Create(string flightNumber, Airline airline, Airport origin, Airport destination,
        AircraftType aircraft, TimeOnly departureLocalTime, int durationMinutes, OperatingDays operatingDays,
        DateOnly validFrom, DateOnly validTo, Money baseFare)
    {
        var normalized = flightNumber.Trim().ToUpperInvariant();
        if (!FlightNumberRegex().IsMatch(normalized) || !normalized.StartsWith(airline.Code, StringComparison.Ordinal))
        {
            throw new DomainException("invalid_flight_number", $"'{flightNumber}' must be the airline code followed by 1-4 digits.");
        }

        if (origin.Id == destination.Id)
        {
            throw new DomainException("invalid_route", "Origin and destination must differ.");
        }

        if (durationMinutes is < 20 or > 1200)
        {
            throw new DomainException("invalid_duration", "Duration must be between 20 and 1200 minutes.");
        }

        if (operatingDays == OperatingDays.None || validTo < validFrom)
        {
            throw new DomainException("invalid_schedule", "A schedule needs at least one operating day and a valid date range.");
        }

        return new FlightSchedule
        {
            FlightNumber = normalized,
            AirlineId = airline.Id,
            Airline = airline,
            OriginAirportId = origin.Id,
            Origin = origin,
            DestinationAirportId = destination.Id,
            Destination = destination,
            AircraftTypeId = aircraft.Id,
            AircraftType = aircraft,
            DepartureLocalTime = departureLocalTime,
            DurationMinutes = durationMinutes,
            OperatingDays = operatingDays,
            ValidFrom = validFrom,
            ValidTo = validTo,
            BaseFare = baseFare,
        };
    }

    public bool OperatesOn(DateOnly date) =>
        date >= ValidFrom && date <= ValidTo && OperatingDays.Includes(date.DayOfWeek);

    /// <summary>
    /// Creates flight instances for every operating day in the window, skipping dates in
    /// <paramref name="existingDates"/> so the operation is idempotent.
    /// Requires <see cref="Airline"/>, <see cref="Origin"/>, <see cref="Destination"/> and
    /// <see cref="AircraftType"/> (with seats) to be loaded.
    /// </summary>
    public IEnumerable<Flight> GenerateFlights(DateOnly from, DateOnly to, IReadOnlySet<DateOnly> existingDates)
    {
        var start = from > ValidFrom ? from : ValidFrom;
        var end = to < ValidTo ? to : ValidTo;

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (OperatesOn(date) && !existingDates.Contains(date))
            {
                yield return Flight.Create(FlightNumber, Airline, Origin, Destination, AircraftType, date,
                    DepartureLocalTime, TimeSpan.FromMinutes(DurationMinutes), BaseFare, Id);
            }
        }
    }

    [GeneratedRegex("^[A-Z0-9]{2}[0-9]{1,4}$")]
    private static partial Regex FlightNumberRegex();
}
