using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using SkyJourney.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(SkyJourney.IntegrationTests.Infrastructure.SkyJourneyApiFactory))]

namespace SkyJourney.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in memory against a throwaway PostgreSQL container (one per test run).
/// The clock is frozen at <see cref="StartTime"/>, so seeded flights and prices are deterministic.
/// </summary>
public sealed class SkyJourneyApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Monday 2 November 2026, 06:00 UTC (07:00 in Budapest, after the October DST change).</summary>
    public static readonly DateTimeOffset StartTime = new(2026, 11, 2, 6, 0, 0, TimeSpan.Zero);

    public const int HorizonDays = 21;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("skyjourney_tests")
        .Build();

    public FakeTimeProvider Clock { get; } = new(StartTime);

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        // Building the host runs migrations and seeding (see DatabaseInitializer).
        _ = Services;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:SkyJourney", _postgres.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("FlightData:SeedOnStartup", "true");
        builder.UseSetting("FlightData:HorizonDays", HorizonDays.ToString(System.Globalization.CultureInfo.InvariantCulture));
        // All in-memory requests share one client "IP"; RateLimitTests uses its own low limit.
        builder.UseSetting("RateLimiting:AuthPermitLimit", "100000");
        builder.UseSetting("RateLimiting:PaymentPermitLimit", "100000");
        // Tests run the recurring jobs explicitly (RunOnceAsync) so they never race with assertions.
        builder.UseSetting("BackgroundJobs:Enabled", "false");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    /// <summary>Runs <paramref name="action"/> with a fresh DbContext, e.g. to arrange or assert database state.</summary>
    public async Task WithDbAsync(Func<SkyJourneyDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<SkyJourneyDbContext>());
    }

    public async Task<T> WithDbAsync<T>(Func<SkyJourneyDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<SkyJourneyDbContext>());
    }
}

public static class HttpResponseExtensions
{
    public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<T>(SkyJourneyApiFactory.Json, TestContext.Current.CancellationToken);
        return body ?? throw new InvalidOperationException("Response body was empty.");
    }
}
