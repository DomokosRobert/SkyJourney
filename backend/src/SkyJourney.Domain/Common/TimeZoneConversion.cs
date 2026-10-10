namespace SkyJourney.Domain.Common;

public static class TimeZoneConversion
{
    public static TimeZoneInfo Find(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new DomainException("invalid_time_zone", $"'{timeZoneId}' is not a known IANA time zone.");
        }
    }

    /// <summary>
    /// Converts an airport-local wall-clock time to a UTC instant, resolving DST edge cases:
    /// a time inside the spring-forward gap is moved past the gap; an ambiguous autumn time
    /// resolves to its first occurrence (daylight time), as airline schedules conventionally do.
    /// </summary>
    public static DateTimeOffset ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(local))
        {
            var rule = zone.GetAdjustmentRules().FirstOrDefault(r => r.DateStart <= local && r.DateEnd >= local);
            local = local.Add(rule?.DaylightDelta ?? TimeSpan.FromHours(1));
        }

        var offset = zone.IsAmbiguousTime(local)
            ? zone.GetAmbiguousTimeOffsets(local).Max()
            : zone.GetUtcOffset(local);

        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    public static DateTimeOffset ToLocal(DateTimeOffset instant, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(instant, zone);
}
