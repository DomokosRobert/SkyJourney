using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SkyJourney.Infrastructure.Jobs;

public sealed class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobs";

    /// <summary>Run the recurring jobs in this process. Off in tests; with several API instances every instance may run them (they are safe to overlap).</summary>
    public bool Enabled { get; init; } = true;

    public TimeSpan BookingExpiryInterval { get; init; } = TimeSpan.FromMinutes(1);

    public TimeSpan FlightHorizonInterval { get; init; } = TimeSpan.FromHours(6);
}

/// <summary>
/// Runs <see cref="RunOnceAsync"/> at start-up and then on a fixed interval, each run in its own DI scope.
/// A failed run is logged and retried on the next tick; it never stops the host.
/// </summary>
public abstract partial class RecurringJob(IServiceScopeFactory scopes, IOptions<BackgroundJobOptions> options, TimeProvider time, ILogger logger)
    : BackgroundService
{
    protected ILogger Logger => logger;

    protected TimeProvider Time => time;

    protected abstract TimeSpan Interval(BackgroundJobOptions options);

    /// <summary>One pass of the job. Public so tests can run it deterministically.</summary>
    public abstract Task RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval(options.Value), time);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await RunOnceAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogRunFailed(ex, GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Background job {Job} failed; retrying on the next interval")]
    private partial void LogRunFailed(Exception exception, string job);
}
