using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SkyJourney.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c> tooling, so migrations can be created without booting the API.
/// Override the connection with the <c>SKYJOURNEY_DESIGN_CONNECTION</c> environment variable.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SkyJourneyDbContext>
{
    public SkyJourneyDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SKYJOURNEY_DESIGN_CONNECTION")
            ?? "Host=localhost;Port=5433;Database=skyjourney;Username=skyjourney;Password=skyjourney_dev_password";

        var options = new DbContextOptionsBuilder<SkyJourneyDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new SkyJourneyDbContext(options);
    }
}
