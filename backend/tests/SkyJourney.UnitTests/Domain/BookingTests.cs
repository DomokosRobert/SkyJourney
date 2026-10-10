using SkyJourney.Domain.Bookings;
using SkyJourney.Domain.Common;
using SkyJourney.Domain.Fleet;
using SkyJourney.Domain.Flights;

namespace SkyJourney.UnitTests.Domain;

public sealed class BookingTests
{
    private static readonly DateTimeOffset Now = TestData.Now;

    [Fact]
    public void New_booking_is_pending_payment_with_hold_and_server_side_total()
    {
        var (outbound, inbound) = TestData.RoundTrip();
        var booking = TestData.Booking(outbound, inbound, FareFamily.Plus, adults: 2, children: 1);

        var expected = outbound.GetFare(FareFamily.Plus).Price.Multiply(3).Add(inbound.GetFare(FareFamily.Plus).Price.Multiply(3));
        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
        Assert.Equal(expected, booking.TotalPrice);
        Assert.Equal(Now + Booking.HoldDuration, booking.ExpiresAt);
        Assert.Single(booking.History);
        Assert.True(BookingReference.IsWellFormed(booking.Reference));
    }

    [Fact]
    public void Booking_snapshots_the_fare_so_later_price_changes_do_not_affect_it()
    {
        var (outbound, _) = TestData.RoundTrip();
        var booking = TestData.Booking(outbound);
        var paid = booking.TotalPrice;

        outbound.GetFare(FareFamily.Basic).ChangePrice(new Money(999, "EUR"));

        Assert.Equal(paid, booking.TotalPrice);
        Assert.NotEqual(999m, booking.Segments.Single().FarePrice.Amount);
    }

    [Fact]
    public void Requires_an_adult()
    {
        var (outbound, _) = TestData.RoundTrip();
        var ex = Assert.Throws<DomainException>(() => Booking.Create(Guid.NewGuid(), "ABCDEF",
            [new SegmentSelection(outbound, JourneyDirection.Outbound, FareFamily.Basic)],
            [new PassengerDetails("Kid", "Solo", PassengerType.Child)], "k", "h", Now));
        Assert.Equal("adult_required", ex.Code);
    }

    [Fact]
    public void Rejects_more_passengers_than_seats_left()
    {
        var tinyAircraft = AircraftType.Create("T4", "Tiny", rows: 2, seatLetters: "AB", frontRows: 0, extraLegroomRows: []);
        var flight = TestData.Flight(TestData.Budapest(), TestData.London(), new DateOnly(2026, 11, 11), new TimeOnly(7, 5), tinyAircraft);

        var ex = Assert.Throws<DomainException>(() => TestData.Booking(flight, adults: 5));
        Assert.Equal("insufficient_capacity", ex.Code);
    }

    [Fact]
    public void Rejects_return_flight_that_is_not_the_reverse_route()
    {
        var (outbound, _) = TestData.RoundTrip();
        var other = TestData.Flight(TestData.Budapest(), TestData.London(), new DateOnly(2026, 11, 15), new TimeOnly(9, 0));
        var ex = Assert.Throws<DomainException>(() => TestData.Booking(outbound, other));
        Assert.Equal("invalid_itinerary", ex.Code);
    }

    [Fact]
    public void Rejects_return_departing_before_outbound_arrives()
    {
        var bud = TestData.Budapest();
        var ltn = TestData.London();
        var outbound = TestData.Flight(bud, ltn, new DateOnly(2026, 11, 11), new TimeOnly(7, 5));
        var tooEarly = TestData.Flight(ltn, bud, new DateOnly(2026, 11, 11), new TimeOnly(9, 0));
        var ex = Assert.Throws<DomainException>(() => TestData.Booking(outbound, tooEarly));
        Assert.Equal("invalid_itinerary", ex.Code);
    }

    [Fact]
    public void Rejects_flights_inside_the_sales_cutoff()
    {
        var soon = TestData.Flight(TestData.Budapest(), TestData.London(), new DateOnly(2026, 11, 1), new TimeOnly(11, 30));
        var ex = Assert.Throws<DomainException>(() => TestData.Booking(soon));
        Assert.Equal("flight_not_bookable", ex.Code);
    }

