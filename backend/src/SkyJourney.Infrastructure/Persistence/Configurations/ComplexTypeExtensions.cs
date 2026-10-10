using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyJourney.Domain.Common;
using SkyJourney.Domain.Flights;

namespace SkyJourney.Infrastructure.Persistence.Configurations;

internal static class ComplexTypeExtensions
{
    public static void ConfigureMoney(this ComplexPropertyBuilder<Money> money)
    {
        money.Property(m => m.Amount).HasPrecision(10, 2);
        money.Property(m => m.Currency).HasMaxLength(3).IsFixedLength();
    }

    public static void ConfigureConditions(this ComplexPropertyBuilder<FareConditions> conditions)
    {
        conditions.Property(c => c.IncludesCabinTrolley);
        conditions.Property(c => c.CheckedBaggageKg);
        conditions.Property(c => c.IncludesSeatSelection);
        conditions.Property(c => c.RefundPercentage);
    }
}
