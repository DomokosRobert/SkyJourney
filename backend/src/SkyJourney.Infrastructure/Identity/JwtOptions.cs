using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SkyJourney.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Minimum HMAC-SHA256 key size (256 bits).</summary>
    public const int MinimumKeyBytes = 32;

    [Required]
    public string Issuer { get; init; } = "skyjourney-api";

    [Required]
    public string Audience { get; init; } = "skyjourney-mobile";

    /// <summary>
    /// Symmetric signing key (at least 32 bytes of UTF-8). Supplied through an environment variable or
    /// a secret store in production. Never commit a real value.
    /// </summary>
    public string? SigningKey { get; init; }

    [Range(1, 60)]
    public int AccessTokenMinutes { get; init; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; init; } = 30;
}

/// <summary>The resolved key used both to sign and to validate access tokens.</summary>
public sealed class JwtSigningKey(SymmetricSecurityKey key, bool isEphemeral)
{
    public SymmetricSecurityKey Key { get; } = key;

    /// <summary>True when no key was configured and a random per-process key was generated (development/testing only).</summary>
    public bool IsEphemeral { get; } = isEphemeral;

    public static JwtSigningKey Create(JwtOptions options, bool allowEphemeral)
    {
        if (string.IsNullOrEmpty(options.SigningKey))
        {
            if (!allowEphemeral)
            {
                throw new InvalidOperationException(
                    $"'{JwtOptions.SectionName}:SigningKey' must be configured (at least {JwtOptions.MinimumKeyBytes} bytes).");
            }

            return new JwtSigningKey(new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64)), isEphemeral: true);
        }

        var bytes = Encoding.UTF8.GetBytes(options.SigningKey);
        if (bytes.Length < JwtOptions.MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                $"'{JwtOptions.SectionName}:SigningKey' is too short: at least {JwtOptions.MinimumKeyBytes} bytes are required.");
        }

        return new JwtSigningKey(new SymmetricSecurityKey(bytes), isEphemeral: false);
    }
}
