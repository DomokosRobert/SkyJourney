using FluentValidation;
using SkyJourney.Application.Bookings;
using SkyJourney.Domain.Bookings;
using SkyJourney.Domain.Flights;

namespace SkyJourney.UnitTests.Bookings;

public sealed class BookingValidatorTests
{
    private static readonly Guid Outbound = Guid.CreateVersion7();
    private static readonly Guid Inbound = Guid.CreateVersion7();

    private readonly CreateBookingRequestValidator _create = new();
    private readonly SelectSeatsRequestValidator _seats = new();

    private static PassengerRequest Adult(string first = "Ada") => new(first, "Lovelace", PassengerType.Adult);

    private static CreateBookingRequest Valid(params PassengerRequest[] passengers) =>
        new(new FlightSelectionRequest(Outbound, FareFamily.Plus), new FlightSelectionRequest(Inbound, FareFamily.Plus),
            passengers.Length == 0 ? [Adult()] : passengers);

    [Fact]
    public void Valid_return_booking_passes() => Assert.True(_create.Validate(Valid()).IsValid);

    [Fact]
    public void One_way_booking_passes() => Assert.True(_create.Validate(Valid() with { Return = null }).IsValid);

    [Fact]
    public void Return_flight_must_differ_from_outbound()
    {
        var request = Valid() with { Return = new FlightSelectionRequest(Outbound, FareFamily.Plus) };

        Assert.False(_create.Validate(request).IsValid);
    }

    [Fact]
    public void At_least_one_adult_is_required()
    {
        var result = _create.Validate(Valid(new PassengerRequest("Tom", "Thumb", PassengerType.Child)));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateBookingRequest.Passengers));
    }

    [Fact]
    public void Passenger_count_is_capped()
    {
        var passengers = Enumerable.Range(0, Booking.MaxPassengers + 1).Select(i => Adult($"Ada{(char)('a' + i)}")).ToArray();

        Assert.False(_create.Validate(Valid(passengers)).IsValid);
    }

    [Fact]
    public void Unknown_fare_family_is_rejected()
    {
        var request = Valid() with { Outbound = new FlightSelectionRequest(Outbound, (FareFamily)99) };

        Assert.False(_create.Validate(request).IsValid);
    }

    [Fact]
    public void Passenger_names_are_validated() => Assert.False(_create.Validate(Valid(Adult(" "))).IsValid);

    [Theory]
    [InlineData("12C", true)]
    [InlineData("1a", true)]
    [InlineData("A1", false)]
    [InlineData("123C", false)]
    [InlineData("", false)]
    public void Seat_numbers_must_be_row_then_letter(string seat, bool valid)
    {
        var request = new SelectSeatsRequest([new SeatAssignmentRequest(Guid.CreateVersion7(), seat)]);

        Assert.Equal(valid, _seats.Validate(request).IsValid);
    }

    [Fact]
    public void A_passenger_may_be_seated_only_once()
    {
        var passenger = Guid.CreateVersion7();

        Assert.False(_seats.Validate(new SelectSeatsRequest(
            [new SeatAssignmentRequest(passenger, "1A"), new SeatAssignmentRequest(passenger, "1B")])).IsValid);
    }

    [Fact]
    public void A_seat_may_be_requested_only_once_regardless_of_case()
    {
        Assert.False(_seats.Validate(new SelectSeatsRequest(
            [new SeatAssignmentRequest(Guid.CreateVersion7(), "1a"), new SeatAssignmentRequest(Guid.CreateVersion7(), "1A")])).IsValid);
    }

    [Fact]
    public void An_empty_seat_selection_is_rejected() => Assert.False(_seats.Validate(new SelectSeatsRequest([])).IsValid);

    [Theory]
    [InlineData("3f2a-key")]
    [InlineData("0193b3c2-8a7e-7c4d-9f00-1234567890ab")]
    public void Idempotency_key_accepts_visible_ascii(string key) => Assert.Equal(key, IdempotencyKey.Require(key));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("ünïcode")]
    public void Idempotency_key_rejects_missing_or_invalid_values(string? key)
    {
        var ex = Assert.Throws<ValidationException>(() => IdempotencyKey.Require(key));

        Assert.Contains(ex.Errors, e => e.PropertyName == IdempotencyKey.HeaderName);
    }

    [Fact]
    public void Idempotency_key_length_is_capped() =>
        Assert.Throws<ValidationException>(() => IdempotencyKey.Require(new string('k', IdempotencyKey.MaxLength + 1)));
}
