using SkyJourney.Domain.Common;

namespace SkyJourney.Domain.Bookings;

/// <summary>Deliberately minimal personal data: only what a boarding pass needs.</summary>
public sealed class BookingPassenger : Entity
{
    public const int MaxNameLength = 50;

    private BookingPassenger()
    {
    }

    internal BookingPassenger(Guid bookingId, int sequence, string firstName, string lastName, PassengerType type)
    {
        BookingId = bookingId;
        Sequence = sequence;
        FirstName = NormalizeName(firstName);
        LastName = NormalizeName(lastName);
        Type = type;
    }

    public Guid BookingId { get; private set; }

    /// <summary>1-based position in the booking, for stable ordering.</summary>
    public int Sequence { get; private set; }

    public string FirstName { get; private set; } = null!;

    public string LastName { get; private set; } = null!;

    public PassengerType Type { get; private set; }

    private static string NormalizeName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > MaxNameLength)
        {
            throw new DomainException("invalid_passenger", $"Passenger names must be 1-{MaxNameLength} characters.");
        }

        return trimmed;
    }
}
