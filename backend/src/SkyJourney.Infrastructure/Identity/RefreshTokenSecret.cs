using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace SkyJourney.Infrastructure.Identity;

/// <summary>Generates opaque 256-bit refresh tokens and the SHA-256 hashes stored in their place.</summary>
public static class RefreshTokenSecret
{
    public static string Generate() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