    [Fact]
    public void Holding_a_paid_seat_adds_its_price_to_the_total()
    {
        var (outbound, _) = TestData.RoundTrip();
        var booking = TestData.Booking(outbound);
        var before = booking.TotalPrice;
        var seat = outbound.AircraftType.Seats.First(s => s.Category == SeatCategory.ExtraLegroom);

        booking.HoldSeats(booking.Segments.Single().Id, [new SeatSelection(booking.Passengers.Single(), seat)], Now);

        Assert.Equal(before.Add(new Money(15.99m, "EUR")), booking.TotalPrice);
        Assert.Equal(SeatReservationStatus.Held, booking.Segments.Single().SeatReservations.Single().Status);
    }

    [Fact]
    public void Flex_fares_include_free_seat_selection()
    {
        var (outbound, _) = TestData.RoundTrip();
        var booking = TestData.Booking(outbound, family: FareFamily.Flex);
        var before = booking.TotalPrice;

        booking.HoldSeats(booking.Segments.Single().Id,
            [new SeatSelection(booking.Passengers.Single(), outbound.AircraftType.Seats.First())], Now);

        Assert.Equal(before, booking.TotalPrice);
    }

    [Fact]
    public void Releasing_seats_removes_them_from_the_total()
    {
        var (outbound, _) = TestData.RoundTrip();
        var booking = TestData.Booking(outbound);
        var before = booking.TotalPrice;
        var segment = booking.Segments.Single();
        var passenger = booking.Passengers.Single();
        booking.HoldSeats(segment.Id, [new SeatSelection(passenger, outbound.AircraftType.Seats.Last())], Now);

        booking.ReleaseSeats(segment.Id, [passenger.Id], Now);

        Assert.Equal(before, booking.TotalPrice);
        Assert.Empty(segment.ActiveSeatReservations);
    }

    [Fact]
    public void Cannot_hold_a_seat_from_a_different_aircraft()
    {
        var (outbound, _) = TestData.RoundTrip();
        var booking = TestData.Booking(outbound);
        var foreignSeat = AircraftType.Create("X1", "Other", 2, "AB", 0, []).Seats.First();

        var ex = Assert.Throws<DomainException>(() =>
            booking.HoldSeats(booking.Segments.Single().Id, [new SeatSelection(booking.Passengers.Single(), foreignSeat)], Now));
        Assert.Equal("seat_not_on_aircraft", ex.Code);
    }

    [Fact]
    public void Cannot_assign_the_same_seat_to_two_passengers()
    {
        var (outbound, _) = TestData.RoundTrip();
        var booking = TestData.Booking(outbound, adults: 2);
        var seat = outbound.AircraftType.Seats.First();
        var selections = booking.Passengers.Select(p => new SeatSelection(p, seat)).ToList();

        var ex = Assert.Throws<DomainException>(() => booking.HoldSeats(booking.Segments.Single().Id, selections, Now));
        Assert.Equal("duplicate_seat_selection", ex.Code);
    }

    [Fact]
    public void Successful_payment_confirms_booking_and_seats()
    {
        var (outbound, _) = TestData.RoundTrip();
        var booking = TestData.Booking(outbound);
        booking.HoldSeats(booking.Segments.Single().Id, [new SeatSelection(booking.Passengers.Single(), outbound.AircraftType.Seats.First())], Now);

        var attempt = booking.StartPayment("pay-1", "Simulated", Now);
        booking.CompletePayment(attempt, succeeded: true, "sim_123", null, Now.AddMinutes(1));

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(PaymentStatus.Succeeded, attempt.Status);
        Assert.Equal(booking.TotalPrice, attempt.Amount);
        Assert.Equal(SeatReservationStatus.Confirmed, booking.Segments.Single().SeatReservations.Single().Status);
    }

    [Fact]
    public void Failed_payment_keeps_booking_payable()
    {
        var (outbound, _) = TestData.RoundTrip();
        var booking = TestData.Booking(outbound);

        var failed = booking.StartPayment("pay-1", "Simulated", Now);
        booking.CompletePayment(failed, succeeded: false, null, "card_declined", Now);
        var retry = booking.StartPayment("pay-2", "Simulated", Now);

        Assert.Equal(PaymentStatus.Failed, failed.Status);
        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
        Assert.Equal(PaymentStatus.Pending, retry.Status);
    }

    [Fact]
    public void Cannot_pay_after_the_hold_expires()
    {
        var (outbound, _) = TestData.RoundTrip();
        var booking = TestData.Booking(outbound);

        var ex = Assert.Throws<DomainException>(() => booking.StartPayment("p", "Simulated", booking.ExpiresAt));
        Assert.Equal("booking_expired", ex.Code);
    }

