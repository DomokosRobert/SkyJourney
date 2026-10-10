using SkyJourney.Application.Bookings;
using SkyJourney.Application.Common;
using SkyJourney.Domain.Bookings;

namespace SkyJourney.Application.Payments;

/// <summary>
/// A payment request. The token stands in for a payment method created on the client by a payment provider's SDK;
/// no card number, CVV or bank detail ever reaches this API.
/// </summary>
public sealed record PayBookingRequest(string PaymentToken);

public sealed record PaymentResultDto(
    Guid PaymentId,
    PaymentStatus Status,
    MoneyDto Amount,
    string? FailureCode,
    string? FailureMessage,
    BookingDto Booking);

public sealed record PaymentResult(PaymentResultDto Payment, bool Created);
