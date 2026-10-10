using SkyJourney.Application.Auth;

namespace SkyJourney.UnitTests.Auth;

public sealed class AuthValidatorTests
{
    private readonly RegisterRequestValidator _register = new();
    private readonly UpdateProfileRequestValidator _profile = new();

    private static RegisterRequest Valid() => new("ada@example.com", "Correct-Horse-9", "Ada", "Lovelace");

    [Fact]
    public void Valid_registration_passes() => Assert.True(_register.Validate(Valid()).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Email_must_look_like_an_address(string email)
    {
        var result = _register.Validate(Valid() with { Email = email });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterRequest.Email));
    }

    [Theory]
    [InlineData("Short1")]
    [InlineData("")]
    public void Password_must_meet_the_minimum_length(string password)
    {
        var result = _register.Validate(Valid() with { Password = password });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterRequest.Password));
    }

    [Fact]
    public void Password_length_is_capped() =>
        Assert.False(_register.Validate(Valid() with { Password = new string('A', AccountRules.PasswordMaxLength + 1) }).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Names_are_required(string name)
    {
        Assert.False(_register.Validate(Valid() with { FirstName = name }).IsValid);
        Assert.False(_profile.Validate(new UpdateProfileRequest("Ada", name)).IsValid);
    }

    [Fact]
    public void Names_are_capped_at_fifty_characters() =>
        Assert.False(_profile.Validate(new UpdateProfileRequest(new string('a', 51), "Lovelace")).IsValid);
}
