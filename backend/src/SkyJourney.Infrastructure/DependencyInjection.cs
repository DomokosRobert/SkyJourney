using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkyJourney.Application.Common;
using SkyJourney.Application.FlightData;
using SkyJourney.Infrastructure.FlightData;
using SkyJourney.Infrastructure.Persistence;

namespace SkyJourney.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "SkyJourney";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<SkyJourneyDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<ISkyJourneyDbContext>(sp => sp.GetRequiredService<SkyJourneyDbContext>());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IFlightDataProvider, DeterministicFlightDataProvider>();
        return services;
    }
}
