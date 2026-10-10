using SkyJourney.Domain.Common;
using SkyJourney.Domain.Fleet;
using SkyJourney.Domain.Flights;

namespace SkyJourney.Domain.Bookings;

public sealed record PassengerDetails(string FirstName, string LastName, PassengerType Type);

public sealed record SegmentSelection(Flight Flight, JourneyDirection Direction, FareFamily FareFamily);

public sealed record SeatSelection(BookingPassenger Passenger, AircraftSeat Seat);

/// <summary>
/// Aggregate root for a reservation. All status changes go through <see cref="TransitionTo"/>,
/// which enforces the state machine and appends to the audit history.
/// </summary>
public sealed class Booking : Entity
{
    public const int MaxPassengers = 9;

    /// <summary>How long capacity and seats are held while the customer pays.</summary>
    public static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(20);

    /// <summary>Customers may cancel a confirmed booking until this long before the first departure.</summary>
    public static readonly TimeSpan CancellationCutoff = TimeSpan.FromHours(24);

    /// <summary>Minimum time between outbound arrival and return departure.</summary>
    public static readonly TimeSpan MinimumStay = TimeSpan.FromHours(2);

    private static readonly Dictionary<BookingStatus, BookingStatus[]> AllowedTransitions = new()
    {
        [BookingStatus.PendingPayment] = [BookingStatus.Confirmed, BookingStatus.Expired, BookingStatus.Cancelled],
        [BookingStatus.Confirmed] = [BookingStatus.Cancelled],
        [BookingStatus.Cancelled] = [],
        [BookingStatus.Expired] = [],
    };

    private readonly List<BookingPassenger> _passengers = [];
    private readonly List<BookingSegment> _segments = [];
    private readonly List<PaymentAttempt> _payments = [];
    private readonly List<BookingStatusHistory> _history = [];

    private Booking()
    {
    }

    public string Reference { get; private set; } = null!;

    public Guid UserId { get; private set; }

    public BookingStatus Status { get; private set; }

    public Money TotalPrice { get; private set; } = null!;

    public Money RefundedAmount { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When an unpaid booking's hold lapses.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string IdempotencyKey { get; private set; } = null!;

    /// <summary>Hash of the creation request; detects an idempotency key reused for a different request.</summary>
    public string RequestHash { get; private set; } = null!;

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; private set; }

    public IReadOnlyCollection<BookingPassenger> Passengers => _passengers;

    public IReadOnlyCollection<BookingSegment> Segments => _segments;

    public IReadOnlyCollection<PaymentAttempt> Payments => _payments;

    public IReadOnlyCollection<BookingStatusHistory> History => _history;

    public int PassengerCount => _passengers.Count;

    public DateTimeOffset FirstDepartureUtc => _segments.Min(s => s.Flight.DepartureUtc);

    public bool IsHoldExpired(DateTimeOffset now) => Status == BookingStatus.PendingPayment && now >= ExpiresAt;

    public static Booking Create(Guid userId, string reference, IReadOnlyList<SegmentSelection> segments,
        IReadOnlyList<PassengerDetails> passengers, string idempotencyKey, string requestHash, DateTimeOffset now)
    {
        ValidatePassengers(passengers);
        ValidateItinerary(segments, passengers.Count, now);

        var booking = new Booking
        {
            Reference = reference,
            UserId = userId,
            CreatedAt = now,
            ExpiresAt = now + HoldDuration,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
        };

        for (var i = 0; i < passengers.Count; i++)
        {
            var p = passengers[i];
            booking._passengers.Add(new BookingPassenger(booking.Id, i + 1, p.FirstName, p.LastName, p.Type));
        }

        foreach (var selection in segments.OrderBy(s => s.Direction))
        {
            var fare = selection.Flight.GetFare(selection.FareFamily);
            booking._segments.Add(new BookingSegment(booking.Id, selection.Flight, selection.Direction, fare));
        }

        booking.RefundedAmount = Money.Zero(booking._segments[0].FarePrice.Currency);
        booking.RecalculateTotal();
        booking.TransitionTo(BookingStatus.PendingPayment, BookingActor.Customer, "Booking created", now);
        return booking;
    }

    /// <summary>
    /// Releases the current seats of the given passengers on a segment. Persist this before calling
    /// <see cref="HoldSeats"/> so a swap between two passengers never trips the unique seat index.
    /// </summary>
    public void ReleaseSeats(Guid segmentId, IReadOnlyCollection<Guid> passengerIds, DateTimeOffset now)
    {
        EnsureSeatsChangeable(now);
        var segment = GetSegment(segmentId);
        foreach (var reservation in segment.ActiveSeatReservations.Where(r => passengerIds.Contains(r.BookingPassengerId)))
        {
            reservation.Release();
        }

        RecalculateTotal();
    }

