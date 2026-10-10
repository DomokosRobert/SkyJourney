using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SkyJourney.Application.Common;
using SkyJourney.Domain.Airports;
using SkyJourney.Domain.Bookings;
using SkyJourney.Domain.Fleet;
using SkyJourney.Domain.Flights;
using SkyJourney.Infrastructure.Identity;

namespace SkyJourney.Infrastructure.Persistence;

public sealed class SkyJourneyDbContext(DbContextOptions<SkyJourneyDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), ISkyJourneyDbContext
{
    public DbSet<Airport> Airports => Set<Airport>();

    public DbSet<Airline> Airlines => Set<Airline>();

    public DbSet<AircraftType> AircraftTypes => Set<AircraftType>();

    public DbSet<AircraftSeat> AircraftSeats => Set<AircraftSeat>();

    public DbSet<FlightSchedule> FlightSchedules => Set<FlightSchedule>();

    public DbSet<Flight> Flights => Set<Flight>();

    public DbSet<Fare> Fares => Set<Fare>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<SeatReservation> SeatReservations => Set<SeatReservation>();

    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("users");
            b.Property(u => u.FirstName).HasMaxLength(50);
            b.Property(u => u.LastName).HasMaxLength(50);
        });
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");

        builder.ApplyConfigurationsFromAssembly(typeof(SkyJourneyDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(10, 2);
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }
}
