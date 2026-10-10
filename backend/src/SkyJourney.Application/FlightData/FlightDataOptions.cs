namespace SkyJourney.Application.FlightData;

public sealed class FlightDataOptions
{
    public const string SectionName = "FlightData";

    /// <summary>Import the provider's data set and generate flight instances when the API starts.</summary>
    public bool SeedOnStartup { get; init; }

    /// <summary>How many days ahead flight instances are generated from schedules.</summary>
    public int HorizonDays { get; init; } = 90;
}
