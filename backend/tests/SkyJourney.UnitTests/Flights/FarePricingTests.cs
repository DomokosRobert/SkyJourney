using SkyJourney.Domain.Common;
using SkyJourney.Domain.Flights;

namespace SkyJourney.UnitTests.Flights;

public sealed class FarePricingTests
{
    private static readonly Money BaseFare = new(80m, "EUR");

    [Fact]
    public void Price_is_stable_for_the_same_flight_and_date()
    {
        var date = new DateOnly(2026, 11, 2);

        Assert.Equal(FarePricing.PriceFor(BaseFare, FareFamily.Basic, "SJ100", date),
                     FarePricing.PriceFor(BaseFare, FareFamily.Basic, "SJ100", date));
    }

    [Fact]
    public void Higher_fare_families_cost_more()
    {
        var date = new DateOnly(2026, 11, 2);

        var basic = FarePricing.PriceFor(BaseFare, FareFamily.Basic, "SJ100", date).Amount;
        var plus = FarePricing.PriceFor(BaseFare, FareFamily.Plus, "SJ100", date).Amount;
        var flex = FarePricing.PriceFor(BaseFare, FareFamily.Flex, "SJ100", date).Amount;

        Assert.True(basic < plus && plus < flex);
    }

    [Fact]
    public void Prices_vary_between_dates()
    {
        var prices = Enumerable.Range(0, 14)
            .Select(d => FarePricing.PriceFor(BaseFare, FareFamily.Basic, "SJ100", new DateOnly(2026, 11, 1).AddDays(d)).Amount)
            .Distinct();

        Assert.True(prices.Count() > 5);
    }

    [Theory]
    [InlineData(123.45, 123.99)]
    [InlineData(123.00, 123.99)]
    [InlineData(2.50, 9.99)]
    public void Prices_round_to_retail_price_points(decimal raw, decimal expected) =>
        Assert.Equal(expected, FarePricing.ToPricePoint(raw));

    [Fact]
    public void Demand_factor_stays_within_bounds()
    {
        var factors = Enumerable.Range(0, 365)
            .Select(d => FarePricing.DemandFactor("SJ204", new DateOnly(2026, 1, 1).AddDays(d)))
            .ToList();

        Assert.All(factors, f => Assert.InRange(f, 0.75m, 1.60m * 1.15m));
    }
}
