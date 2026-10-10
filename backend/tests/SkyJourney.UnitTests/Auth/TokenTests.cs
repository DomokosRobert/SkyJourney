using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using SkyJourney.Infrastructure.Identity;

namespace SkyJourney.UnitTests.Auth;

public sealed class TokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Missing_signing_key_is_rejected_outside_development()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => JwtSigningKey.Create(new JwtOptions(), allowEphemeral: false));

        Assert.Contains("Jwt:SigningKey", ex.Message);
    }

    [Fact]
    public void Missing_signing_key_falls_back_to_a_random_key_in_development()
    {
        var first = JwtSigningKey.Create(new JwtOptions(), allowEphemeral: true);
        var second = JwtSigningKey.Create(new JwtOptions(), allowEphemeral: true);

        Assert.True(first.IsEphemeral);
        Assert.NotEqual(first.Key.Key, second.Key.Key);
    }

    [Fact]
    public void Short_signing_keys_are_rejected_even_in_development() =>
        Assert.Throws<InvalidOperationException>(() =>
            JwtSigningKey.Create(new JwtOptions { SigningKey = "too-short" }, allowEphemeral: true));

    [Fact]
    public void Refresh_tokens_are_random_and_stored_as_sha256_hex()
    {
        var a = RefreshTokenSecret.Generate();
        var b = RefreshTokenSecret.Generate();

        Assert.NotEqual(a, b);
        Assert.Equal(43, a.Length); // 32 bytes, base64url without padding
        Assert.Matches("^[0-9a-f]{64}$", RefreshTokenSecret.Hash(a));
        Assert.Equal(RefreshTokenSecret.Hash(a), RefreshTokenSecret.Hash(a));
        Assert.NotEqual(RefreshTokenSecret.Hash(a), RefreshTokenSecret.Hash(b));
    }

    [Fact]
    public void Access_token_carries_subject_email_roles_and_lifetime()
    {
        var options = new JwtOptions { SigningKey = new string('k', 32), AccessTokenMinutes = 15 };
        var issuer = new AccessTokenIssuer(Options.Create(options), JwtSigningKey.Create(options, false), new FakeTimeProvider(Now));
        var user = new ApplicationUser { Email = "ada@example.com" };

        var (token, expiresAt) = issuer.Issue(user, ["Admin"]);
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

        Assert.Equal(Now.AddMinutes(15), expiresAt);
        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal("ada@example.com", jwt.GetClaim("email").Value);
        Assert.Equal("Admin", jwt.GetClaim(ClaimNames.Role).Value);
        Assert.Equal("skyjourney-api", jwt.Issuer);
        Assert.Equal(["skyjourney-mobile"], jwt.Audiences);
        Assert.Equal(Now.UtcDateTime, jwt.ValidFrom);
        Assert.Equal(expiresAt.UtcDateTime, jwt.ValidTo);
        Assert.Equal("HS256", jwt.Alg);
    }
}
