using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using SkyJourney.Domain.Bookings;
using SkyJourney.Infrastructure.Jobs;
using SkyJourney.IntegrationTests.Bookings;
using SkyJourney.IntegrationTests.Infrastructure;

namespace SkyJourney.IntegrationTests.Jobs;

/// <summary>
/// The recurring jobs are disabled in tests (BackgroundJobs:Enabled=false); these tests run one pass explicitly.
/// </summary>
public sealed class JobTests(SkyJourneyApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task RunOnceAsync<TJob>(WebApplicationFactory<Program> host) where TJob : RecurringJob
    {
        var job = host.Services.GetServices<IHostedService>().OfType<TJob>().Single();
        await using var scope = host.Services.CreateAsyncScope();
        await job.RunOnceAsync(scope.ServiceProvider, Ct);
    }

    [Fact]
    public async Task Booking_expiry_job_expires_lapsed_holds_and_releases_their_places()
    {
        var flight = await factory.FindFlightAsync("BUD", "FCO", new DateOnly(2026, 11, 11));
        var client = await factory.CreateUserClientAsync();
        var lapsed = await client.CreateBookingAsync(BookingTestHelpers.Request(flight.Id, adults: 2));
        var active = await client.CreateBookingAsync(BookingTestHelpers.Request(flight.Id));
        // Move only this booking's hold into the past; the shared clock stays frozen for every other test.
        var past = SkyJourneyApiFactory.StartTime - TimeSpan.FromMinutes(1);
        await factory.WithDbAsync(db => db.Bookings.Where(b => b.Id == lapsed.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.ExpiresAt, past), Ct));

        await RunOnceAsync<BookingExpiryJob>(factory);

        var statuses = await factory.WithDbAsync(db => db.Bookings.AsNoTracking()
            .Where(b => b.Id == lapsed.Id || b.Id == active.Id).ToDictionaryAsync(b => b.Id, b => b.Status, Ct));
        Assert.Equal(BookingStatus.Expired, statuses[lapsed.Id]);
        Assert.Equal(BookingStatus.PendingPayment, statuses[active.Id]);
        Assert.Equal(flight.SeatsSold + 1, await factory.SeatsSoldAsync(flight.Id));
    }

    [Fact]
    public async Task Flight_horizon_job_generates_the_next_day_once_the_date_rolls_over()
    {
        var clock = new FakeTimeProvider(SkyJourneyApiFactory.StartTime.AddDays(1));
        await using var host = factory.WithOwnClock(clock);
        var newDay = DateOnly.FromDateTime(SkyJourneyApiFactory.StartTime.UtcDateTime).AddDays(SkyJourneyApiFactory.HorizonDays + 1);
        Task<int> CountAsync() => factory.WithDbAsync(db => db.Flights.CountAsync(f => f.DepartureLocalDate == newDay, Ct));
        Assert.Equal(0, await CountAsync());

        await RunOnceAsync<FlightHorizonJob>(host);
        var generated = await CountAsync();
        await RunOnceAsync<FlightHorizonJob>(host);

        Assert.True(generated > 0, "Expected flights on the new last day of the horizon.");
        Assert.Equal(generated, await CountAsync());
    }
}
