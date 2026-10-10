using Microsoft.EntityFrameworkCore;
using SkyJourney.Application.Common;
using SkyJourney.Domain.Bookings;

namespace SkyJourney.Application.Bookings;

/// <summary>
/// Expires unpaid bookings whose hold has lapsed: releases their seats and gives their places back to the flights.
/// Runs lazily before anything that needs up-to-date availability on a flight, and periodically from a background job.
/// </summary>
public sealed class BookingExpiryService(ISkyJourneyDbContext db, TimeProvider time)
{
    /// <summary>Upper bound for one lazy pass, so a request never does unbounded clean-up work.</summary>
    public const int LazyBatchSize = 50;

    /// <summary>
    /// Expires up to <paramref name="maxBookings"/> lapsed bookings, optionally only those on the given flights.
    /// Clears the change tracker, so call it before loading the entities a request works with.
    /// </summary>
    /// <returns>The number of bookings this call expired.</returns>
    public async Task<int> ExpireDueAsync(IReadOnlyCollection<Guid>? flightIds, int maxBookings, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var due = db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.PendingPayment && b.ExpiresAt <= now);
        if (flightIds is not null)
        {
            due = due.Where(b => b.Segments.Any(s => flightIds.Contains(s.FlightId)));
        }

        var ids = await due.OrderBy(b => b.ExpiresAt).Select(b => b.Id).Take(maxBookings).ToListAsync(cancellationToken);

        var expired = 0;
        foreach (var id in ids)
        {
            if (await TryExpireAsync(id, now, cancellationToken))
            {
                expired++;
            }
        }

        return expired;
    }

    private async Task<bool> TryExpireAsync(Guid bookingId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var booking = await db.Bookings
                .Include(b => b.Passengers)
                .Include(b => b.Segments).ThenInclude(s => s.SeatReservations)
                .AsSplitQuery()
                .SingleOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

            if (booking is null || !booking.IsHoldExpired(now))
            {
                return false;
            }

            booking.Expire(now);
            await db.SaveChangesAsync(cancellationToken);
            await FlightCapacity.ReleaseAsync(db, booking, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request paid, cancelled or expired the booking first; its outcome stands.
            return false;
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }
}
