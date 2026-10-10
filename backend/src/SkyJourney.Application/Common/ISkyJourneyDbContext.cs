using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SkyJourney.Domain.Airports;
using SkyJourney.Domain.Bookings;
using SkyJourney.Domain.Fleet;
using SkyJourney.Domain.Flights;

namespace SkyJourney.Application.Common;

/// <summary>Application-facing view of the database (see ADR-0004).</summary>
public interface ISkyJourneyDbContext
{
    DbSet<Airport> Airports { get; }

    DbSet<Airline> Airlines { get; }

    DbSet<AircraftType> AircraftTypes { get; }

    DbSet<AircraftSeat> AircraftSeats { get; }

    DbSet<FlightSchedule> FlightSchedules { get; }

    DbSet<Flight> Flights { get; }

    DbSet<Fare> Fares { get; }

    DbSet<Booking> Bookings { get; }

    DbSet<SeatReservation> SeatReservations { get; }

    DbSet<PaymentAttempt> PaymentAttempts { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
