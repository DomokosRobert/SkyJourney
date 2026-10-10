using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkyJourney.Application.Common;
using SkyJourney.Application.Flights;
using SkyJourney.Domain.Bookings;
using SkyJourney.Domain.Common;
using SkyJourney.Domain.Flights;

namespace SkyJourney.Application.Bookings;

/// <summary>
/// The customer's booking use cases. Every query is scoped to the authenticated user: another user's booking
/// is indistinguishable from one that does not exist (404).
/// </summary>
public sealed class BookingService(ISkyJourneyDbContext db, ICurrentUser currentUser, BookingExpiryService expiry, TimeProvider time)
{
    internal const string IdempotencyIndex = "ix_bookings_user_id_idempotency_key";
    internal const string ReferenceIndex = "ix_bookings_reference";
    internal const string ActiveSeatIndex = "ux_seat_reservations_active_seat";

    private const int MaxReferenceAttempts = 3;

    public async Task<CreateBookingResult> CreateAsync(CreateBookingRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var requestHash = ComputeRequestHash(request);

        if (await FindReplayAsync(userId, idempotencyKey, requestHash, cancellationToken) is { } replay)
        {
            return new CreateBookingResult(replay, Created: false);
        }

        FlightSelectionRequest[] selections = request.Return is { } inbound ? [request.Outbound, inbound] : [request.Outbound];
        var flightIds = selections.Select(s => s.FlightId).ToList();
        await expiry.ExpireDueAsync(flightIds, BookingExpiryService.LazyBatchSize, cancellationToken);

        for (var attempt = 1; ; attempt++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var flights = await db.Flights.Include(f => f.Fares)
                    .Where(f => flightIds.Contains(f.Id))
                    .ToDictionaryAsync(f => f.Id, cancellationToken);

                var segments = selections.Select((s, i) => new SegmentSelection(
                    flights.GetValueOrDefault(s.FlightId) ?? throw new NotFoundException("Flight", s.FlightId),
                    i == 0 ? JourneyDirection.Outbound : JourneyDirection.Return,
                    s.FareFamily)).ToList();
                var passengers = request.Passengers.Select(p => new PassengerDetails(p.FirstName, p.LastName, p.Type)).ToList();

                var booking = Booking.Create(userId, BookingReference.Generate(), segments, passengers,
                    idempotencyKey, requestHash, time.GetUtcNow());

                await FlightCapacity.ReserveAsync(db, booking, cancellationToken);
                db.Bookings.Add(booking);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                db.ChangeTracker.Clear();
                return new CreateBookingResult(await GetAsync(booking.Reference, cancellationToken), Created: true);
            }
            catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == IdempotencyIndex)
            {
                // A concurrent request with the same key won the race; answer as a replay of its booking.
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                var winner = await FindReplayAsync(userId, idempotencyKey, requestHash, cancellationToken)
                             ?? throw new InvalidOperationException("Idempotency key conflict without a stored booking.");
                return new CreateBookingResult(winner, Created: false);
            }
            catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == ReferenceIndex && attempt < MaxReferenceAttempts)
            {
                // Reference collision (about 1 in 900 million per pair): try again with a new one.
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
            }
        }
    }

    public async Task<BookingDto> GetAsync(string reference, CancellationToken cancellationToken)
    {
        var booking = await LoadAsync(reference, tracked: false, cancellationToken);
        return ToDto(booking, time.GetUtcNow());
    }

    public async Task<PagedResult<BookingSummaryDto>> ListAsync(PageRequest page, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var now = time.GetUtcNow();
        var mine = db.Bookings.AsNoTracking().Where(b => b.UserId == userId);
        var paymentStartedAfter = now - Booking.PaymentTimeout;

        var total = await mine.CountAsync(cancellationToken);
        var rows = await mine
            .OrderByDescending(b => b.CreatedAt).ThenBy(b => b.Id)
            .Skip(page.Skip).Take(page.Size)
            .Select(b => new
            {
                b.Id,
                b.Reference,
                b.Status,
                b.CreatedAt,
                b.ExpiresAt,
                PaymentInFlight = b.Payments.Any(p => p.Status == PaymentStatus.Pending && p.CreatedAt > paymentStartedAfter),
                b.TotalPrice.Amount,
                b.TotalPrice.Currency,
                PassengerCount = b.Passengers.Count,
                Segments = b.Segments.OrderBy(s => s.Direction)
                    .Select(s => new { s.Flight.DepartureUtc, s.Flight.Origin, s.Flight.Destination })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r =>
        {
            var outbound = r.Segments[0];
            var inbound = r.Segments.Count > 1 ? r.Segments[1] : null;
            var status = EffectiveStatus(r.Status, r.ExpiresAt, r.PaymentInFlight, now);
            var lastDeparture = inbound?.DepartureUtc ?? outbound.DepartureUtc;
            return new BookingSummaryDto(
                r.Id,
                r.Reference,
                status,
                r.CreatedAt,
                new MoneyDto(r.Amount, r.Currency),
                r.PassengerCount,
                AirportDto.From(outbound.Origin),
                AirportDto.From(outbound.Destination),
                TimeZoneConversion.ToLocal(outbound.DepartureUtc, outbound.Origin.TimeZone),
                outbound.DepartureUtc,
                inbound is null ? null : TimeZoneConversion.ToLocal(inbound.DepartureUtc, inbound.Origin.TimeZone),
                status is BookingStatus.Confirmed or BookingStatus.PendingPayment && lastDeparture > now);
        }).ToList();

        return new PagedResult<BookingSummaryDto>(items, Math.Max(page.Page, 1), page.Size, total);
    }

    public async Task<SeatMapDto> GetSeatMapAsync(string reference, Guid segmentId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var booking = await LoadAsync(reference, tracked: false, cancellationToken);
        var segment = FindSegment(booking, segmentId);
        var aircraft = segment.Flight.AircraftType;

        var seats = await db.AircraftSeats.AsNoTracking()
            .Where(s => s.AircraftTypeId == aircraft.Id)
            .OrderBy(s => s.Row).ThenBy(s => s.Letter)
            .ToListAsync(cancellationToken);

        // Seats held by other bookings whose hold has lapsed count as free; they are released lazily on selection.
        var occupied = (await db.Bookings.AsNoTracking()
            .Where(b => b.Id != booking.Id && (b.Status != BookingStatus.PendingPayment || b.ExpiresAt > now))
            .SelectMany(b => b.Segments)
            .Where(s => s.FlightId == segment.FlightId)
            .SelectMany(s => s.SeatReservations)
            .Where(r => r.Status != SeatReservationStatus.Released)
            .Select(r => r.AircraftSeatId)
            .ToListAsync(cancellationToken)).ToHashSet();

        var mine = segment.ActiveSeatReservations.ToDictionary(r => r.AircraftSeatId, r => r.BookingPassengerId);
        var currency = segment.FarePrice.Currency;

        return new SeatMapDto(
            segment.Id,
            segment.Flight.FlightNumber,
            aircraft.Name,
            aircraft.Rows,
            aircraft.SeatLetters,
            seats.Select(s =>
            {
                var availability = mine.ContainsKey(s.Id) ? SeatAvailability.Selected
                    : occupied.Contains(s.Id) ? SeatAvailability.Occupied
                    : SeatAvailability.Available;
                return new SeatMapSeatDto(s.SeatNumber, s.Row, s.Letter, s.Category,
                    MoneyDto.From(SeatPricing.PriceFor(s.Category, segment.FareConditions, currency)),
                    availability, mine.TryGetValue(s.Id, out var passengerId) ? passengerId : null);
            }).ToList());
    }

    public async Task<BookingDto> SelectSeatsAsync(string reference, Guid segmentId, SelectSeatsRequest request,
        CancellationToken cancellationToken)
    {
        var flightId = await OwnBookings(reference)
            .SelectMany(b => b.Segments)
            .Where(s => s.Id == segmentId)
            .Select(s => (Guid?)s.FlightId)
            .SingleOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("Booking segment", segmentId);

        // Frees seats whose hold lapsed on this flight, including this booking's own if it has run out of time.
        await expiry.ExpireDueAsync([flightId], BookingExpiryService.LazyBatchSize, cancellationToken);

        var now = time.GetUtcNow();
        var booking = await LoadAsync(reference, tracked: true, cancellationToken);
        if (booking.Status == BookingStatus.Expired)
        {
            throw new ConflictException("booking_expired", "The booking hold has expired. Please search again.");
        }

        var segment = FindSegment(booking, segmentId);
        var passengers = booking.Passengers.ToDictionary(p => p.Id);
        var unknownPassenger = request.Seats.FirstOrDefault(s => !passengers.ContainsKey(s.PassengerId));
        if (unknownPassenger is not null)
        {
            throw new UnprocessableException("passenger_not_in_booking", $"Passenger '{unknownPassenger.PassengerId}' is not part of this booking.");
        }

        var seatNumbers = request.Seats.Select(s => s.SeatNumber.ToUpperInvariant()).ToList();
        var seats = await db.AircraftSeats
            .Where(s => s.AircraftTypeId == segment.Flight.AircraftTypeId && seatNumbers.Contains(s.SeatNumber))
            .ToDictionaryAsync(s => s.SeatNumber, cancellationToken);
        var unknownSeat = seatNumbers.FirstOrDefault(n => !seats.ContainsKey(n));
        if (unknownSeat is not null)
        {
            throw new UnprocessableException("seat_not_found", $"Seat {unknownSeat} does not exist on this aircraft.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Release first and persist, so swapping seats between passengers never trips the unique seat index.
            booking.ReleaseSeats(segment.Id, request.Seats.Select(s => s.PassengerId).ToList(), now);
            await db.SaveChangesAsync(cancellationToken);

            booking.HoldSeats(segment.Id,
                request.Seats.Select(s => new SeatSelection(passengers[s.PassengerId], seats[s.SeatNumber.ToUpperInvariant()])).ToList(),
                now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == ActiveSeatIndex)
        {
            throw new ConflictException("seat_unavailable",
                "One or more of the selected seats were just taken by another traveller. Please choose again.");
        }

        db.ChangeTracker.Clear();
        return await GetAsync(reference, cancellationToken);
    }

    public async Task<CancellationQuoteDto> GetCancellationQuoteAsync(string reference, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var booking = await LoadAsync(reference, tracked: false, cancellationToken);
        if (booking.IsHoldExpired(now))
        {
            return new CancellationQuoteDto(false, null, "booking_expired", "The booking hold has already expired.");
        }

        try
        {
            return new CancellationQuoteDto(true, MoneyDto.From(booking.CalculateRefund(BookingActor.Customer, now)), null, null);
        }
        catch (DomainException ex)
        {
            return new CancellationQuoteDto(false, null, ex.Code, ex.Message);
        }
    }

    public async Task<BookingDto> CancelAsync(string reference, CancellationToken cancellationToken)
    {
        var flightIds = await OwnBookings(reference)
            .SelectMany(b => b.Segments.Select(s => s.FlightId))
            .ToListAsync(cancellationToken);
        if (flightIds.Count == 0)
        {
            throw new NotFoundException("Booking", reference);
        }

        await expiry.ExpireDueAsync(flightIds, BookingExpiryService.LazyBatchSize, cancellationToken);

        var booking = await LoadAsync(reference, tracked: true, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        booking.Cancel(BookingActor.Customer, "Cancelled by customer", time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await FlightCapacity.ReleaseAsync(db, booking, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        db.ChangeTracker.Clear();
        return await GetAsync(reference, cancellationToken);
    }

    /// <summary>
    /// SHA-256 of the request as received. The same key with a different body is a client bug and is rejected,
    /// rather than silently returning a booking that does not match what was asked for.
    /// </summary>
    internal static string ComputeRequestHash(CreateBookingRequest request) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));

    /// <summary>
    /// A lapsed hold reads as expired immediately, even before the expiry job has processed it, unless a payment
    /// that started in time is still running (mirrors <see cref="Booking.IsHoldExpired"/> for projections).
    /// </summary>
    internal static BookingStatus EffectiveStatus(BookingStatus status, DateTimeOffset expiresAt, bool paymentInFlight, DateTimeOffset now) =>
        status == BookingStatus.PendingPayment && now >= expiresAt && !paymentInFlight ? BookingStatus.Expired : status;

    private async Task<BookingDto?> FindReplayAsync(Guid userId, string idempotencyKey, string requestHash,
        CancellationToken cancellationToken)
    {
        var existing = await db.Bookings.AsNoTracking()
            .Where(b => b.UserId == userId && b.IdempotencyKey == idempotencyKey)
            .Select(b => new { b.Reference, b.RequestHash })
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            return null;
        }

        if (existing.RequestHash != requestHash)
        {
            throw new UnprocessableException("idempotency_key_reused",
                "This Idempotency-Key was already used for a different booking request. Use a new key for a new booking.");
        }

        return await GetAsync(existing.Reference, cancellationToken);
    }

    private IQueryable<Booking> OwnBookings(string reference)
    {
        var userId = currentUser.UserId;
        var normalized = reference.Trim().ToUpperInvariant();
        return db.Bookings.Where(b => b.UserId == userId && b.Reference == normalized);
    }

    private async Task<Booking> LoadAsync(string reference, bool tracked, CancellationToken cancellationToken)
    {
        if (!BookingReference.IsWellFormed(reference.Trim().ToUpperInvariant()))
        {
            throw new NotFoundException("Booking", reference);
        }

        var query = OwnBookings(reference)
            .Include(b => b.Passengers)
            .Include(b => b.Segments).ThenInclude(s => s.SeatReservations)
            .Include(b => b.Segments).ThenInclude(s => s.Flight).ThenInclude(f => f.Airline)
            .Include(b => b.Segments).ThenInclude(s => s.Flight).ThenInclude(f => f.Origin)
            .Include(b => b.Segments).ThenInclude(s => s.Flight).ThenInclude(f => f.Destination)
            .Include(b => b.Segments).ThenInclude(s => s.Flight).ThenInclude(f => f.AircraftType)
            .Include(b => b.Payments)
            .AsSplitQuery();

        return await (tracked ? query : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException("Booking", reference);
    }

    private static BookingSegment FindSegment(Booking booking, Guid segmentId) =>
        booking.Segments.SingleOrDefault(s => s.Id == segmentId) ?? throw new NotFoundException("Booking segment", segmentId);

    internal static BookingDto ToDto(Booking booking, DateTimeOffset now)
    {
        var status = booking.IsHoldExpired(now) ? BookingStatus.Expired : booking.Status;
        return new BookingDto(
            booking.Id,
            booking.Reference,
            status,
            booking.CreatedAt,
            status == BookingStatus.PendingPayment ? booking.ExpiresAt : null,
            booking.ConfirmedAt,
            booking.CancelledAt,
            MoneyDto.From(booking.TotalPrice),
            MoneyDto.From(booking.RefundedAmount),
            booking.Passengers.OrderBy(p => p.Sequence)
                .Select(p => new PassengerDto(p.Id, p.Sequence, p.FirstName, p.LastName, p.Type))
                .ToList(),
            booking.Segments.OrderBy(s => s.Direction).Select(ToDto).ToList(),
            DataSources.Demo);
    }

    private static BookingSegmentDto ToDto(BookingSegment segment)
    {
        var flight = segment.Flight;
        return new BookingSegmentDto(
            segment.Id,
            segment.Direction,
            new SegmentFlightDto(
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
                flight.Status),
            segment.FareFamily,
            MoneyDto.From(segment.FarePrice),
            FareConditionsDto.From(segment.FareConditions),
            segment.ActiveSeatReservations
                .Select(r => new SeatAssignmentDto(r.BookingPassengerId, r.SeatNumber, MoneyDto.From(r.Price)))
                .OrderBy(r => r.SeatNumber.Length).ThenBy(r => r.SeatNumber, StringComparer.Ordinal)
                .ToList());
    }
}
