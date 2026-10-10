using SkyJourney.Application.Common;
using SkyJourney.Application.Flights;
using SkyJourney.Domain.Bookings;
using SkyJourney.Domain.Fleet;
using SkyJourney.Domain.Flights;

namespace SkyJourney.Application.Bookings;

public sealed record FlightSelectionRequest(Guid FlightId, FareFamily FareFamily);

public sealed record PassengerRequest(string FirstName, string LastName, PassengerType Type);

/// <summary>
/// Body of <c>POST /api/v1/bookings</c>. Only identifiers and choices: every price is computed on the server.
/// </summary>
public sealed record CreateBookingRequest(
    FlightSelectionRequest Outbound,
    FlightSelectionRequest? Return,
    IReadOnlyList<PassengerRequest> Passengers);

public sealed record SeatAssignmentRequest(Guid PassengerId, string SeatNumber);

/// <summary>
/// Body of <c>PUT …/segments/{segmentId}/seats</c>. Sets the seats of the listed passengers on that segment;
/// passengers not listed keep their current seat (or none).
/// </summary>
public sealed record SelectSeatsRequest(IReadOnlyList<SeatAssignmentRequest> Seats);

public sealed record PassengerDto(Guid Id, int Sequence, string FirstName, string LastName, PassengerType Type);

public sealed record SeatAssignmentDto(Guid PassengerId, string SeatNumber, MoneyDto Price);

public sealed record SegmentFlightDto(
    Guid FlightId,
    string FlightNumber,
    AirlineDto Airline,
    AirportDto Origin,
    AirportDto Destination,
    DateTimeOffset DepartureLocal,
    DateTimeOffset DepartureUtc,
    DateTimeOffset ArrivalLocal,
    DateTimeOffset ArrivalUtc,
    int DurationMinutes,
    string AircraftName,
    FlightStatus Status);

public sealed record BookingSegmentDto(
    Guid Id,
    JourneyDirection Direction,
    SegmentFlightDto Flight,
    FareFamily FareFamily,
    MoneyDto FarePricePerPassenger,
    FareConditionsDto FareConditions,
    IReadOnlyList<SeatAssignmentDto> Seats);

public sealed record BookingDto(
    Guid Id,
    string Reference,
    BookingStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? CancelledAt,
    MoneyDto TotalPrice,
    MoneyDto RefundedAmount,
    IReadOnlyList<PassengerDto> Passengers,
    IReadOnlyList<BookingSegmentDto> Segments,
    string DataSource);

public sealed record BookingSummaryDto(
    Guid Id,
    string Reference,
    BookingStatus Status,
    DateTimeOffset CreatedAt,
    MoneyDto TotalPrice,
    int PassengerCount,
    AirportDto Origin,
    AirportDto Destination,
    DateTimeOffset DepartureLocal,
    DateTimeOffset DepartureUtc,
    DateTimeOffset? ReturnDepartureLocal,
    bool IsUpcoming);

/// <summary>The outcome of a create call: the booking, and whether this call created it or replayed an earlier one.</summary>
public sealed record CreateBookingResult(BookingDto Booking, bool Created);

public enum SeatAvailability
{
    Available,
    Occupied,

    /// <summary>Held or confirmed for a passenger of the booking being viewed.</summary>
    Selected,
}

public sealed record SeatMapSeatDto(
    string SeatNumber,
    int Row,
    string Letter,
    SeatCategory Category,
    MoneyDto Price,
    SeatAvailability Availability,
    Guid? PassengerId);

public sealed record SeatMapDto(
    Guid SegmentId,
    string FlightNumber,
    string AircraftName,
    int Rows,
    string SeatLetters,
    IReadOnlyList<SeatMapSeatDto> Seats);

/// <summary>What cancelling now would do. <see cref="Refund"/> is null when cancellation is not allowed.</summary>
public sealed record CancellationQuoteDto(bool CanCancel, MoneyDto? Refund, string? ReasonCode, string? Reason);
