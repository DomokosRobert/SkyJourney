using SkyJourney.Domain.Common;

namespace SkyJourney.Domain.Fleet;

public sealed class Airline : Entity
{
    private Airline()
    {
    }

    /// <summary>Two-character designator used as the flight-number prefix.</summary>
    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public static Airline Create(string code, string name)
    {
        if (code is not { Length: 2 } || !code.All(char.IsAsciiLetterOrDigit) || string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("invalid_airline", "An airline needs a 2-character code and a name.");
        }

        return new Airline { Code = code.ToUpperInvariant(), Name = name.Trim() };
    }
}
