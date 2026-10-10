using SkyJourney.Application.Bookings;
using SkyJourney.Application.Common;
using SkyJourney.Application.Flights;
using SkyJourney.Domain.Airports;
using SkyJourney.Domain.Bookings;
using SkyJourney.Domain.Flights;

namespace SkyJourney.Application.Admin;

// Airports

public sealed record AdminAirportDto(
    string IataCode, string Name, string City, string CountryCode, string TimeZoneId, double Latitude, double Longitude, bool IsActive)
{
    public static AdminAirportDto From(Airport a) =>
        new(a.IataCode, a.Name, a.City, a.CountryCode, a.TimeZoneId, a.Latitude, a.Longitude, a.IsActive);
}

/// <summary>Body of <c>POST /api/v1/admin/airports</c>. New airports start active.</summary>
public sealed record CreateAirportRequest(
    string IataCode, string Name, string City, string CountryCode, string TimeZoneId, double Latitude, double Longitude);

/// <summary>Body of <c>PUT /api/v1/admin/airports/{iataCode}</c>. The IATA code itself cannot change.</summary>
public sealed record UpdateAirportRequest(
    string Name, string City, string CountryCode, string TimeZoneId, double Latitude, double Longitude);

// Schedules

public sealed record AdminScheduleDto(
    Guid Id,
    string FlightNumber,
    AirlineDto Airline,
    AirportDto Origin,
    AirportDto Destination,
    string AircraftCode,
    TimeOnly DepartureLocalTime,
    int DurationMinutes,
    IReadOnlyList<DayOfWeek> OperatingDays,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    MoneyDto BaseFare);

/// <summary>
/// Body of <c>POST /api/v1/admin/schedules</c>. <see cref="BaseFare"/> is the Basic fare before demand adjustment;
/// the other fare families and per-date prices are derived from it (see FarePricing).
/// </summary>
public sealed record CreateScheduleRequest(
    string FlightNumber,
    string AirlineCode,
    string OriginIata,
    string DestinationIata,
    string AircraftCode,
    TimeOnly DepartureLocalTime,
    int DurationMinutes,
    IReadOnlyList<DayOfWeek> OperatingDays,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    decimal BaseFare,
    string Currency);

public sealed record CreateScheduleResult(AdminScheduleDto Schedule, int GeneratedFlights);

// Flights and fares

public sealed record AdminFareDto(FareFamily Family, MoneyDto Price, bool IsAvailable, FareConditionsDto Conditions);

public sealed record AdminFlightDto(
    Guid Id,
    string FlightNumber,
    Guid? ScheduleId,
    AirlineDto Airline,
    AirportDto Origin,
    AirportDto Destination,
    DateTimeOffset DepartureLocal,
    DateTimeOffset DepartureUtc,
    DateTimeOffset ArrivalLocal,
    DateTimeOffset ArrivalUtc,
    string AircraftName,
    FlightStatus Status,
    int Capacity,
    int SeatsSold,
    IReadOnlyList<AdminFareDto> Fares);

public sealed record AdminFlightQuery(
    DateOnly? Date, string? Origin, string? Destination, string? FlightNumber, FlightStatus? Status, int? Page, int? PageSize);

/// <summary>
/// Body of <c>PUT /api/v1/admin/flights/{id}/fares/{family}</c>. Only affects bookings created afterwards:
/// existing bookings keep the price they were sold at.
/// </summary>
public sealed record UpdateFareRequest(decimal Price, bool IsAvailable);

public sealed record CancelRequest(string Reason);

/// <summary>A booking on a cancelled flight that could not be cancelled yet (e.g. a payment was in progress).</summary>
public sealed record SkippedBooking(string Reference, string Code, string Reason);

public sealed record FlightCancellationResultDto(
    AdminFlightDto Flight,
    int CancelledBookings,
    IReadOnlyList<MoneyDto> Refunded,
    IReadOnlyList<SkippedBooking> Skipped);

// Bookings

public sealed record AdminBookingQuery(string? Reference, Guid? FlightId, Guid? UserId, BookingStatus? Status, int? Page, int? PageSize);

public sealed record AdminBookingSummaryDto(
    Guid Id,
    string Reference,
    Guid UserId,
    BookingStatus Status,
    DateTimeOffset CreatedAt,
    MoneyDto TotalPrice,
    MoneyDto RefundedAmount,
    int PassengerCount,
    IReadOnlyList<string> FlightNumbers,
    DateTimeOffset FirstDepartureUtc);

public sealed record BookingHistoryDto(BookingStatus? From, BookingStatus To, BookingActor Actor, string? Reason, DateTimeOffset At);

public sealed record AdminPaymentDto(
    Guid Id,
    PaymentStatus Status,
    MoneyDto Amount,
    MoneyDto RefundedAmount,
    string Provider,
    string? ProviderReference,
    string? FailureCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

/// <summary>A booking as support staff see it: the customer view plus owner, status history and payment attempts.</summary>
public sealed record AdminBookingDto(
    BookingDto Booking,
    Guid UserId,
    IReadOnlyList<BookingHistoryDto> History,
    IReadOnlyList<AdminPaymentDto> Payments);
