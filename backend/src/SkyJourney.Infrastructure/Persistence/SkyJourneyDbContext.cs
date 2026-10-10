using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SkyJourney.Application.Common;
using SkyJourney.Domain.Airports;
using SkyJourney.Domain.Bookings;
using SkyJourney.Domain.Common;
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

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new UniqueConstraintViolationException(pg.ConstraintName, ex);
        }
    }

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

        // Domain entities create their own UUIDv7 ids. Declaring the keys as never generated lets EF Core insert
        // children added to an already-tracked aggregate (a seat hold, a payment attempt, a history row); with the
        // default "generated" Guid convention it would assume they already exist and issue an UPDATE instead.
        foreach (var entityType in builder.Model.GetEntityTypes().Where(t => typeof(Entity).IsAssignableFrom(t.ClrType)))
        {
            builder.Entity(entityType.ClrType).Property(nameof(Entity.Id)).ValueGeneratedNever();
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(10, 2);
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }
}
