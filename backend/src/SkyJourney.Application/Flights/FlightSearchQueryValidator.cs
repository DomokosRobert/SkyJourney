using FluentValidation;
using SkyJourney.Domain.Bookings;

namespace SkyJourney.Application.Flights;

internal sealed class FlightSearchQueryValidator : AbstractValidator<FlightSearchQuery>
{
    public FlightSearchQueryValidator()
    {
        RuleFor(q => q.Origin).NotEmpty().Matches("^[A-Za-z]{3}$").WithMessage("Origin must be a 3-letter IATA code.");
        RuleFor(q => q.Destination).NotEmpty().Matches("^[A-Za-z]{3}$").WithMessage("Destination must be a 3-letter IATA code.");
        RuleFor(q => q.Destination)
            .Must((q, destination) => !string.Equals(q.Origin, destination, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Origin and destination must differ.");

        RuleFor(q => q.DepartureDate).NotNull();
        RuleFor(q => q.ReturnDate)
            .GreaterThanOrEqualTo(q => q.DepartureDate)
            .When(q => q.ReturnDate.HasValue && q.DepartureDate.HasValue)
            .WithMessage("Return date cannot be before the departure date.");

        RuleFor(q => q.Adults).InclusiveBetween(1, Booking.MaxPassengers);
        RuleFor(q => q.Children).InclusiveBetween(0, Booking.MaxPassengers - 1);
        RuleFor(q => q.PassengerCount)
            .LessThanOrEqualTo(Booking.MaxPassengers)
            .OverridePropertyName("passengers")
            .WithMessage($"At most {Booking.MaxPassengers} passengers can travel on one booking.");

        RuleFor(q => q.SortOrder)
            .NotNull()
            .OverridePropertyName("sort")
            .WithMessage("Sort must be one of: price, departure, arrival, duration.");

        RuleFor(q => q.MaxPrice).GreaterThan(0);
        RuleFor(q => q.DepartBefore)
            .GreaterThanOrEqualTo(q => q.DepartAfter)
            .When(q => q.DepartAfter.HasValue && q.DepartBefore.HasValue);
    }
}
