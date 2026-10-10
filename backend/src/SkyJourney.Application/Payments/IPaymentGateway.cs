using SkyJourney.Domain.Common;

namespace SkyJourney.Application.Payments;

/// <summary>Charges a payment method. Implementations must treat <see cref="PaymentCharge.PaymentAttemptId"/> as an idempotency key.</summary>
public interface IPaymentGateway
{
    /// <summary>Stored on each payment attempt as its provider (max. 32 characters).</summary>
    string Name { get; }

    Task<PaymentGatewayResult> ChargeAsync(PaymentCharge charge, CancellationToken cancellationToken);
}

public sealed record PaymentCharge(Guid PaymentAttemptId, string BookingReference, Money Amount, string PaymentToken);

/// <summary>The provider's answer. A decline is a normal result, not an exception.</summary>
public sealed record PaymentGatewayResult(bool Succeeded, string? ProviderReference, string? FailureCode)
{
    public static PaymentGatewayResult Success(string providerReference) => new(true, providerReference, null);

    public static PaymentGatewayResult Declined(string failureCode, string? providerReference = null) =>
        new(false, providerReference, failureCode);
}
