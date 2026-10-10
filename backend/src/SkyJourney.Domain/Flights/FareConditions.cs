using SkyJourney.Domain.Common;

namespace SkyJourney.Domain.Flights;

/// <summary>
/// What a fare includes. Copied onto each booking segment at booking time so later fare
/// changes never alter what a customer already bought.
/// </summary>
public sealed record FareConditions
{
    public FareConditions(bool includesCabinTrolley, int checkedBaggageKg, bool includesSeatSelection, int refundPercentage)
    {
        if (checkedBaggageKg is < 0 or > 64 || refundPercentage is < 0 or > 100)
        {
            throw new DomainException("invalid_fare_conditions", "Fare conditions are out of range.");
        }

        IncludesCabinTrolley = includesCabinTrolley;
        CheckedBaggageKg = checkedBaggageKg;
        IncludesSeatSelection = includesSeatSelection;
        RefundPercentage = refundPercentage;
    }

    /// <summary>A small under-seat bag is always included; this adds a 10 kg overhead-bin trolley.</summary>
    public bool IncludesCabinTrolley { get; }

    public int CheckedBaggageKg { get; }

    public bool IncludesSeatSelection { get; }

    /// <summary>Share of the fare refunded on an eligible cancellation.</summary>
    public int RefundPercentage { get; }

    public static FareConditions For(FareFamily family) => family switch
    {
        FareFamily.Basic => new FareConditions(false, 0, false, 0),
        FareFamily.Plus => new FareConditions(true, 20, false, 50),
        FareFamily.Flex => new FareConditions(true, 23, true, 100),
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, null),
    };
}
