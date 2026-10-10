namespace SkyJourney.Domain.Flights;

[Flags]
public enum OperatingDays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    Daily = Monday | Tuesday | Wednesday | Thursday | Friday | Saturday | Sunday,
}

public static class OperatingDaysExtensions
{
    public static bool Includes(this OperatingDays days, DayOfWeek dayOfWeek) => (days & ToFlag(dayOfWeek)) != 0;

    public static OperatingDays ToFlag(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => OperatingDays.Monday,
        DayOfWeek.Tuesday => OperatingDays.Tuesday,
        DayOfWeek.Wednesday => OperatingDays.Wednesday,
        DayOfWeek.Thursday => OperatingDays.Thursday,
        DayOfWeek.Friday => OperatingDays.Friday,
        DayOfWeek.Saturday => OperatingDays.Saturday,
        DayOfWeek.Sunday => OperatingDays.Sunday,
        _ => throw new ArgumentOutOfRangeException(nameof(dayOfWeek), dayOfWeek, null),
    };
}
