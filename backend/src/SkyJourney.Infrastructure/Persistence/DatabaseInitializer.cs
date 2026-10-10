using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SkyJourney.Application.FlightData;

namespace SkyJourney.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Apply pending EF Core migrations at startup. Development only; production runs migrations as a release step.</summary>
    public bool MigrateOnStartup { get; init; }
}

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var database = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        var flightData = configuration.GetSection(FlightDataOptions.SectionName).Get<FlightDataOptions>() ?? new FlightDataOptions();
        if (!database.MigrateOnStartup && !flightData.SeedOnStartup)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SkyJourneyDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        if (database.MigrateOnStartup)
        {
            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count > 0)
            {
                logger.LogInformation("Applying {Count} pending migration(s): {Migrations}", pending.Count, pending);
                await db.Database.MigrateAsync(cancellationToken);
            }
        }

        if (flightData.SeedOnStartup)
        {
            var importer = scope.ServiceProvider.GetRequiredService<FlightDataImporter>();
            var today = DateOnly.FromDateTime(scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            await importer.ImportReferenceDataAsync(cancellationToken);
            await importer.GenerateFlightsAsync(today, today.AddDays(flightData.HorizonDays), cancellationToken);
        }
    }
}
