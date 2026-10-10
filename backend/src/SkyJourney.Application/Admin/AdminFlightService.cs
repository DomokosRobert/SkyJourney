using Microsoft.EntityFrameworkCore;
using SkyJourney.Application.Bookings;
using SkyJourney.Application.Common;
using SkyJourney.Application.Flights;
using SkyJourney.Domain.Bookings;
using SkyJourney.Domain.Common;
using SkyJourney.Domain.Flights;

namespace SkyJourney.Application.Admin;

/// <summary>Operational control of dated flights: inspect load, reprice or close fares, and cancel flights.</summary>
public sealed class AdminFlightService(
    ISkyJourneyDbContext db,
    AdminBookingService bookings,
    BookingExpiryService expiry)
{
    public async Task<PagedResult<AdminFlightDto>> ListAsync(AdminFlightQuery filter, CancellationToken cancellationToken)
    {
        var page = new PageRequest(filter.Page ?? 1, filter.PageSize ?? 20);
        var query = db.Flights.AsNoTracking();
        if (filter.Date is { } date)
        {
            query = query.Where(f => f.DepartureLocalDate == date);
        }

        if (!string.IsNullOrWhiteSpace(filter.Origin))
        {
            var origin = filter.Origin.Trim().ToUpperInvariant();
            query = query.Where(f => f.Origin.IataCode == origin);
        }

        if (!string.IsNullOrWhiteSpace(filter.Destination))
        {
            var destination = filter.Destination.Trim().ToUpperInvariant();
            query = query.Where(f => f.Destination.IataCode == destination);
        }

        if (!string.IsNullOrWhiteSpace(filter.FlightNumber))
        {
            var number = filter.FlightNumber.Trim().ToUpperInvariant();
            query = query.Where(f => f.FlightNumber == number);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(f => f.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var flights = await WithDetails(query)
            .OrderBy(f => f.DepartureUtc).ThenBy(f => f.FlightNumber)
            .Skip(page.Skip).Take(page.Size)
            .ToListAsync(cancellationToken);
        return new PagedResult<AdminFlightDto>(flights.Select(ToDto).ToList(), Math.Max(page.Page, 1), page.Size, total);
    }

    public async Task<AdminFlightDto> GetAsync(Guid flightId, CancellationToken cancellationToken) =>
        ToDto(await WithDetails(db.Flights.AsNoTracking()).SingleOrDefaultAsync(f => f.Id == flightId, cancellationToken)
              ?? throw new NotFoundException("Flight", flightId));

    /// <summary>
    /// Sets a fare's price and availability. Bookings already made keep the price they were sold at (fare snapshot);
    /// unpaid bookings are not repriced either.
    /// </summary>
    public async Task<AdminFlightDto> UpdateFareAsync(Guid flightId, string family, UpdateFareRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<FareFamily>(family, ignoreCase: true, out var fareFamily) || !Enum.IsDefined(fareFamily))
        {
            throw new NotFoundException("Fare family", family);
        }

        var flight = await db.Flights.Include(f => f.Fares).SingleOrDefaultAsync(f => f.Id == flightId, cancellationToken)
                     ?? throw new NotFoundException("Flight", flightId);
        if (flight.Status == FlightStatus.Cancelled)
        {
            throw new ConflictException("flight_cancelled", $"Flight {flight.FlightNumber} is cancelled.");
        }

        var fare = flight.Fares.SingleOrDefault(f => f.Family == fareFamily)
                   ?? throw new NotFoundException("Fare", $"{flight.FlightNumber} {fareFamily}");
        fare.ChangePrice(new Money(request.Price, fare.Price.Currency));
        fare.SetAvailability(request.IsAvailable);
        await db.SaveChangesAsync(cancellationToken);

        db.ChangeTracker.Clear();
        return await GetAsync(flightId, cancellationToken);
    }

    /// <summary>
    /// Cancels the flight (no new bookings or payments) and then every active booking on it, with a full refund.
    /// Bookings that cannot be cancelled at this moment (a payment is in progress) are reported as skipped; calling
    /// this again for the already cancelled flight processes them, so the operation is safe to repeat.
    /// </summary>
    public async Task<FlightCancellationResultDto> CancelAsync(Guid flightId, CancelRequest request, CancellationToken cancellationToken)
    {
        var flight = await db.Flights.SingleOrDefaultAsync(f => f.Id == flightId, cancellationToken)
                     ?? throw new NotFoundException("Flight", flightId);
        if (flight.Status == FlightStatus.Scheduled)
        {
            // Committed before touching bookings: from here on capacity reservations on this flight fail
            // (FlightCapacity checks the status in the same statement) and payments are refused.
            flight.Cancel();
            await db.SaveChangesAsync(cancellationToken);
        }

        db.ChangeTracker.Clear();

        // Lapsed holds become Expired (and release their places) rather than being reported as cancellations.
        while (await expiry.ExpireDueAsync([flightId], BookingExpiryService.LazyBatchSize, cancellationToken) ==
               BookingExpiryService.LazyBatchSize)
        {
        }

        var active = await db.Bookings.AsNoTracking()
            .Where(b => (b.Status == BookingStatus.PendingPayment || b.Status == BookingStatus.Confirmed) &&
                        b.Segments.Any(s => s.FlightId == flightId))
            .OrderBy(b => b.CreatedAt)
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);

        var reason = $"Flight {flight.FlightNumber} cancelled: {request.Reason.Trim()}";
        var cancelled = 0;
        var refunded = new Dictionary<string, decimal>();
        var skipped = new List<SkippedBooking>();
        foreach (var bookingId in active)
        {
            var (refund, skip) = await bookings.TryCancelAsync(bookingId, reason, cancellationToken);
            if (skip is not null)
            {
                skipped.Add(skip);
            }
            else if (refund is not null)
            {
                cancelled++;
                refunded[refund.Currency] = refunded.GetValueOrDefault(refund.Currency) + refund.Amount;
            }
        }

        return new FlightCancellationResultDto(
            await GetAsync(flightId, cancellationToken),
            cancelled,
            refunded.OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => new MoneyDto(r.Value, r.Key)).ToList(),
            skipped);
    }

    private static IQueryable<Flight> WithDetails(IQueryable<Flight> flights) => flights
        .Include(f => f.Airline)
        .Include(f => f.Origin)
        .Include(f => f.Destination)
        .Include(f => f.AircraftType)
        .Include(f => f.Fares)
        .AsSplitQuery();

    private static AdminFlightDto ToDto(Flight f) => new(
        f.Id,
        f.FlightNumber,
        f.FlightScheduleId,
        new AirlineDto(f.Airline.Code, f.Airline.Name),
        AirportDto.From(f.Origin),
        AirportDto.From(f.Destination),
        TimeZoneConversion.ToLocal(f.DepartureUtc, f.Origin.TimeZone),
        f.DepartureUtc,
        TimeZoneConversion.ToLocal(f.ArrivalUtc, f.Destination.TimeZone),
        f.ArrivalUtc,
        f.AircraftType.Name,
        f.Status,
        f.Capacity,
        f.SeatsSold,
        f.Fares.OrderBy(x => x.Family)
            .Select(x => new AdminFareDto(x.Family, MoneyDto.From(x.Price), x.IsAvailable, FareConditionsDto.From(x.Conditions)))
            .ToList());
}
