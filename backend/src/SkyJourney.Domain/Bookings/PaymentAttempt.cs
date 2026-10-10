using SkyJourney.Domain.Common;

namespace SkyJourney.Domain.Bookings;

/// <summary>One attempt to pay for a booking. Never contains card data, only provider references.</summary>
public sealed class PaymentAttempt : Entity
{
    private PaymentAttempt()
    {
    }

    internal PaymentAttempt(Guid bookingId, Money amount, string idempotencyKey, string provider, DateTimeOffset now)
    {
        BookingId = bookingId;
        Amount = amount;
        IdempotencyKey = idempotencyKey;
        Provider = provider;
        Status = PaymentStatus.Pending;
        RefundedAmount = Money.Zero(amount.Currency);
        CreatedAt = now;
    }

    public Guid BookingId { get; private set; }

    public Money Amount { get; private set; } = null!;

    public Money RefundedAmount { get; private set; } = null!;

    public PaymentStatus Status { get; private set; }

    public string IdempotencyKey { get; private set; } = null!;

    public string Provider { get; private set; } = null!;

    public string? ProviderReference { get; private set; }

    public string? FailureCode { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    internal void MarkSucceeded(string providerReference, DateTimeOffset now)
    {
        EnsurePending();
        Status = PaymentStatus.Succeeded;
        ProviderReference = providerReference;
        CompletedAt = now;
    }

    internal void MarkFailed(string failureCode, string? providerReference, DateTimeOffset now)
    {
        EnsurePending();
        Status = PaymentStatus.Failed;
        FailureCode = failureCode;
        ProviderReference = providerReference;
        CompletedAt = now;
    }

    internal void Refund(Money amount)
    {
        if (Status != PaymentStatus.Succeeded)
        {
            throw new DomainException("invalid_payment_transition", $"A {Status} payment cannot be refunded.");
        }

        if (amount.Amount == 0)
        {
            return;
        }

        RefundedAmount = amount;
        Status = amount.Amount >= Amount.Amount ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
    }

    private void EnsurePending()
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new DomainException("invalid_payment_transition", $"Payment is already {Status}.");
        }
    }
}
