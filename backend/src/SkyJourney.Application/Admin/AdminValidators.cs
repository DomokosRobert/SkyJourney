using FluentValidation;

namespace SkyJourney.Application.Admin;

internal static class AdminRules
{
    public const int AirportTextMaxLength = 100;
    public const int ReasonMaxLength = 200;
    public const decimal MaxFare = 10_000m;
    public const int MaxScheduleDays = 400;

    public static IRuleBuilderOptions<T, decimal> ValidPrice<T>(this IRuleBuilder<T, decimal> rule) => rule
        .GreaterThan(0m)
        .LessThanOrEqualTo(MaxFare)
        .Must(p => decimal.Round(p, 2) == p).WithMessage("At most two decimal places.");

    public static IRuleBuilderOptions<T, string> ValidReason<T>(this IRuleBuilder<T, string> rule) => rule
        .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Required.")
        .MaximumLength(ReasonMaxLength);
}

internal sealed class CreateAirportRequestValidator : AbstractValidator<CreateAirportRequest>
{
    public CreateAirportRequestValidator()
    {
        RuleFor(r => r.IataCode).NotEmpty().Matches("^[A-Za-z]{3}$").WithMessage("A 3-letter IATA code.");
        RuleFor(r => r.Name).NotEmpty().MaximumLength(AdminRules.AirportTextMaxLength);
        RuleFor(r => r.City).NotEmpty().MaximumLength(AdminRules.AirportTextMaxLength);
        RuleFor(r => r.CountryCode).NotEmpty().Matches("^[A-Z]{2}$").WithMessage("An ISO 3166-1 alpha-2 code, e.g. HU.");
        RuleFor(r => r.TimeZoneId).NotEmpty().MaximumLength(64);
        RuleFor(r => r.Latitude).InclusiveBetween(-90, 90);
        RuleFor(r => r.Longitude).InclusiveBetween(-180, 180);
    }
}

internal sealed class UpdateAirportRequestValidator : AbstractValidator<UpdateAirportRequest>
{
    public UpdateAirportRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(AdminRules.AirportTextMaxLength);
        RuleFor(r => r.City).NotEmpty().MaximumLength(AdminRules.AirportTextMaxLength);
        RuleFor(r => r.CountryCode).NotEmpty().Matches("^[A-Z]{2}$").WithMessage("An ISO 3166-1 alpha-2 code, e.g. HU.");
        RuleFor(r => r.TimeZoneId).NotEmpty().MaximumLength(64);
        RuleFor(r => r.Latitude).InclusiveBetween(-90, 90);
        RuleFor(r => r.Longitude).InclusiveBetween(-180, 180);
    }
}

internal sealed class CreateScheduleRequestValidator : AbstractValidator<CreateScheduleRequest>
{
    public CreateScheduleRequestValidator()
    {
        RuleFor(r => r.FlightNumber).NotEmpty().MaximumLength(6);
        RuleFor(r => r.AirlineCode).NotEmpty().MaximumLength(3);
        RuleFor(r => r.OriginIata).NotEmpty().Length(3);
        RuleFor(r => r.DestinationIata).NotEmpty().Length(3)
            .NotEqual(r => r.OriginIata, StringComparer.OrdinalIgnoreCase).WithMessage("Origin and destination must differ.");
        RuleFor(r => r.AircraftCode).NotEmpty().MaximumLength(8);
        RuleFor(r => r.DurationMinutes).InclusiveBetween(20, 1200);
        RuleFor(r => r.OperatingDays).Cascade(CascadeMode.Stop).NotEmpty()
            .Must(d => d.All(Enum.IsDefined)).WithMessage("Unknown day of week.");
        RuleFor(r => r.ValidTo).GreaterThanOrEqualTo(r => r.ValidFrom)
            .Must((r, to) => to.DayNumber - r.ValidFrom.DayNumber <= AdminRules.MaxScheduleDays)
            .WithMessage($"A schedule can span at most {AdminRules.MaxScheduleDays} days.");
        RuleFor(r => r.BaseFare).ValidPrice();
        RuleFor(r => r.Currency).NotEmpty().Matches("^[A-Z]{3}$").WithMessage("An ISO 4217 code, e.g. EUR.");
    }
}

internal sealed class UpdateFareRequestValidator : AbstractValidator<UpdateFareRequest>
{
    public UpdateFareRequestValidator() => RuleFor(r => r.Price).ValidPrice();
}

internal sealed class CancelRequestValidator : AbstractValidator<CancelRequest>
{
    public CancelRequestValidator() => RuleFor(r => r.Reason).ValidReason();
}
