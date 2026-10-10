using FluentValidation;
using FluentValidation.Results;
using SkyJourney.Application.Auth;
using SkyJourney.Domain.Bookings;

namespace SkyJourney.Application.Bookings;

internal sealed class FlightSelectionRequestValidator : AbstractValidator<FlightSelectionRequest>
{
    public FlightSelectionRequestValidator()
    {
        RuleFor(s => s.FlightId).NotEmpty();
        RuleFor(s => s.FareFamily).IsInEnum();
    }
}

internal sealed class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(r => r.Outbound).NotNull().SetValidator(new FlightSelectionRequestValidator());
        RuleFor(r => r.Return!).SetValidator(new FlightSelectionRequestValidator()).When(r => r.Return is not null);
        RuleFor(r => r.Return!.FlightId)
            .NotEqual(r => r.Outbound.FlightId).WithMessage("The return flight must differ from the outbound flight.")
            .When(r => r.Return is not null && r.Outbound is not null);

        RuleFor(r => r.Passengers).Cascade(CascadeMode.Stop).NotNull()
            .Must(p => p.Count is >= 1 and <= Booking.MaxPassengers)
            .WithMessage($"A booking needs 1-{Booking.MaxPassengers} passengers.")
            .Must(p => p.Any(x => x?.Type == PassengerType.Adult))
            .WithMessage("Every booking needs at least one adult.");
        RuleForEach(r => r.Passengers).NotNull().ChildRules(p =>
        {
            p.RuleFor(x => x.FirstName).ValidName();
            p.RuleFor(x => x.LastName).ValidName();
            p.RuleFor(x => x.Type).IsInEnum();
        });
    }
}

internal sealed class SelectSeatsRequestValidator : AbstractValidator<SelectSeatsRequest>
{
    public SelectSeatsRequestValidator()
    {
        RuleFor(r => r.Seats).Cascade(CascadeMode.Stop).NotEmpty()
            .Must(s => s.Count <= Booking.MaxPassengers)
            .WithMessage($"At most {Booking.MaxPassengers} seats can be selected at once.")
            .Must(s => s.Select(x => x?.PassengerId).Distinct().Count() == s.Count)
            .WithMessage("Each passenger may appear only once.")
            .Must(s => s.Select(x => x?.SeatNumber?.ToUpperInvariant()).Distinct().Count() == s.Count)
            .WithMessage("Each seat may appear only once.");
        RuleForEach(r => r.Seats).NotNull().ChildRules(s =>
        {
            s.RuleFor(x => x.PassengerId).NotEmpty();
            s.RuleFor(x => x.SeatNumber).NotEmpty().Matches("^[0-9]{1,2}[A-Za-z]$").WithMessage("Use a seat number such as 12C.");
        });
    }
}

/// <summary>Rules for the <c>Idempotency-Key</c> request header.</summary>
public static class IdempotencyKey
{
    public const string HeaderName = "Idempotency-Key";
    public const int MaxLength = 64;

    /// <summary>Returns the key, or throws a validation error naming the header.</summary>
    public static string Require(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw Invalid("The Idempotency-Key header is required. Send a new random value (e.g. a UUID) per booking attempt.");
        }

        if (key.Length > MaxLength || !key.All(c => c is > ' ' and <= '~'))
        {
            throw Invalid($"The Idempotency-Key header must be 1-{MaxLength} visible ASCII characters.");
        }

        return key;
    }

    private static ValidationException Invalid(string message) =>
        new([new ValidationFailure(HeaderName, message)]);
}