    [Fact]
    public void Expiring_releases_seats_and_is_final()
    {
        var (outbound, _) = TestData.RoundTrip();
        var booking = TestData.Booking(outbound);
        booking.HoldSeats(booking.Segments.Single().Id, [new SeatSelection(booking.Passengers.Single(), outbound.AircraftType.Seats.First())], Now);

        booking.Expire(booking.ExpiresAt.AddSeconds(1));

        Assert.Equal(BookingStatus.Expired, booking.Status);
        Assert.All(booking.Segments.Single().SeatReservations, r => Assert.Equal(SeatReservationStatus.Released, r.Status));
        Assert.Throws<DomainException>(() => booking.Cancel(BookingActor.Customer, "late", booking.ExpiresAt.AddMinutes(1)));
    }

    [Fact]
    public void Cannot_expire_a_booking_whose_hold_is_still_valid()
    {
        var (outbound, _) = TestData.RoundTrip();
        var booking = TestData.Booking(outbound);
        Assert.Throws<DomainException>(() => booking.Expire(Now.AddMinutes(1)));
    }

    [Theory]
    [InlineData(FareFamily.Basic, 0)]
    [InlineData(FareFamily.Plus, 50)]
    [InlineData(FareFamily.Flex, 100)]
    public void Refund_follows_the_fare_family_refund_percentage(FareFamily family, int expectedPercentage)
    {
        var (outbound, inbound) = TestData.RoundTrip();
        var booking = ConfirmedBooking(outbound, inbound, family);
        var fares = booking.Segments.Aggregate(Money.Zero("EUR"), (sum, s) => sum.Add(s.FarePrice.Multiply(booking.PassengerCount)));

        var refund = booking.Cancel(BookingActor.Customer, "Change of plans", Now.AddDays(1));

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(fares.Percent(expectedPercentage).Amount, refund.Amount, precision: 1);
        Assert.Equal(refund, booking.RefundedAmount);
    }

    [Fact]
    public void Customer_cannot_cancel_within_24_hours_of_departure()
    {
        var (outbound, _) = TestData.RoundTrip();
        var booking = ConfirmedBooking(outbound, null, FareFamily.Flex);

        var ex = Assert.Throws<DomainException>(() =>
            booking.Cancel(BookingActor.Customer, "Too late", outbound.DepartureUtc.AddHours(-23)));

        Assert.Equal("cancellation_window_closed", ex.Code);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
    }

    [Fact]
    public void Administrator_cancellation_refunds_everything_regardless_of_window()
    {
        var (outbound, _) = TestData.RoundTrip();
        var booking = ConfirmedBooking(outbound, null, FareFamily.Basic);

        var refund = booking.Cancel(BookingActor.Administrator, "Flight cancelled", outbound.DepartureUtc.AddHours(-3));

        Assert.Equal(booking.TotalPrice, refund);
        Assert.Equal(PaymentStatus.Refunded, booking.Payments.Single().Status);
    }

    [Fact]
    public void Cancelling_unpaid_booking_refunds_nothing_and_releases_seats()
    {
        var (outbound, _) = TestData.RoundTrip();
        var booking = TestData.Booking(outbound);
        booking.HoldSeats(booking.Segments.Single().Id, [new SeatSelection(booking.Passengers.Single(), outbound.AircraftType.Seats.First())], Now);

        var refund = booking.Cancel(BookingActor.Customer, "Abandoned", Now);

        Assert.Equal(0m, refund.Amount);
        Assert.Empty(booking.Segments.Single().ActiveSeatReservations);
    }

    [Fact]
    public void Cancelled_booking_cannot_be_cancelled_again()
    {
        var (outbound, _) = TestData.RoundTrip();
        var booking = TestData.Booking(outbound);
        booking.Cancel(BookingActor.Customer, "first", Now);

        var ex = Assert.Throws<DomainException>(() => booking.Cancel(BookingActor.Customer, "second", Now));
        Assert.Equal("booking_not_cancellable", ex.Code);
        Assert.Equal(2, booking.History.Count);
    }

    private static Booking ConfirmedBooking(Flight outbound, Flight? inbound, FareFamily family)
    {
        var booking = TestData.Booking(outbound, inbound, family, adults: 2);
        var attempt = booking.StartPayment("pay", "Simulated", Now);
        booking.CompletePayment(attempt, true, "sim_ref", null, Now);
        return booking;
    }
}
