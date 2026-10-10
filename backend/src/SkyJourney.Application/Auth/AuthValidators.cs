using FluentValidation;

namespace SkyJourney.Application.Auth;

public static class AccountRules
{
    public const int EmailMaxLength = 254;
    public const int PasswordMinLength = 10;
    public const int PasswordMaxLength = 128;
    public const int NameMaxLength = 50;

    public static IRuleBuilderOptions<T, string> ValidName<T>(this IRuleBuilder<T, string> rule) => rule
        .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Required.")
        .MaximumLength(NameMaxLength);
}

internal sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().MaximumLength(AccountRules.EmailMaxLength).EmailAddress();
        // Composition rules (upper case, lower case, digit) are enforced by Identity's password validator.
        RuleFor(r => r.Password).NotEmpty()
            .MinimumLength(AccountRules.PasswordMinLength)
            .MaximumLength(AccountRules.PasswordMaxLength);
        RuleFor(r => r.FirstName).ValidName();
        RuleFor(r => r.LastName).ValidName();
    }
}

internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().MaximumLength(AccountRules.EmailMaxLength);
        RuleFor(r => r.Password).NotEmpty().MaximumLength(AccountRules.PasswordMaxLength);
    }
}

internal sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() => RuleFor(r => r.RefreshToken).NotEmpty().MaximumLength(200);
}

internal sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator() => RuleFor(r => r.RefreshToken).NotEmpty().MaximumLength(200);
}

internal sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(r => r.FirstName).ValidName();
        RuleFor(r => r.LastName).ValidName();
    }
}
