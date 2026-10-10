using System.Security.Cryptography;

namespace SkyJourney.Domain.Bookings;

/// <summary>Six-character, PNR-style public booking identifier (e.g. <c>K7Q2XM</c>).</summary>
public static class BookingReference
{
    // No 0/O, 1/I/L: references get read out over the phone and typed by hand.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public const int Length = 6;

    public static string Generate() => new(RandomNumberGenerator.GetItems<char>(Alphabet, Length));

    public static bool IsWellFormed(string? value) =>
        value is { Length: Length } && value.All(c => Alphabet.Contains(c, StringComparison.Ordinal));
}
