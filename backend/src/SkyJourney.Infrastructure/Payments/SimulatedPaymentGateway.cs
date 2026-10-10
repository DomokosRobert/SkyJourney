using SkyJourney.Application.Payments;

namespace SkyJourney.Infrastructure.Payments;

/// <summary>
/// A stand-in for a card processor's test mode. The outcome depends only on the test token, so it is deterministic.
/// Nothing is charged and no payment data is involved; swapping in a real provider means implementing
/// <see cref="IPaymentGateway"/> against its API (see ADR-0008).
/// </summary>
public sealed class SimulatedPaymentGateway : IPaymentGateway
{
    public const string SuccessToken = "sim_success";
    public const string DeclinedToken = "sim_declined";
    public const string InsufficientFundsToken = "sim_insufficient_funds";

    public string Name => "Simulated";

    public Task<PaymentGatewayResult> ChargeAsync(PaymentCharge charge, CancellationToken cancellationToken)
    {
        // Derived from the attempt id, so retrying the same attempt yields the same reference (provider-side idempotency).
        var reference = $"sim_{charge.PaymentAttemptId:N}";
        var result = charge.PaymentToken switch
        {
            SuccessToken => PaymentGatewayResult.Success(reference),
            DeclinedToken => PaymentGatewayResult.Declined(PaymentFailures.CardDeclined, reference),
            InsufficientFundsToken => PaymentGatewayResult.Declined(PaymentFailures.InsufficientFunds, reference),
            _ => PaymentGatewayResult.Declined(PaymentFailures.InvalidPaymentToken),
        };
        return Task.FromResult(result);
    }
}