    public void HoldSeats(Guid segmentId, IReadOnlyCollection<SeatSelection> selections, DateTimeOffset now)
    {
        EnsureSeatsChangeable(now);
        var segment = GetSegment(segmentId);

        if (selections.Select(s => s.Passenger.Id).Distinct().Count() != selections.Count ||
            selections.Select(s => s.Seat.Id).Distinct().Count() != selections.Count)
        {
            throw new DomainException("duplicate_seat_selection", "Each passenger and each seat may appear only once.");
        }

        foreach (var (passenger, seat) in selections)
        {
            if (!_passengers.Contains(passenger))
            {
                throw new DomainException("passenger_not_in_booking", "The passenger does not belong to this booking.");
            }

            if (seat.AircraftTypeId != segment.Flight.AircraftTypeId)
            {
                throw new DomainException("seat_not_on_aircraft", $"Seat {seat.SeatNumber} does not exist on this aircraft.");
            }

            if (segment.ActiveSeatReservations.Any(r => r.BookingPassengerId == passenger.Id))
            {
                throw new DomainException("passenger_already_seated", "Release the passenger's current seat first.");
            }

            var price = SeatPricing.PriceFor(seat.Category, segment.FareConditions, segment.FarePrice.Currency);
            segment.AddSeatReservation(new SeatReservation(segment, passenger, seat, price, now));
        }

        RecalculateTotal();
    }

    public PaymentAttempt StartPayment(string idempotencyKey, string provider, DateTimeOffset now)
    {
        if (Status != BookingStatus.PendingPayment)
        {
            throw new DomainException("booking_not_payable", $"A {Status} booking cannot be paid.");
        }

        if (IsHoldExpired(now))
        {
            throw new DomainException("booking_expired", "The booking hold has expired. Please search again.");
        }

        if (_payments.Any(p => p.Status == PaymentStatus.Pending))
        {
            throw new DomainException("payment_in_progress", "Another payment for this booking is in progress.");
        }

        var attempt = new PaymentAttempt(Id, TotalPrice, idempotencyKey, provider, now);
        _payments.Add(attempt);
        return attempt;
    }

    public void CompletePayment(PaymentAttempt attempt, bool succeeded, string? providerReference, string? failureCode, DateTimeOffset now)
    {
        if (!_payments.Contains(attempt))
        {
            throw new DomainException("payment_not_in_booking", "The payment does not belong to this booking.");
        }

        if (!succeeded)
        {
            attempt.MarkFailed(failureCode ?? "payment_failed", providerReference, now);
            return;
        }

        attempt.MarkSucceeded(providerReference ?? throw new ArgumentNullException(nameof(providerReference)), now);
        foreach (var reservation in _segments.SelectMany(s => s.ActiveSeatReservations))
        {
            reservation.Confirm();
        }

        ConfirmedAt = now;
        TransitionTo(BookingStatus.Confirmed, BookingActor.System, "Payment succeeded", now);
    }

    public void Expire(DateTimeOffset now)
    {
        if (!IsHoldExpired(now))
        {
            throw new DomainException("booking_not_expirable", "Only unpaid bookings past their hold time can expire.");
        }

        ReleaseAllSeats();
        TransitionTo(BookingStatus.Expired, BookingActor.System, "Payment hold expired", now);
    }

    /// <summary>The refund a cancellation would produce right now, or a reason why it is not allowed.</summary>
    public Money CalculateRefund(BookingActor actor, DateTimeOffset now)
    {
        var currency = TotalPrice.Currency;
        switch (Status)
        {
            case BookingStatus.PendingPayment:
                return Money.Zero(currency);
            case BookingStatus.Confirmed when actor == BookingActor.Administrator:
                return TotalPrice;
            case BookingStatus.Confirmed when FirstDepartureUtc - now < CancellationCutoff:
                throw new DomainException("cancellation_window_closed",
                    $"Bookings can be cancelled until {CancellationCutoff.TotalHours:0} hours before departure.");
            case BookingStatus.Confirmed:
                var refund = Money.Zero(currency);
                foreach (var segment in _segments)
                {
                    var percentage = segment.FareConditions.RefundPercentage;
                    refund = refund.Add(segment.FarePrice.Multiply(PassengerCount).Percent(percentage));
                    if (percentage == 100)
                    {
                        foreach (var seat in segment.ActiveSeatReservations)
                        {
                            refund = refund.Add(seat.Price);
                        }
                    }
                }

                return refund;
            default:
                throw new DomainException("booking_not_cancellable", $"A {Status} booking cannot be cancelled.");
        }
    }

