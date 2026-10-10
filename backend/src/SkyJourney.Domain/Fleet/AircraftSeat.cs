using SkyJourney.Domain.Common;

namespace SkyJourney.Domain.Fleet;

public sealed class AircraftSeat : Entity
{
    private AircraftSeat()
    {
    }

    internal AircraftSeat(Guid aircraftTypeId, int row, char letter, SeatCategory category)
    {
        AircraftTypeId = aircraftTypeId;
        Row = row;
        Letter = letter.ToString();
        SeatNumber = $"{row}{letter}";
        Category = category;
    }

    public Guid AircraftTypeId { get; private set; }

    public int Row { get; private set; }

    public string Letter { get; private set; } = null!;

    /// <summary>Row + letter, e.g. <c>12C</c>.</summary>
    public string SeatNumber { get; private set; } = null!;

    public SeatCategory Category { get; private set; }
}
