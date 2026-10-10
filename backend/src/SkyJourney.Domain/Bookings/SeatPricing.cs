using SkyJourney.Domain.Common;
using SkyJourney.Domain.Fleet;
using SkyJourney.Domain.Flights;

namespace SkyJourney.Domain.Bookings;

public static class SeatPricing
{
    public static Money PriceFor(SeatCategory category, FareConditions conditions, string currency)
    {
        if (conditions.IncludesSeatSelection)
        {
            return Money.Zero(currency);
        }

        return new Money(category switch
        {
            SeatCategory.Standard => 6.99m,
            SeatCategory.Front => 9.99m,
            SeatCategory.ExtraLegroom => 15.99m,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
        }, currency);
    }
}
