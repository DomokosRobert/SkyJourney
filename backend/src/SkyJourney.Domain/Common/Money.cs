using System.Globalization;

namespace SkyJourney.Domain.Common;

/// <summary>An amount in a specific ISO 4217 currency. Always <see cref="decimal"/>, never floating point.</summary>
public sealed record Money
{
    public Money(decimal amount, string currency)
    {
        if (amount < 0)
        {
            throw new DomainException("invalid_amount", "Monetary amounts cannot be negative.");
        }

        if (currency is not { Length: 3 } || !currency.All(char.IsAsciiLetterUpper))
        {
            throw new DomainException("invalid_currency", $"'{currency}' is not an ISO 4217 currency code.");
        }

        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money Zero(string currency) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    /// <summary>Returns <paramref name="percentage"/> percent of this amount, rounded to cents.</summary>
    public Money Percent(int percentage) => new(Amount * percentage / 100m, Currency);

    public override string ToString() => $"{Amount.ToString("0.00", CultureInfo.InvariantCulture)} {Currency}";

    private void EnsureSameCurrency(Money other)
    {
        if (other.Currency != Currency)
        {
            throw new DomainException("currency_mismatch", $"Cannot combine {Currency} with {other.Currency}.");
        }
    }
}
