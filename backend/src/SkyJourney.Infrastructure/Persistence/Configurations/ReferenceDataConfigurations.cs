using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyJourney.Domain.Airports;
using SkyJourney.Domain.Fleet;

namespace SkyJourney.Infrastructure.Persistence.Configurations;

internal sealed class AirportConfiguration : IEntityTypeConfiguration<Airport>
{
    public void Configure(EntityTypeBuilder<Airport> builder)
    {
        builder.Property(a => a.IataCode).HasMaxLength(3).IsFixedLength();
        builder.HasIndex(a => a.IataCode).IsUnique();
        builder.Property(a => a.Name).HasMaxLength(100);
        builder.Property(a => a.City).HasMaxLength(100);
        builder.Property(a => a.CountryCode).HasMaxLength(2).IsFixedLength();
        builder.Property(a => a.TimeZoneId).HasMaxLength(64);
        builder.Ignore(a => a.TimeZone);
    }
}

internal sealed class AirlineConfiguration : IEntityTypeConfiguration<Airline>
{
    public void Configure(EntityTypeBuilder<Airline> builder)
    {
        builder.Property(a => a.Code).HasMaxLength(2).IsFixedLength();
        builder.HasIndex(a => a.Code).IsUnique();
        builder.Property(a => a.Name).HasMaxLength(100);
    }
}

internal sealed class AircraftTypeConfiguration : IEntityTypeConfiguration<AircraftType>
{
    public void Configure(EntityTypeBuilder<AircraftType> builder)
    {
        builder.Property(a => a.Code).HasMaxLength(8);
        builder.HasIndex(a => a.Code).IsUnique();
        builder.Property(a => a.Name).HasMaxLength(100);
        builder.Property(a => a.SeatLetters).HasMaxLength(10);
        builder.Ignore(a => a.Capacity);
        builder.HasMany(a => a.Seats).WithOne().HasForeignKey(s => s.AircraftTypeId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AircraftSeatConfiguration : IEntityTypeConfiguration<AircraftSeat>
{
    public void Configure(EntityTypeBuilder<AircraftSeat> builder)
    {
        builder.Property(s => s.Letter).HasMaxLength(1).IsFixedLength();
        builder.Property(s => s.SeatNumber).HasMaxLength(4);
        builder.HasIndex(s => new { s.AircraftTypeId, s.SeatNumber }).IsUnique();
    }
}
