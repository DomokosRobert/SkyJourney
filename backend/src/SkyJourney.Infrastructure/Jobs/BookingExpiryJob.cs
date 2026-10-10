using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkyJourney.Application.Bookings;

namespace SkyJourney.Infrastructure.Jobs;

/// <summary>Expires unpaid bookings whose hold has lapsed, giving their seats back (requests also do this lazily per flight).</summary>
public sealed partial class BookingExpiryJob(
    IServiceScopeFactory scopes, IOptions<BackgroundJobOptions> options, TimeProvider time, ILogger<BookingExpiryJob> logger)
    : RecurringJob(scopes, options, time, logger)
{
    /// <summary>Bookings expired per batch; a run keeps going while batches come back full.</summary>
    public const int BatchSize = 200;

    protected override TimeSpan Interval(BackgroundJobOptions options) => options.BookingExpiryInterval;

    public override async Task RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var expiry = services.GetRequiredService<BookingExpiryService>();
        var total = 0;
        int expired;
        do
        {
            expired = await expiry.ExpireDueAsync(null, BatchSize, cancellationToken);
            total += expired;
        }
        while (expired == BatchSize);

        if (total > 0)
        {
            LogExpired(Logger, total);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Expired {Count} unpaid booking(s)")]
    private static partial void LogExpired(ILogger logger, int count);
}
