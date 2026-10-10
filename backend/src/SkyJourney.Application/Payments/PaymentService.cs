using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkyJourney.Application.Bookings;
using SkyJourney.Application.Common;
using SkyJourney.Domain.Bookings;
using SkyJourney.Domain.Flights;

namespace SkyJourney.Application.Payments;

/// <summary>
/// Pays for an unpaid booking in three steps: record a pending attempt (which claims the booking against concurrent
/// payments, seat changes, cancellation and expiry), charge the gateway outside any transaction, then record the
/// outcome and confirm the booking. Retries with the same <c>Idempotency-Key</c> return the recorded outcome.
/// </summary>
public sealed partial class PaymentService(
    ISkyJourneyDbContext db,
    ICurrentUser currentUser,
    BookingService bookings,
    BookingExpiryService expiry,
    IPaymentGateway gateway,
    TimeProvider time,
    ILogger<PaymentService> logger)
{
    internal const string IdempotencyIndex = "ix_payment_attempts_booking_id_idempotency_key";

    public async Task<PaymentResult> PayAsync(string reference, PayBookingRequest request, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var normalized = reference.Trim().ToUpperInvariant();
        var target = await db.Bookings.AsNoTracking()
            .Where(b => b.UserId == userId && b.Reference == normalized)
            .Select(b => new
            {
                b.Id,
                b.Reference,
                FlightIds = b.Segments.Select(s => s.FlightId).ToList(),
                FlightCancelled = b.Segments.Any(s => s.Flight.Status == FlightStatus.Cancelled),
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Booking", reference);

        var requestHash = ComputeRequestHash(request);
        if (await FindReplayAsync(target.Id, target.Reference, idempotencyKey, requestHash, cancellationToken) is { } replay)
        {
            return new PaymentResult(replay, Created: false);
        }

        if (target.FlightCancelled)
        {
            throw new ConflictException("flight_cancelled",
                "A flight in this booking has been cancelled by the airline. The booking can no longer be paid.");
        }

        await expiry.ExpireDueAsync(target.FlightIds, BookingExpiryService.LazyBatchSize, cancellationToken);

        // 1. Record the attempt. Saving it also bumps the booking's version, so whichever concurrent request
        //    touches the booking second (another payment, a seat change, a cancellation, the expiry job) loses.
        var booking = await db.Bookings
            .Include(b => b.Segments).ThenInclude(s => s.SeatReservations)
            .Include(b => b.Payments)
            .AsSplitQuery()
            .SingleAsync(b => b.Id == target.Id, cancellationToken);

        if (booking.Status == BookingStatus.Expired || booking.IsHoldExpired(time.GetUtcNow()))
        {
            throw new ConflictException("booking_expired", "The booking hold has expired. Please search again.");
        }

        PaymentAttempt attempt;
        try
        {
            attempt = booking.StartPayment(idempotencyKey, requestHash, gateway.Name, time.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException or UniqueConstraintViolationException { ConstraintName: IdempotencyIndex })
        {
            // A concurrent request changed the booking first. If it was a retry with the same key, answer with its outcome.
            db.ChangeTracker.Clear();
            var winner = await FindReplayAsync(target.Id, target.Reference, idempotencyKey, requestHash, cancellationToken);
            return winner is not null
                ? new PaymentResult(winner, Created: false)
                : throw new ConflictException("payment_in_progress", "The booking was changed by another request. Reload it and try again.");
        }

        // 2. Charge. Not cancellable from here on: once the provider may have taken money, the outcome must be recorded.
        var outcome = await gateway.ChargeAsync(
            new PaymentCharge(attempt.Id, booking.Reference, attempt.Amount, request.PaymentToken), CancellationToken.None);

        // 3. Record the outcome; success confirms the booking and its seats.
        booking.CompletePayment(attempt, outcome.Succeeded, outcome.ProviderReference, outcome.FailureCode, time.GetUtcNow());
        try
        {
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Only possible when the charge outlived Booking.PaymentTimeout and the booking was expired meanwhile.
            // A real provider integration must refund (or void) the charge here; the simulated gateway never gets here.
            if (outcome.Succeeded)
            {
                LogLateCharge(attempt.Id, booking.Reference, outcome.ProviderReference);
            }

            throw new ConflictException(PaymentFailures.PaymentTimeout, PaymentFailures.Describe(PaymentFailures.PaymentTimeout));
        }

        db.ChangeTracker.Clear();
        return new PaymentResult(await ToDtoAsync(attempt, target.Reference, cancellationToken), Created: true);
    }

    /// <summary>SHA-256 of the request body; the same key with a different payment method is rejected.</summary>
    internal static string ComputeRequestHash(PayBookingRequest request) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));

    private async Task<PaymentResultDto?> FindReplayAsync(Guid bookingId, string reference, string idempotencyKey,
        string requestHash, CancellationToken cancellationToken)
    {
        var existing = await db.PaymentAttempts.AsNoTracking()
            .SingleOrDefaultAsync(p => p.BookingId == bookingId && p.IdempotencyKey == idempotencyKey, cancellationToken);

        if (existing is null)
        {
            return null;
        }

        if (existing.RequestHash != requestHash)
        {
            throw new UnprocessableException("idempotency_key_reused",
                "This Idempotency-Key was already used for a different payment request. Use a new key per payment.");
        }

        if (existing.Status == PaymentStatus.Pending)
        {
            throw new ConflictException("payment_in_progress", "This payment is still being processed. Try again shortly.");
        }

        return await ToDtoAsync(existing, reference, cancellationToken);
    }

    private async Task<PaymentResultDto> ToDtoAsync(PaymentAttempt attempt, string reference, CancellationToken cancellationToken) =>
        new(attempt.Id,
            attempt.Status,
            MoneyDto.From(attempt.Amount),
            attempt.FailureCode,
            attempt.Status == PaymentStatus.Failed ? PaymentFailures.Describe(attempt.FailureCode) : null,
            await bookings.GetAsync(reference, cancellationToken));

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Payment {PaymentId} for booking {Reference} succeeded at the provider ({ProviderReference}) after the booking changed; it must be refunded")]
    private partial void LogLateCharge(Guid paymentId, string reference, string? providerReference);
}
