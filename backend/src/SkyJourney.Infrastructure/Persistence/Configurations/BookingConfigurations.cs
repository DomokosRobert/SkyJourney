using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyJourney.Domain.Bookings;
using SkyJourney.Domain.Fleet;
using SkyJourney.Domain.Flights;
using SkyJourney.Infrastructure.Identity;

namespace SkyJourney.Infrastructure.Persistence.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.Property(b => b.Reference).HasMaxLength(BookingReference.Length).IsFixedLength();
        builder.HasIndex(b => b.Reference).IsUnique();

        builder.Property(b => b.IdempotencyKey).HasMaxLength(64);
        builder.Property(b => b.RequestHash).HasMaxLength(64);
        builder.HasIndex(b => new { b.UserId, b.IdempotencyKey }).IsUnique();
        builder.HasIndex(b => new { b.UserId, b.CreatedAt });
        builder.HasIndex(b => new { b.Status, b.ExpiresAt });

        builder.ComplexProperty(b => b.TotalPrice, m => m.ConfigureMoney());
        builder.ComplexProperty(b => b.RefundedAmount, m => m.ConfigureMoney());

        // PostgreSQL system column used as an optimistic concurrency token.
        builder.Property(b => b.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(b => b.Passengers).WithOne().HasForeignKey(p => p.BookingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(b => b.Segments).WithOne().HasForeignKey(s => s.BookingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(b => b.Payments).WithOne().HasForeignKey(p => p.BookingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(b => b.History).WithOne().HasForeignKey(h => h.BookingId).OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(b => b.PassengerCount);
        builder.Ignore(b => b.FirstDepartureUtc);
    }
}

internal sealed class BookingPassengerConfiguration : IEntityTypeConfiguration<BookingPassenger>
{
    public void Configure(EntityTypeBuilder<BookingPassenger> builder)
    {
        builder.ToTable("booking_passengers");
        builder.Property(p => p.FirstName).HasMaxLength(BookingPassenger.MaxNameLength);
        builder.Property(p => p.LastName).HasMaxLength(BookingPassenger.MaxNameLength);
        builder.HasIndex(p => new { p.BookingId, p.Sequence }).IsUnique();
    }
}

internal sealed class BookingSegmentConfiguration : IEntityTypeConfiguration<BookingSegment>
{
    public void Configure(EntityTypeBuilder<BookingSegment> builder)
    {
        builder.ToTable("booking_segments");
        builder.ComplexProperty(s => s.FarePrice, m => m.ConfigureMoney());
        builder.ComplexProperty(s => s.FareConditions, c => c.ConfigureConditions());
        builder.HasOne(s => s.Flight).WithMany().HasForeignKey(s => s.FlightId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(s => s.SeatReservations).WithOne().HasForeignKey(r => r.BookingSegmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => new { s.BookingId, s.Direction }).IsUnique();
        builder.HasIndex(s => s.FlightId);
        builder.Ignore(s => s.ActiveSeatReservations);
    }
}

internal sealed class SeatReservationConfiguration : IEntityTypeConfiguration<SeatReservation>
{
    private const string ActiveFilter = "status IN ('Held', 'Confirmed')";

    public void Configure(EntityTypeBuilder<SeatReservation> builder)
    {
        builder.Property(r => r.SeatNumber).HasMaxLength(4);
        builder.ComplexProperty(r => r.Price, m => m.ConfigureMoney());

        builder.HasOne<Flight>().WithMany().HasForeignKey(r => r.FlightId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AircraftSeat>().WithMany().HasForeignKey(r => r.AircraftSeatId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BookingPassenger>().WithMany().HasForeignKey(r => r.BookingPassengerId).OnDelete(DeleteBehavior.Cascade);

        // The core double-booking guarantee: one active reservation per seat per flight.
        builder.HasIndex(r => new { r.FlightId, r.AircraftSeatId })
            .IsUnique()
            .HasFilter(ActiveFilter)
            .HasDatabaseName("ux_seat_reservations_active_seat");

        // A passenger occupies at most one seat per segment.
        builder.HasIndex(r => new { r.BookingSegmentId, r.BookingPassengerId })
            .IsUnique()
            .HasFilter(ActiveFilter)
            .HasDatabaseName("ux_seat_reservations_active_passenger");
    }
}

internal sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        builder.ComplexProperty(p => p.Amount, m => m.ConfigureMoney());
        builder.ComplexProperty(p => p.RefundedAmount, m => m.ConfigureMoney());
        builder.Property(p => p.IdempotencyKey).HasMaxLength(64);
        builder.Property(p => p.RequestHash).HasMaxLength(64);
        builder.Property(p => p.Provider).HasMaxLength(32);
        builder.Property(p => p.ProviderReference).HasMaxLength(64);
        builder.Property(p => p.FailureCode).HasMaxLength(64);

        builder.HasIndex(p => new { p.BookingId, p.IdempotencyKey }).IsUnique();
        builder.HasIndex(p => p.BookingId)
            .IsUnique()
            .HasFilter("status IN ('Succeeded', 'PartiallyRefunded', 'Refunded')")
            .HasDatabaseName("ux_payment_attempts_one_success_per_booking");
    }
}

internal sealed class BookingStatusHistoryConfiguration : IEntityTypeConfiguration<BookingStatusHistory>
{
    public void Configure(EntityTypeBuilder<BookingStatusHistory> builder)
    {
        builder.ToTable("booking_status_history");
        builder.Property(h => h.Reason).HasMaxLength(200);
        builder.HasIndex(h => new { h.BookingId, h.ChangedAt });
    }
}
