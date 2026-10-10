using SkyJourney.Application.Payments;
using SkyJourney.Domain.Common;
using SkyJourney.Infrastructure.Payments;

namespace SkyJourney.UnitTests.Payments;

public sealed class PaymentTests
{
    private readonly PayBookingRequestValidator _validator = new();
    private readonly SimulatedPaymentGateway _gateway = new();

    [Theory]
    [InlineData("sim_success", true)]
    [InlineData("tok_ABC123", true)]
    [InlineData("", false)]
    [InlineData("4111 1111 1111 1111", false)]
    [InlineData("sim-success", false)]
    public void Payment_token_must_be_an_opaque_test_token(string token, bool valid) =>
        Assert.Equal(valid, _validator.Validate(new PayBookingRequest(token)).IsValid);

    [Fact]
    public void Payment_token_length_is_capped() =>
        Assert.False(_validator.Validate(new PayBookingRequest(new string('a', 65))).IsValid);

    [Fact]
    public void Request_hash_is_stable_and_depends_on_the_token()
    {
        var hash = PaymentService.ComputeRequestHash(new PayBookingRequest("sim_success"));

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, PaymentService.ComputeRequestHash(new PayBookingRequest("sim_success")));
        Assert.NotEqual(hash, PaymentService.ComputeRequestHash(new PayBookingRequest("sim_declined")));
    }

    [Theory]
    [InlineData(SimulatedPaymentGateway.SuccessToken, true, null)]
    [InlineData(SimulatedPaymentGateway.DeclinedToken, false, PaymentFailures.CardDeclined)]
    [InlineData(SimulatedPaymentGateway.InsufficientFundsToken, false, PaymentFailures.InsufficientFunds)]
    [InlineData("tok_real_looking", false, PaymentFailures.InvalidPaymentToken)]
    public async Task Simulated_gateway_outcome_depends_only_on_the_token(string token, bool succeeded, string? failureCode)
    {
        var attemptId = Guid.CreateVersion7();

        var result = await _gateway.ChargeAsync(
            new PaymentCharge(attemptId, "SJ1234", new Money(100m, "EUR"), token), TestContext.Current.CancellationToken);

        Assert.Equal(succeeded, result.Succeeded);
        Assert.Equal(failureCode, result.FailureCode);
        if (succeeded)
        {
            Assert.Equal($"sim_{attemptId:N}", result.ProviderReference);
        }
    }

    [Fact]
    public void Every_failure_code_has_a_customer_message()
    {
        foreach (var code in new[] { PaymentFailures.CardDeclined, PaymentFailures.InsufficientFunds,
                     PaymentFailures.InvalidPaymentToken, PaymentFailures.PaymentTimeout })
        {
            Assert.False(string.IsNullOrWhiteSpace(PaymentFailures.Describe(code)));
        }
    }
}
