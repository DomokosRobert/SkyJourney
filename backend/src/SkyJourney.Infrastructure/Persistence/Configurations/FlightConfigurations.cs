using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyJourney.Domain.Flights;

namespace SkyJourney.Infrastructure.Persistence.Configurations;

internal sealed class FlightScheduleConfiguration : IEntityTypeConfiguration<FlightSchedule>
{
    public void Configure(EntityTypeBuilder<FlightSchedule> builder)
    {
        builder.Property(s => s.FlightNumber).HasMaxLength(6);
        builder.Property(s => s.OperatingDays).HasConversion<int>();
        builder.ComplexProperty(s => s.BaseFare, m => m.ConfigureMoney());

        builder.HasOne(s => s.Airline).WithMany().HasForeignKey(s => s.AirlineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Origin).WithMany().HasForeignKey(s => s.OriginAirportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Destination).WithMany().HasForeignKey(s => s.DestinationAirportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.AircraftType).WithMany().HasForeignKey(s => s.AircraftTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.FlightNumber);
        builder.ToTable(t => t.HasCheckConstraint("ck_flight_schedules_route", "origin_airport_id <> destination_airport_id"));
    }
}

internal sealed class FlightConfiguration : IEntityTypeConfiguration<Flight>
{
    public void Configure(EntityTypeBuilder<Flight> builder)
    {
        builder.Property(f => f.FlightNumber).HasMaxLength(6);

        builder.HasOne(f => f.Airline).WithMany().HasForeignKey(f => f.AirlineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(f => f.Origin).WithMany().HasForeignKey(f => f.OriginAirportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(f => f.Destination).WithMany().HasForeignKey(f => f.DestinationAirportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(f => f.AircraftType).WithMany().HasForeignKey(f => f.AircraftTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FlightSchedule>().WithMany().HasForeignKey(f => f.FlightScheduleId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(f => f.Fares).WithOne().HasForeignKey(f => f.FlightId).OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(f => f.Duration);
        builder.Ignore(f => f.SeatsAvailable);

        // One instance of a flight number per local day; makes schedule generation idempotent.
        builder.HasIndex(f => new { f.FlightNumber, f.DepartureLocalDate }).IsUnique();
        // Search: route + local date.
        builder.HasIndex(f => new { f.OriginAirportId, f.DestinationAirportId, f.DepartureLocalDate });

        builder.ToTable(t =>
        {
            // Last line of defence against overselling, independent of application code.
            t.HasCheckConstraint("ck_flights_seats_sold", "seats_sold >= 0 AND seats_sold <= capacity");
            t.HasCheckConstraint("ck_flights_times", "arrival_utc > departure_utc");
            t.HasCheckConstraint("ck_flights_route", "origin_airport_id <> destination_airport_id");
        });
    }
}

internal sealed class FareConfiguration : IEntityTypeConfiguration<Fare>
{
    public void Configure(EntityTypeBuilder<Fare> builder)
    {
        builder.ComplexProperty(f => f.Price, m => m.ConfigureMoney());
        builder.ComplexProperty(f => f.Conditions, c => c.ConfigureConditions());
        builder.HasIndex(f => new { f.FlightId, f.Family }).IsUnique();
    }
}
