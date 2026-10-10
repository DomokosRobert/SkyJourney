using SkyJourney.Domain.Airports;
using SkyJourney.Domain.Common;
using SkyJourney.Domain.Fleet;

namespace SkyJourney.Domain.Flights;

/// <summary>A concrete, dated flight (a "flight instance").</summary>
public sealed class Flight : Entity
{
    /// <summary>Online sales close this long before departure.</summary>
    public static readonly TimeSpan SalesCutoff = TimeSpan.FromHours(2);

    private readonly List<Fare> _fares = [];

    private Flight()
    {
    }

    public string FlightNumber { get; private set; } = null!;

    public Guid AirlineId { get; private set; }

    public Airline Airline { get; private set; } = null!;

    public Guid? FlightScheduleId { get; private set; }

    public Guid OriginAirportId { get; private set; }

    public Airport Origin { get; private set; } = null!;

    public Guid DestinationAirportId { get; private set; }

    public Airport Destination { get; private set; } = null!;

    public Guid AircraftTypeId { get; private set; }

    public AircraftType AircraftType { get; private set; } = null!;

    public DateTimeOffset DepartureUtc { get; private set; }

    public DateTimeOffset ArrivalUtc { get; private set; }

    /// <summary>Departure date in the origin airport's time zone; what users search by.</summary>
    public DateOnly DepartureLocalDate { get; private set; }

    public int Capacity { get; private set; }

    /// <summary>Passengers holding or owning a place on this flight. Only changed through atomic SQL updates.</summary>
    public int SeatsSold { get; private set; }

    public FlightStatus Status { get; private set; }

    public IReadOnlyCollection<Fare> Fares => _fares;

    public TimeSpan Duration => ArrivalUtc - DepartureUtc;

    public int SeatsAvailable => Capacity - SeatsSold;

    public static Flight Create(string flightNumber, Airline airline, Airport origin, Airport destination,
        AircraftType aircraft, DateOnly localDate, TimeOnly localDepartureTime, TimeSpan duration,
        Money baseFare, Guid? scheduleId = null)
    {
        if (origin.Id == destination.Id)
        {
            throw new DomainException("invalid_route", "Origin and destination must differ.");
        }

        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromHours(20))
        {
            throw new DomainException("invalid_duration", "Flight duration must be between 1 minute and 20 hours.");
        }

        var departureUtc = TimeZoneConversion.ToUtc(localDate, localDepartureTime, origin.TimeZone);
        var flight = new Flight
        {
            FlightNumber = flightNumber,
            AirlineId = airline.Id,
            Airline = airline,
            FlightScheduleId = scheduleId,
            OriginAirportId = origin.Id,
            Origin = origin,
            DestinationAirportId = destination.Id,
            Destination = destination,
            AircraftTypeId = aircraft.Id,
            AircraftType = aircraft,
            DepartureUtc = departureUtc,
            ArrivalUtc = departureUtc + duration,
            DepartureLocalDate = localDate,
            Capacity = aircraft.Capacity,
            Status = FlightStatus.Scheduled,
        };

        foreach (var family in Enum.GetValues<FareFamily>())
        {
            flight._fares.Add(new Fare(flight.Id, family, FarePricing.PriceFor(baseFare, family, flightNumber, localDate)));
        }

        return flight;
    }

    public bool IsOpenForSale(DateTimeOffset now) =>
        Status == FlightStatus.Scheduled && DepartureUtc - now >= SalesCutoff;

    public Fare GetFare(FareFamily family) =>
        _fares.SingleOrDefault(f => f.Family == family)
        ?? throw new DomainException("fare_not_found", $"Fare {family} is not offered on flight {FlightNumber}.");

    public void Cancel()
    {
        if (Status == FlightStatus.Cancelled)
        {
            throw new DomainException("flight_already_cancelled", $"Flight {FlightNumber} is already cancelled.");
        }

        Status = FlightStatus.Cancelled;
    }
}
