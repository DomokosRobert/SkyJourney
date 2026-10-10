using SkyJourney.Domain.Common;

namespace SkyJourney.UnitTests.Domain;

public sealed class MoneyTests
{
    [Fact]
    public void Rounds_to_cents_away_from_zero()
    {
        Assert.Equal(10.01m, new Money(10.005m, "EUR").Amount);
    }

    [Fact]
    public void Adding_different_currencies_is_rejected()
    {
        var ex = Assert.Throws<DomainException>(() => new Money(1, "EUR").Add(new Money(1, "HUF")));
        Assert.Equal("currency_mismatch", ex.Code);
    }

    [Theory]
    [InlineData("eur")]
    [InlineData("EURO")]
    [InlineData("")]
    public void Rejects_invalid_currency_codes(string currency)
    {
        Assert.Throws<DomainException>(() => new Money(1, currency));
    }

    [Fact]
    public void Rejects_negative_amounts()
    {
        Assert.Throws<DomainException>(() => new Money(-0.01m, "EUR"));
    }

    [Fact]
    public void Percent_rounds_to_cents()
    {
        Assert.Equal(new Money(33.33m, "EUR"), new Money(66.66m, "EUR").Percent(50));
    }
}
