using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using SkyJourney.Application.Airports;
using SkyJourney.Application.Common;
using SkyJourney.Domain.Airports;
using SkyJourney.Domain.Common;
using SkyJourney.Domain.Flights;

namespace SkyJourney.Application.Flights;

public sealed class FlightSearchService(ISkyJourneyDbContext db, AirportService airports, TimeProvider time)
{
    /// <summary>How far ahead customers may search.</summary>
    public const int BookingHorizonDays = 365;

    /// <summary>Maximum range of the lowest-fare calendar.</summary>
    public const int MaxCalendarDays = 62;

    /// <summary>Exact seat counts are only revealed when few remain ("3 seats left").</summary>
    public const int SeatsLeftThreshold = 9;

    public async Task<FlightSearchResponse> SearchAsync(FlightSearchQuery query, CancellationToken cancellationToken)
    {
        var origin = await airports.FindActiveAsync(query.Origin!, cancellationToken);
        var destination = await airports.FindActiveAsync(query.Destination!, cancellationToken);
        var now = time.GetUtcNow();

        var departureDate = query.DepartureDate!.Value;
        EnsureDateBookable(nameof(query.DepartureDate), departureDate, origin, now);

        var outbound = await FindOffersAsync(origin, destination, departureDate, query, now, cancellationToken);

        IReadOnlyList<FlightOfferDto>? inbound = null;
        if (query.ReturnDate is { } returnDate)
        {
            EnsureDateBookable(nameof(query.ReturnDate), returnDate, destination, now);
            inbound = await FindOffersAsync(destination, origin, returnDate, query, now, cancellationToken);
        }

        return new FlightSearchResponse(
            AirportDto.From(origin),
            AirportDto.From(destination),
            departureDate,
            query.ReturnDate,
            new PassengerCountDto(query.AdultCount, query.ChildCount),
            outbound,
            inbound,
            DataSources.Demo);
    }

    public async Task<FlightOfferDto> GetFlightAsync(Guid flightId, int passengers, CancellationToken cancellationToken)
    {
        var flight = await FlightsWithDetails().SingleOrDefaultAsync(f => f.Id == flightId, cancellationToken)
                     ?? throw new NotFoundException("Flight", flightId);

        return ToOffer(flight, Math.Clamp(passengers, 1, Domain.Bookings.Booking.MaxPassengers), time.GetUtcNow());
    }

    /// <summary>The cheapest bookable fare per day on a route, for the date picker.</summary>
    public async Task<LowestFaresResponse> GetLowestFaresAsync(string originCode, string destinationCode, DateOnly from, DateOnly to,
        CancellationToken cancellationToken)
    {
        if (to < from || to.DayNumber - from.DayNumber >= MaxCalendarDays)
        {
            throw new ValidationException([new ValidationFailure("to", $"The date range must span 1 to {MaxCalendarDays} days.")]);
        }

        var origin = await airports.FindActiveAsync(originCode, cancellationToken);
        var destination = await airports.FindActiveAsync(destinationCode, cancellationToken);
        var cutoff = time.GetUtcNow() + Flight.SalesCutoff;

        var fares = await db.Flights.AsNoTracking()
            .Where(f => f.OriginAirportId == origin.Id && f.DestinationAirportId == destination.Id &&
                        f.DepartureLocalDate >= from && f.DepartureLocalDate <= to &&
                        f.Status == FlightStatus.Scheduled && f.DepartureUtc >= cutoff && f.SeatsSold < f.Capacity)
            .SelectMany(f => f.Fares.Where(fare => fare.IsAvailable)
                .Select(fare => new { f.DepartureLocalDate, fare.Price.Amount, fare.Price.Currency }))
            .ToListAsync(cancellationToken);

        var days = fares
            .GroupBy(f => f.DepartureLocalDate)
            .Select(g => g.MinBy(f => f.Amount)!)
            .OrderBy(f => f.DepartureLocalDate)
            .Select(f => new LowestFareDto(f.DepartureLocalDate, new MoneyDto(f.Amount, f.Currency)))
            .ToList();

        return new LowestFaresResponse(origin.IataCode, destination.IataCode, days, DataSources.Demo);
    }

