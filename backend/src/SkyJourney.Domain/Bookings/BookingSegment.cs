using SkyJourney.Domain.Common;
using SkyJourney.Domain.Flights;

namespace SkyJourney.Domain.Bookings;

/// <summary>One flight within a booking, with a snapshot of the fare as it was sold.</summary>
public sealed class BookingSegment : Entity
{
    private readonly List<SeatReservation> _seatReservations = [];

    private BookingSegment()
    {
    }

    internal BookingSegment(Guid bookingId, Flight flight, JourneyDirection direction, Fare fare)
    {
        BookingId = bookingId;
        FlightId = flight.Id;
        Flight = flight;
        Direction = direction;
        FareFamily = fare.Family;
        FarePrice = fare.Price;
        FareConditions = fare.Conditions;
    }

    public Guid BookingId { get; private set; }

    public Guid FlightId { get; private set; }

    public Flight Flight { get; private set; } = null!;

    public JourneyDirection Direction { get; private set; }

    public FareFamily FareFamily { get; private set; }

    /// <summary>Price per passenger agreed at booking time.</summary>
    public Money FarePrice { get; private set; } = null!;

    public FareConditions FareConditions { get; private set; } = null!;

    public IReadOnlyCollection<SeatReservation> SeatReservations => _seatReservations;

    public IEnumerable<SeatReservation> ActiveSeatReservations =>
        _seatReservations.Where(r => r.Status != SeatReservationStatus.Released);

    internal void AddSeatReservation(SeatReservation reservation) => _seatReservations.Add(reservation);
}
