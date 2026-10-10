using SkyJourney.Domain.Common;

namespace SkyJourney.Domain.Flights;

/// <summary>
/// Deterministic demo pricing: base fare × fare-family multiplier × a pseudo-random but stable
/// demand factor for the (flight number, date) pair. Real pricing would come from a revenue
/// management system; this exists so search results show realistic-looking variation.
/// </summary>
public static class FarePricing
{
    public static decimal FamilyMultiplier(FareFamily family) => family switch
    {
        FareFamily.Basic => 1.00m,
        FareFamily.Plus => 1.55m,
        FareFamily.Flex => 2.20m,
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, null),
    };

    public static decimal DemandFactor(string flightNumber, DateOnly date)
    {
        var hash = StableHash.Fnv1A($"{flightNumber}|{date:yyyy-MM-dd}");
        var factor = 0.75m + hash % 86 / 100m; // 0.75 .. 1.60
        if (date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Sunday)
        {
            factor *= 1.15m;
        }

        return factor;
    }

    public static Money PriceFor(Money baseFare, FareFamily family, string flightNumber, DateOnly date)
    {
        var raw = baseFare.Amount * DemandFactor(flightNumber, date) * FamilyMultiplier(family);
        return new Money(ToPricePoint(raw), baseFare.Currency);
    }

    /// <summary>Rounds to a retail price point ending in .99, with a floor of 9.99.</summary>
    public static decimal ToPricePoint(decimal amount) => Math.Max(9.99m, decimal.Floor(amount) + 0.99m);
}