    private async Task<IReadOnlyList<FlightOfferDto>> FindOffersAsync(Airport origin, Airport destination, DateOnly date,
        FlightSearchQuery query, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var passengers = query.PassengerCount;
        var cutoff = now + Flight.SalesCutoff;

        var flights = await FlightsWithDetails()
            .Where(f => f.OriginAirportId == origin.Id && f.DestinationAirportId == destination.Id &&
                        f.DepartureLocalDate == date && f.Status == FlightStatus.Scheduled &&
                        f.DepartureUtc >= cutoff && f.Capacity - f.SeatsSold >= passengers)
            .ToListAsync(cancellationToken);

        var offers = flights
            .Select(f => ToOffer(f, passengers, now))
            .Where(o => o.LowestPricePerPassenger is not null)
            .Where(o => query.DepartAfter is not { } after || TimeOnly.FromDateTime(o.DepartureLocal.DateTime) >= after)
            .Where(o => query.DepartBefore is not { } before || TimeOnly.FromDateTime(o.DepartureLocal.DateTime) <= before)
            .Where(o => query.MaxPrice is not { } max || o.LowestPricePerPassenger!.Amount <= max);

        return query.SortOrder switch
        {
            FlightSortOrder.Departure => offers.OrderBy(o => o.DepartureUtc).ToList(),
            FlightSortOrder.Arrival => offers.OrderBy(o => o.ArrivalUtc).ToList(),
            FlightSortOrder.Duration => offers.OrderBy(o => o.DurationMinutes).ThenBy(o => o.DepartureUtc).ToList(),
            _ => offers.OrderBy(o => o.LowestPricePerPassenger!.Amount).ThenBy(o => o.DepartureUtc).ToList(),
        };
    }

    private IQueryable<Flight> FlightsWithDetails() => db.Flights.AsNoTracking()
        .Include(f => f.Airline)
        .Include(f => f.Origin)
        .Include(f => f.Destination)
        .Include(f => f.AircraftType)
        .Include(f => f.Fares);

    private static FlightOfferDto ToOffer(Flight flight, int passengers, DateTimeOffset now)
    {
        var fares = flight.Fares
            .Where(f => f.IsAvailable)
            .OrderBy(f => f.Family)
            .Select(f => new FareOfferDto(
                f.Family,
                MoneyDto.From(f.Price),
                MoneyDto.From(f.Price.Multiply(passengers)),
                FareConditionsDto.From(f.Conditions)))
            .ToList();

        return new FlightOfferDto(
            flight.Id,
            flight.FlightNumber,
            new AirlineDto(flight.Airline.Code, flight.Airline.Name),
            AirportDto.From(flight.Origin),
            AirportDto.From(flight.Destination),
            TimeZoneConversion.ToLocal(flight.DepartureUtc, flight.Origin.TimeZone),
            flight.DepartureUtc,
            TimeZoneConversion.ToLocal(flight.ArrivalUtc, flight.Destination.TimeZone),
            flight.ArrivalUtc,
            (int)flight.Duration.TotalMinutes,
            flight.AircraftType.Name,
            flight.IsOpenForSale(now) && flight.SeatsAvailable >= passengers && fares.Count > 0,
            flight.SeatsAvailable <= SeatsLeftThreshold ? flight.SeatsAvailable : null,
            fares.Count > 0 ? fares.MinBy(f => f.PricePerPassenger.Amount)!.PricePerPassenger : null,
            fares);
    }

    /// <summary>Rejects dates before "today" at the airport, or beyond the booking horizon.</summary>
    private static void EnsureDateBookable(string field, DateOnly date, Airport airport, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(TimeZoneConversion.ToLocal(now, airport.TimeZone).DateTime);
        if (date < today)
        {
            throw new ValidationException([new ValidationFailure(field, "The date is in the past.")]);
        }

        if (date > today.AddDays(BookingHorizonDays))
        {
            throw new ValidationException([new ValidationFailure(field, $"Flights can be searched at most {BookingHorizonDays} days ahead.")]);
        }
    }
}
