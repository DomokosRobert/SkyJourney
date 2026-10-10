using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkyJourney.Application.Common;
using SkyJourney.Application.FlightData;

namespace SkyJourney.Infrastructure.Jobs;

/// <summary>
/// Keeps flight instances generated from the schedules for the configured number of days ahead, so the bookable
/// window rolls forward every day without a restart. Generation skips flights that already exist.
/// </summary>
public sealed partial class FlightHorizonJob(
    IServiceScopeFactory scopes, IOptions<BackgroundJobOptions> options, TimeProvider time, ILogger<FlightHorizonJob> logger)
    : RecurringJob(scopes, options, time, logger)
{
    protected override TimeSpan Interval(BackgroundJobOptions options) => options.FlightHorizonInterval;

    public override async Task RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var horizon = services.GetRequiredService<IOptions<FlightDataOptions>>().Value.HorizonDays;
        var today = DateOnly.FromDateTime(Time.GetUtcNow().UtcDateTime);
        try
        {
            var created = await services.GetRequiredService<FlightDataImporter>()
                .GenerateFlightsAsync(today, today.AddDays(horizon), cancellationToken);
            if (created > 0)
            {
                LogGenerated(Logger, created, today.AddDays(horizon));
            }
        }
        catch (UniqueConstraintViolationException)
        {
            // Another instance generated the same flights concurrently; the next run fills any gap.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated {Count} flight(s) up to {Until}")]
    private static partial void LogGenerated(ILogger logger, int count, DateOnly until);
}
