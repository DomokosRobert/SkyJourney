using SkyJourney.Domain.Common;

namespace SkyJourney.Domain.Flights;

/// <summary>A fare family offered on a specific flight, with its current price.</summary>
public sealed class Fare : Entity
{
    private Fare()
    {
    }

    internal Fare(Guid flightId, FareFamily family, Money price)
    {
        FlightId = flightId;
        Family = family;
        Price = price;
        Conditions = FareConditions.For(family);
        IsAvailable = true;
    }

    public Guid FlightId { get; private set; }

    public FareFamily Family { get; private set; }

    /// <summary>Price per passenger for this flight.</summary>
    public Money Price { get; private set; } = null!;

    public FareConditions Conditions { get; private set; } = null!;

    public bool IsAvailable { get; private set; }

    public void ChangePrice(Money price)
    {
        if (price.Currency != Price.Currency)
        {
            throw new DomainException("currency_mismatch", "A fare's currency cannot be changed.");
        }

        Price = price;
    }

    public void SetAvailability(bool isAvailable) => IsAvailable = isAvailable;
}
