using FluentValidation;

namespace SkyJourney.Application.Payments;

internal sealed class PayBookingRequestValidator : AbstractValidator<PayBookingRequest>
{
    public PayBookingRequestValidator()
    {
        RuleFor(r => r.PaymentToken).NotEmpty().MaximumLength(64)
            .Matches("^[A-Za-z0-9_]+$").WithMessage("Payment tokens contain only letters, digits and underscores.");
    }
}

/// <summary>Customer-facing explanations for payment failure codes.</summary>
public static class PaymentFailures
{
    public const string CardDeclined = "card_declined";
    public const string InsufficientFunds = "insufficient_funds";
    public const string InvalidPaymentToken = "invalid_payment_token";
    public const string PaymentTimeout = "payment_timeout";

    public static string Describe(string? code) => code switch
    {
        CardDeclined => "The payment was declined. Try a different payment method.",
        InsufficientFunds => "The payment method has insufficient funds.",
        InvalidPaymentToken => "The payment method could not be used. Choose it again and retry.",
        PaymentTimeout => "The payment did not complete in time and was not charged.",
        _ => "The payment failed. You have not been charged.",
    };
}
