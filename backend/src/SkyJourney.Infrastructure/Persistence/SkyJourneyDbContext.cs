using Microsoft.EntityFrameworkCore;

namespace SkyJourney.Infrastructure.Persistence;

public sealed class SkyJourneyDbContext(DbContextOptions<SkyJourneyDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SkyJourneyDbContext).Assembly);
    }
}
