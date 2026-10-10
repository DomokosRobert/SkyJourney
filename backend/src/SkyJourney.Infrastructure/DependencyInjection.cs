using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SkyJourney.Application.Auth;
using SkyJourney.Application.Common;
using SkyJourney.Application.FlightData;
using SkyJourney.Infrastructure.FlightData;
using SkyJourney.Infrastructure.Identity;
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

    /// <summary>
    /// ASP.NET Core Identity (users, password hashing, lockout), token issuance and the auth service.
    /// Without a configured signing key, Development and Testing fall back to a random per-process key.
    /// </summary>
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = AccountRules.PasswordMinLength;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<SkyJourneyDbContext>();

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var allowEphemeralKey = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        services.AddSingleton(sp => JwtSigningKey.Create(sp.GetRequiredService<IOptions<JwtOptions>>().Value, allowEphemeralKey));
        services.AddSingleton<AccessTokenIssuer>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
