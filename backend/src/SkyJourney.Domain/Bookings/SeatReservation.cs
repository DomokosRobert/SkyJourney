using SkyJourney.Domain.Common;
using SkyJourney.Domain.Fleet;

namespace SkyJourney.Domain.Bookings;

/// <summary>
/// A passenger's seat on one segment. At most one Held/Confirmed reservation may exist per
/// (flight, seat): enforced by a partial unique index in the database.
/// </summary>
public sealed class SeatReservation : Entity
{
    private SeatReservation()
    {
    }

    internal SeatReservation(BookingSegment segment, BookingPassenger passenger, AircraftSeat seat, Money price, DateTimeOffset now)
    {
        BookingSegmentId = segment.Id;
        BookingPassengerId = passenger.Id;
        FlightId = segment.FlightId;
        AircraftSeatId = seat.Id;
        SeatNumber = seat.SeatNumber;
        Price = price;
        Status = SeatReservationStatus.Held;
        CreatedAt = now;
    }

    public Guid BookingSegmentId { get; private set; }

    public Guid BookingPassengerId { get; private set; }

    /// <summary>Denormalised from the segment so the uniqueness rule can be a single index.</summary>
    public Guid FlightId { get; private set; }

    public Guid AircraftSeatId { get; private set; }

    public string SeatNumber { get; private set; } = null!;

    public Money Price { get; private set; } = null!;

    public SeatReservationStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal void Confirm()
    {
        if (Status == SeatReservationStatus.Held)
        {
            Status = SeatReservationStatus.Confirmed;
        }
    }

    internal void Release() => Status = SeatReservationStatus.Released;
}
