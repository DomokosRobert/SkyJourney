using Microsoft.EntityFrameworkCore;
using SkyJourney.Application.Bookings;
using SkyJourney.Application.Common;
using SkyJourney.Domain.Bookings;
using SkyJourney.Domain.Common;

namespace SkyJourney.Application.Admin;

/// <summary>
/// Support view of all bookings. Unlike <see cref="BookingService"/> nothing is scoped to the caller; the endpoints
/// using this service require the Admin role. Responses carry the owner's user id but no contact details.
/// </summary>
public sealed class AdminBookingService(ISkyJourneyDbContext db, BookingExpiryService expiry, TimeProvider time)
{
    public async Task<PagedResult<AdminBookingSummaryDto>> ListAsync(AdminBookingQuery filter, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var paymentStartedAfter = now - Booking.PaymentTimeout;
        var page = new PageRequest(filter.Page ?? 1, filter.PageSize ?? 20);

        var query = db.Bookings.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Reference))
        {
            var reference = filter.Reference.Trim().ToUpperInvariant();
            query = query.Where(b => b.Reference == reference);
        }

        if (filter.FlightId is { } flightId)
        {
            query = query.Where(b => b.Segments.Any(s => s.FlightId == flightId));
        }

        if (filter.UserId is { } userId)
        {
            query = query.Where(b => b.UserId == userId);
        }

        // Filter on the status customers see: a lapsed hold the expiry job has not processed yet counts as Expired.
        switch (filter.Status)
        {
            case BookingStatus.PendingPayment:
                query = query.Where(b => b.Status == BookingStatus.PendingPayment && (b.ExpiresAt > now ||
                    b.Payments.Any(p => p.Status == PaymentStatus.Pending && p.CreatedAt > paymentStartedAfter)));
                break;
            case BookingStatus.Expired:
                query = query.Where(b => b.Status == BookingStatus.Expired || (b.Status == BookingStatus.PendingPayment &&
                    b.ExpiresAt <= now &&
                    !b.Payments.Any(p => p.Status == PaymentStatus.Pending && p.CreatedAt > paymentStartedAfter)));
                break;
            case { } status:
                query = query.Where(b => b.Status == status);
                break;
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(b => b.CreatedAt).ThenBy(b => b.Id)
            .Skip(page.Skip).Take(page.Size)
            .Select(b => new
            {
                b.Id,
                b.Reference,
                b.UserId,
                b.Status,
                b.CreatedAt,
                b.ExpiresAt,
                PaymentInFlight = b.Payments.Any(p => p.Status == PaymentStatus.Pending && p.CreatedAt > paymentStartedAfter),
                b.TotalPrice,
                b.RefundedAmount,
                PassengerCount = b.Passengers.Count,
                Flights = b.Segments.OrderBy(s => s.Direction).Select(s => new { s.Flight.FlightNumber, s.Flight.DepartureUtc }).ToList(),
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new AdminBookingSummaryDto(
            r.Id,
            r.Reference,
            r.UserId,
            BookingService.EffectiveStatus(r.Status, r.ExpiresAt, r.PaymentInFlight, now),
            r.CreatedAt,
            MoneyDto.From(r.TotalPrice),
            MoneyDto.From(r.RefundedAmount),
            r.PassengerCount,
            r.Flights.Select(f => f.FlightNumber).ToList(),
            r.Flights.Min(f => f.DepartureUtc))).ToList();

        return new PagedResult<AdminBookingSummaryDto>(items, Math.Max(page.Page, 1), page.Size, total);
    }

    public async Task<AdminBookingDto> GetAsync(string reference, CancellationToken cancellationToken)
    {
        var booking = await BookingService.WithDetails(ByReference(reference))
                          .Include(b => b.History)
                          .AsNoTracking()
                          .SingleOrDefaultAsync(cancellationToken)
                      ?? throw new NotFoundException("Booking", reference);

        return new AdminBookingDto(
            BookingService.ToDto(booking, time.GetUtcNow()),
            booking.UserId,
            booking.History.OrderBy(h => h.ChangedAt)
                .Select(h => new BookingHistoryDto(h.FromStatus, h.ToStatus, h.Actor, h.Reason, h.ChangedAt))
                .ToList(),
            booking.Payments.OrderBy(p => p.CreatedAt)
                .Select(p => new AdminPaymentDto(p.Id, p.Status, MoneyDto.From(p.Amount), MoneyDto.From(p.RefundedAmount),
                    p.Provider, p.ProviderReference, p.FailureCode, p.CreatedAt, p.CompletedAt))
                .ToList());
    }

    /// <summary>
    /// Cancels a booking on behalf of the airline: a confirmed booking is refunded in full regardless of fare rules
    /// and the 24-hour cutoff (simulated refund). Refused while a payment is in progress (409 payment_in_progress).
    /// </summary>
    public async Task<AdminBookingDto> CancelAsync(string reference, CancelRequest request, CancellationToken cancellationToken)
    {
        var flightIds = await ByReference(reference)
            .SelectMany(b => b.Segments.Select(s => s.FlightId))
            .ToListAsync(cancellationToken);
        if (flightIds.Count == 0)
        {
            throw new NotFoundException("Booking", reference);
        }

        await expiry.ExpireDueAsync(flightIds, BookingExpiryService.LazyBatchSize, cancellationToken);

        var booking = await BookingService.WithDetails(ByReference(reference)).SingleAsync(cancellationToken);
        if (booking.IsHoldExpired(time.GetUtcNow()))
        {
            throw new ConflictException("booking_expired", "The booking hold has already expired.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        booking.Cancel(BookingActor.Administrator, request.Reason.Trim(), time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await FlightCapacity.ReleaseAsync(db, booking, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        db.ChangeTracker.Clear();
        return await GetAsync(reference, cancellationToken);
    }

    /// <summary>
    /// Cancels one active booking as an administrator, for bulk operations such as a flight cancellation.
    /// Never throws for business reasons: a booking that cannot be cancelled right now is reported as skipped.
    /// </summary>
    /// <returns>The refund, or null when the booking was skipped or no longer active.</returns>
    internal async Task<(Money? Refund, SkippedBooking? Skipped)> TryCancelAsync(Guid bookingId, string reason,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        string? reference = null;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var booking = await BookingService.WithDetails(db.Bookings.Where(b => b.Id == bookingId)).SingleOrDefaultAsync(cancellationToken);
            if (booking is null || booking.Status is not (BookingStatus.PendingPayment or BookingStatus.Confirmed) ||
                booking.IsHoldExpired(now))
            {
                // Gone, already final, or a lapsed hold that the expiry job will release.
                return (null, null);
            }

            reference = booking.Reference;
            var refund = booking.Cancel(BookingActor.Administrator, reason, now);
            await db.SaveChangesAsync(cancellationToken);
            await FlightCapacity.ReleaseAsync(db, booking, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (refund, null);
        }
        catch (DomainException ex)
        {
            return (null, new SkippedBooking(reference ?? bookingId.ToString(), ex.Code, ex.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return (null, new SkippedBooking(reference ?? bookingId.ToString(), "concurrency_conflict",
                "The booking was changed by another request while it was being cancelled."));
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private IQueryable<Booking> ByReference(string reference)
    {
        var normalized = reference.Trim().ToUpperInvariant();
        return db.Bookings.Where(b => b.Reference == normalized);
    }
}
