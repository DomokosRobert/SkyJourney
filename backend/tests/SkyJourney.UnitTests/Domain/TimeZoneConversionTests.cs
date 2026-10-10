using SkyJourney.Domain.Common;

namespace SkyJourney.UnitTests.Domain;

public sealed class TimeZoneConversionTests
{
    private static readonly TimeZoneInfo Budapest = TimeZoneConversion.Find("Europe/Budapest");

    [Fact]
    public void Summer_time_uses_utc_plus_two()
    {
        var utc = TimeZoneConversion.ToUtc(new DateOnly(2026, 7, 1), new TimeOnly(7, 0), Budapest);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 5, 0, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public void Winter_time_uses_utc_plus_one()
    {
        var utc = TimeZoneConversion.ToUtc(new DateOnly(2026, 12, 1), new TimeOnly(7, 0), Budapest);
        Assert.Equal(new DateTimeOffset(2026, 12, 1, 6, 0, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public void Time_in_spring_forward_gap_is_moved_past_the_gap()
    {
        // 29 March 2026: clocks jump from 02:00 to 03:00 in Budapest; 02:30 does not exist.
        var utc = TimeZoneConversion.ToUtc(new DateOnly(2026, 3, 29), new TimeOnly(2, 30), Budapest);
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public void Ambiguous_autumn_time_resolves_to_daylight_time()
    {
        // 25 October 2026: 02:30 happens twice in Budapest; take the first (UTC+2).
        var utc = TimeZoneConversion.ToUtc(new DateOnly(2026, 10, 25), new TimeOnly(2, 30), Budapest);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public void Unknown_time_zone_is_a_domain_error()
    {
        var ex = Assert.Throws<DomainException>(() => TimeZoneConversion.Find("Mars/Olympus_Mons"));
        Assert.Equal("invalid_time_zone", ex.Code);
    }
}
