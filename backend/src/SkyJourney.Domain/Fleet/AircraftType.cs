using SkyJourney.Domain.Common;

namespace SkyJourney.Domain.Fleet;

/// <summary>An aircraft model together with its single-cabin seat configuration.</summary>
public sealed class AircraftType : Entity
{
    private readonly List<AircraftSeat> _seats = [];

    private AircraftType()
    {
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    /// <summary>Seat letters left to right, e.g. <c>ABCDEF</c>; the aisle sits in the middle.</summary>
    public string SeatLetters { get; private set; } = null!;

    public int Rows { get; private set; }

    public IReadOnlyCollection<AircraftSeat> Seats => _seats;

    public int Capacity => Rows * SeatLetters.Length;

    public static AircraftType Create(string code, string name, int rows, string seatLetters,
        int frontRows, IReadOnlyCollection<int> extraLegroomRows)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("invalid_aircraft", "Aircraft code and name are required.");
        }

        if (rows is < 1 or > 60 || seatLetters.Length is < 2 or > 10 || seatLetters.Distinct().Count() != seatLetters.Length)
        {
            throw new DomainException("invalid_aircraft", "Invalid seat layout.");
        }

        var aircraft = new AircraftType { Code = code.Trim().ToUpperInvariant(), Name = name.Trim(), Rows = rows, SeatLetters = seatLetters };
        for (var row = 1; row <= rows; row++)
        {
            var category = extraLegroomRows.Contains(row) ? SeatCategory.ExtraLegroom
                : row <= frontRows ? SeatCategory.Front
                : SeatCategory.Standard;

            foreach (var letter in seatLetters)
            {
                aircraft._seats.Add(new AircraftSeat(aircraft.Id, row, letter, category));
            }
        }

        return aircraft;
    }
}
