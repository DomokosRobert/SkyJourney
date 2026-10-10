using Microsoft.EntityFrameworkCore;
using SkyJourney.Application.Common;
using SkyJourney.Domain.Bookings;
using SkyJourney.Domain.Flights;

namespace SkyJourney.Application.Bookings;

/// <summary>
/// The only code that changes <see cref="Flight.SeatsSold"/>. Every change is a single conditional SQL update, so
/// concurrent bookings can never push a flight past its capacity (see docs/architecture.md, "Concurrency").
/// Call these inside the transaction that also writes the booking.
/// </summary>
internal static class FlightCapacity
{
    /// <summary>Reserves places on each flight, or throws <c>409 insufficient_capacity</c>.</summary>
    public static async Task ReserveAsync(ISkyJourneyDbContext db, Booking booking, CancellationToken cancellationToken)
    {
        var seats = booking.PassengerCount;
        // A fixed lock order (by flight id) keeps two round-trip bookings on the same flights from deadlocking.
        foreach (var segment in booking.Segments.OrderBy(s => s.FlightId))
        {
            var updated = await db.Flights
                .Where(f => f.Id == segment.FlightId && f.Status == FlightStatus.Scheduled && f.SeatsSold + seats <= f.Capacity)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.SeatsSold, f => f.SeatsSold + seats), cancellationToken);

            if (updated == 0)
            {
                throw new ConflictException("insufficient_capacity",
                    $"Flight {segment.Flight.FlightNumber} no longer has {seats} seat(s) available.");
            }
        }
    }

    /// <summary>Gives back the places of a booking that was cancelled or expired.</summary>
    public static async Task ReleaseAsync(ISkyJourneyDbContext db, Booking booking, CancellationToken cancellationToken)
    {
        var seats = booking.PassengerCount;
        foreach (var flightId in booking.Segments.Select(s => s.FlightId).Order())
        {
            await db.Flights
                .Where(f => f.Id == flightId)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.SeatsSold, f => f.SeatsSold - seats), cancellationToken);
        }
    }
}