    public Money Cancel(BookingActor actor, string reason, DateTimeOffset now)
    {
        var refund = CalculateRefund(actor, now);

        if (Status == BookingStatus.Confirmed && refund.Amount > 0)
        {
            var payment = _payments.Single(p => p.Status == PaymentStatus.Succeeded);
            payment.Refund(refund);
        }

        RefundedAmount = refund;
        CancelledAt = now;
        ReleaseAllSeats();
        TransitionTo(BookingStatus.Cancelled, actor, reason, now);
        return refund;
    }

    private void TransitionTo(BookingStatus target, BookingActor actor, string reason, DateTimeOffset now)
    {
        // Booking.Create starts from the implicit "new" state.
        var isCreation = _history.Count == 0 && target == BookingStatus.PendingPayment;
        if (!isCreation && !AllowedTransitions[Status].Contains(target))
        {
            throw new DomainException("invalid_status_transition", $"Cannot change a booking from {Status} to {target}.");
        }

        _history.Add(new BookingStatusHistory(Id, isCreation ? null : Status, target, actor, reason, now));
        Status = target;
    }

    private void RecalculateTotal()
    {
        var total = Money.Zero(_segments[0].FarePrice.Currency);
        foreach (var segment in _segments)
        {
            total = total.Add(segment.FarePrice.Multiply(PassengerCount));
            foreach (var seat in segment.ActiveSeatReservations)
            {
                total = total.Add(seat.Price);
            }
        }

        TotalPrice = total;
    }

    private void ReleaseAllSeats()
    {
        foreach (var reservation in _segments.SelectMany(s => s.ActiveSeatReservations).ToList())
        {
            reservation.Release();
        }
    }

    private BookingSegment GetSegment(Guid segmentId) =>
        _segments.SingleOrDefault(s => s.Id == segmentId)
        ?? throw new DomainException("segment_not_found", "The segment does not belong to this booking.");

    private void EnsureSeatsChangeable(DateTimeOffset now)
    {
        if (Status != BookingStatus.PendingPayment)
        {
            throw new DomainException("seats_locked", "Seats can only be changed before payment.");
        }

        if (IsHoldExpired(now))
        {
            throw new DomainException("booking_expired", "The booking hold has expired. Please search again.");
        }
    }

    private static void ValidatePassengers(IReadOnlyList<PassengerDetails> passengers)
    {
        if (passengers.Count is 0 or > MaxPassengers)
        {
            throw new DomainException("invalid_passenger_count", $"A booking needs 1-{MaxPassengers} passengers.");
        }

        if (passengers.All(p => p.Type != PassengerType.Adult))
        {
            throw new DomainException("adult_required", "Every booking needs at least one adult.");
        }
    }

    private static void ValidateItinerary(IReadOnlyList<SegmentSelection> segments, int passengerCount, DateTimeOffset now)
    {
        var outbound = segments.Where(s => s.Direction == JourneyDirection.Outbound).ToList();
        var inbound = segments.Where(s => s.Direction == JourneyDirection.Return).ToList();
        if (outbound.Count != 1 || inbound.Count > 1)
        {
            throw new DomainException("invalid_itinerary", "A booking has exactly one outbound and at most one return flight.");
        }

        foreach (var (flight, _, family) in segments)
        {
            if (!flight.IsOpenForSale(now))
            {
                throw new DomainException("flight_not_bookable", $"Flight {flight.FlightNumber} is no longer open for booking.");
            }

            if (!flight.GetFare(family).IsAvailable)
            {
                throw new DomainException("fare_unavailable", $"The {family} fare is not available on {flight.FlightNumber}.");
            }

            if (flight.SeatsAvailable < passengerCount)
            {
                throw new DomainException("insufficient_capacity", $"Flight {flight.FlightNumber} does not have enough seats left.");
            }
        }

        if (inbound.Count == 1)
        {
            var (there, back) = (outbound[0].Flight, inbound[0].Flight);
            if (back.OriginAirportId != there.DestinationAirportId || back.DestinationAirportId != there.OriginAirportId)
            {
                throw new DomainException("invalid_itinerary", "The return flight must fly the outbound route in reverse.");
            }

            if (back.DepartureUtc - there.ArrivalUtc < MinimumStay)
            {
                throw new DomainException("invalid_itinerary", "The return flight must depart at least 2 hours after the outbound arrives.");
            }
        }

        if (segments.Select(s => s.Flight.GetFare(s.FareFamily).Price.Currency).Distinct().Count() > 1)
        {
            throw new DomainException("currency_mismatch", "All flights in a booking must be priced in the same currency.");
        }
    }
}
