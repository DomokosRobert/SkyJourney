using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace SkyJourney.Infrastructure.Identity;

/// <summary>Creates short-lived HMAC-SHA256 JWT access tokens (claims: sub, email, role, jti).</summary>
public sealed class AccessTokenIssuer(IOptions<JwtOptions> options, JwtSigningKey signingKey, TimeProvider timeProvider)
{
    private static readonly JsonWebTokenHandler Handler = new() { SetDefaultTimesOnTokenCreation = false };

    public (string Token, DateTimeOffset ExpiresAt) Issue(ApplicationUser user, IEnumerable<string> roles)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(options.Value.AccessTokenMinutes);

        var identity = new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            .. roles.Select(role => new Claim(ClaimNames.Role, role)),
        ]);

        var token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Value.Issuer,
            Audience = options.Value.Audience,
            Subject = identity,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(signingKey.Key, SecurityAlgorithms.HmacSha256),
        });

        return (token, expiresAt);
    }
}

public static class ClaimNames
{
    public const string Role = "role";
}
