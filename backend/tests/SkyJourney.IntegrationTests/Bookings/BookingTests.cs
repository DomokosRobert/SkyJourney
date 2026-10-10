using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using SkyJourney.Api.Endpoints;
using SkyJourney.Application.Bookings;
using SkyJourney.Application.Common;
using SkyJourney.Domain.Bookings;
using SkyJourney.Domain.Fleet;
using SkyJourney.Domain.Flights;
using SkyJourney.IntegrationTests.Infrastructure;

namespace SkyJourney.IntegrationTests.Bookings;

/// <summary>
/// Booking lifecycle over HTTP against PostgreSQL. Each test uses its own route and date (see docs/testing.md),
/// so capacity and seat changes never leak between tests.
/// </summary>
public sealed class BookingTests(SkyJourneyApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Creating_a_return_booking_prices_it_on_the_server_and_reserves_capacity()
    {
        var outbound = await factory.FindFlightAsync("BUD", "VIE", new DateOnly(2026, 11, 8));
        var inbound = await factory.FindFlightAsync("VIE", "BUD", new DateOnly(2026, 11, 10));
        var client = await factory.CreateUserClientAsync();

        var response = await client.PostBookingAsync(BookingTestHelpers.Request(outbound.Id, FareFamily.Plus, adults: 2, children: 1, inbound.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("false", Assert.Single(response.Headers.GetValues(BookingEndpoints.ReplayedHeader)));
        var booking = await response.ReadAsAsync<BookingDto>();
        Assert.Equal($"/api/v1/bookings/{booking.Reference}", response.Headers.Location?.ToString());
        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
        Assert.Equal(SkyJourneyApiFactory.StartTime + Booking.HoldDuration, booking.ExpiresAt);
        Assert.Equal([JourneyDirection.Outbound, JourneyDirection.Return], booking.Segments.Select(s => s.Direction));
        Assert.Equal(["Adult1", "Adult2", "Child1"], booking.Passengers.Select(p => p.FirstName));
        Assert.Equal("demo", booking.DataSource);

        var outboundFare = outbound.GetFare(FareFamily.Plus).Price.Amount;
        var inboundFare = inbound.GetFare(FareFamily.Plus).Price.Amount;
        Assert.Equal(outboundFare, booking.Segments[0].FarePricePerPassenger.Amount);
        Assert.Equal((outboundFare + inboundFare) * 3, booking.TotalPrice.Amount);
        Assert.Equal(50, booking.Segments[0].FareConditions.RefundPercentage);
        Assert.Equal("Vienna", booking.Segments[0].Flight.Destination.City);

        Assert.Equal(outbound.SeatsSold + 3, await factory.SeatsSoldAsync(outbound.Id));
        Assert.Equal(inbound.SeatsSold + 3, await factory.SeatsSoldAsync(inbound.Id));

        var fetched = await client.GetFromJsonAsync<BookingDto>($"/api/v1/bookings/{booking.Reference.ToLowerInvariant()}",
            SkyJourneyApiFactory.Json, Ct);
        Assert.Equal(booking.Id, fetched!.Id);
    }

    [Fact]
    public async Task Repeating_a_request_with_the_same_idempotency_key_returns_the_original_booking()
    {
        var flight = await factory.FindFlightAsync("BUD", "ZRH", new DateOnly(2026, 11, 8));
        var client = await factory.CreateUserClientAsync();
        var request = BookingTestHelpers.Request(flight.Id);
        var key = Guid.NewGuid().ToString();

        var first = await client.PostBookingAsync(request, key);
        var replay = await client.PostBookingAsync(request, key);
        var different = await client.PostBookingAsync(BookingTestHelpers.Request(flight.Id, FareFamily.Flex), key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues(BookingEndpoints.ReplayedHeader)));
        Assert.Equal((await first.ReadAsAsync<BookingDto>()).Id, (await replay.ReadAsAsync<BookingDto>()).Id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, different.StatusCode);
        Assert.Equal("idempotency_key_reused", (await different.ReadAsAsync<ProblemResponse>()).Code);
        Assert.Equal(flight.SeatsSold + 1, await factory.SeatsSoldAsync(flight.Id));

        // The key is scoped per user: someone else may use the same value.
        var other = await factory.CreateUserClientAsync();
        Assert.Equal(HttpStatusCode.Created, (await other.PostBookingAsync(request, key)).StatusCode);
    }

    [Fact]
    public async Task Concurrent_retries_with_one_idempotency_key_create_a_single_booking()
    {
        var flight = await factory.FindFlightAsync("BUD", "MXP", new DateOnly(2026, 11, 8));
        var client = await factory.CreateUserClientAsync();
        var request = BookingTestHelpers.Request(flight.Id);
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostBookingAsync(request, key)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode, $"Unexpected {r.StatusCode}"));
        var ids = await Task.WhenAll(responses.Select(r => r.ReadAsAsync<BookingDto>()));
        Assert.Single(ids.Select(b => b.Id).Distinct());
        Assert.Equal(flight.SeatsSold + 1, await factory.SeatsSoldAsync(flight.Id));
    }

    [Fact]
    public async Task Concurrent_bookings_for_the_last_seats_never_oversell_the_flight()
    {
        var flight = await factory.FindFlightAsync("BUD", "PRG", new DateOnly(2026, 11, 9));
        await factory.WithDbAsync(db => db.Database.ExecuteSqlAsync(
            $"UPDATE flights SET seats_sold = capacity - 3 WHERE id = {flight.Id}", Ct));
        var clients = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => factory.CreateUserClientAsync()));

        var responses = await Task.WhenAll(clients.Select(c => c.PostBookingAsync(BookingTestHelpers.Request(flight.Id))));

        Assert.Equal(3, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        var rejected = responses.Where(r => r.StatusCode != HttpStatusCode.Created).ToList();
        Assert.Equal(7, rejected.Count);
        foreach (var response in rejected)
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("insufficient_capacity", (await response.ReadAsAsync<ProblemResponse>()).Code);
        }

        Assert.Equal(flight.Capacity, await factory.SeatsSoldAsync(flight.Id));
    }

    [Fact]
    public async Task Seat_map_prices_seats_for_the_fare_and_selection_updates_the_total()
    {
        var flight = await factory.FindFlightAsync("BUD", "WAW", new DateOnly(2026, 11, 8));
        var client = await factory.CreateUserClientAsync();
        var booking = await client.CreateBookingAsync(BookingTestHelpers.Request(flight.Id, adults: 2));
        var seatMapUrl = $"/api/v1/bookings/{booking.Reference}/segments/{booking.Segments[0].Id}/seat-map";

        var map = (await client.GetFromJsonAsync<SeatMapDto>(seatMapUrl, SkyJourneyApiFactory.Json, Ct))!;

        Assert.Equal("Airbus A220-300", map.AircraftName);
        Assert.Equal(flight.Capacity, map.Seats.Count);
        Assert.Equal(SeatCategory.Front, Seat(map, "2A").Category);
        Assert.Equal(9.99m, Seat(map, "2A").Price.Amount);
        Assert.Equal(15.99m, Seat(map, "11C").Price.Amount);
        Assert.Equal(6.99m, Seat(map, "20E").Price.Amount);
        Assert.All(map.Seats, s => Assert.Equal(SeatAvailability.Available, s.Availability));

        var (ada, bob) = (booking.Passengers[0].Id, booking.Passengers[1].Id);
        var selected = await (await client.PutSeatsAsync(booking, 0, (ada, "2a"), (bob, "11C"))).ReadAsAsync<BookingDto>();
        Assert.Equal(booking.TotalPrice.Amount + 9.99m + 15.99m, selected.TotalPrice.Amount);
        Assert.Equal(["2A", "11C"], selected.Segments[0].Seats.Select(s => s.SeatNumber));

        // Swapping seats between the two passengers works because seats are released before new holds are written.
        var swapped = await client.PutSeatsAsync(booking, 0, (ada, "11C"), (bob, "2A"));
        Assert.Equal(HttpStatusCode.OK, swapped.StatusCode);
        var afterSwap = await swapped.ReadAsAsync<BookingDto>();
        Assert.Equal(ada, afterSwap.Segments[0].Seats.Single(s => s.SeatNumber == "11C").PassengerId);
        Assert.Equal(selected.TotalPrice, afterSwap.TotalPrice);

        map = (await client.GetFromJsonAsync<SeatMapDto>(seatMapUrl, SkyJourneyApiFactory.Json, Ct))!;
        Assert.Equal(bob, Seat(map, "2A").PassengerId);
        Assert.Equal(SeatAvailability.Selected, Seat(map, "2A").Availability);

        // Another customer sees those seats as occupied.
        var other = await factory.CreateUserClientAsync();
        var otherBooking = await other.CreateBookingAsync(BookingTestHelpers.Request(flight.Id));
        var otherMap = (await other.GetFromJsonAsync<SeatMapDto>(
            $"/api/v1/bookings/{otherBooking.Reference}/segments/{otherBooking.Segments[0].Id}/seat-map", SkyJourneyApiFactory.Json, Ct))!;
        Assert.Equal(SeatAvailability.Occupied, Seat(otherMap, "2A").Availability);
        Assert.Null(Seat(otherMap, "2A").PassengerId);

        var taken = await other.PutSeatsAsync(otherBooking, 0, (otherBooking.Passengers[0].Id, "2A"));
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal("seat_unavailable", (await taken.ReadAsAsync<ProblemResponse>()).Code);
    }

    [Fact]
    public async Task Seats_are_free_on_a_flex_fare_and_unknown_seats_or_passengers_are_rejected()
    {
        var flight = await factory.FindFlightAsync("BUD", "CPH", new DateOnly(2026, 11, 8));
        var client = await factory.CreateUserClientAsync();
        var booking = await client.CreateBookingAsync(BookingTestHelpers.Request(flight.Id, FareFamily.Flex));
        var passenger = booking.Passengers[0].Id;

        var selected = await (await client.PutSeatsAsync(booking, 0, (passenger, "1A"))).ReadAsAsync<BookingDto>();
        Assert.Equal(0m, selected.Segments[0].Seats.Single().Price.Amount);
        Assert.Equal(booking.TotalPrice, selected.TotalPrice);

        var unknownSeat = await client.PutSeatsAsync(booking, 0, (passenger, "99Z"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknownSeat.StatusCode);
        Assert.Equal("seat_not_found", (await unknownSeat.ReadAsAsync<ProblemResponse>()).Code);

        var unknownPassenger = await client.PutSeatsAsync(booking, 0, (Guid.NewGuid(), "2A"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknownPassenger.StatusCode);
        Assert.Equal("passenger_not_in_booking", (await unknownPassenger.ReadAsAsync<ProblemResponse>()).Code);

        var malformed = await client.PutSeatsAsync(booking, 0, (passenger, "A1"));
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    [Fact]
    public async Task Concurrent_selection_of_one_seat_lets_exactly_one_customer_have_it()
    {
        var flight = await factory.FindFlightAsync("BUD", "PRG", new DateOnly(2026, 11, 10));
        var clients = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => factory.CreateUserClientAsync()));
        var bookings = await Task.WhenAll(clients.Select(c => c.CreateBookingAsync(BookingTestHelpers.Request(flight.Id))));

        var responses = await Task.WhenAll(clients.Select((c, i) =>
            c.PutSeatsAsync(bookings[i], 0, (bookings[i].Passengers[0].Id, "5C"))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        foreach (var response in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("seat_unavailable", (await response.ReadAsAsync<ProblemResponse>()).Code);
        }

        var holders = await factory.WithDbAsync(db => db.SeatReservations
            .Where(r => r.FlightId == flight.Id && r.SeatNumber == "5C" && r.Status != SeatReservationStatus.Released)
            .CountAsync(Ct));
        Assert.Equal(1, holders);
    }

    [Fact]
    public async Task Other_users_cannot_see_or_change_a_booking()
    {
        var flight = await factory.FindFlightAsync("BUD", "VIE", new DateOnly(2026, 11, 12));
        var owner = await factory.CreateUserClientAsync();
        var booking = await owner.CreateBookingAsync(BookingTestHelpers.Request(flight.Id));
        var intruder = await factory.CreateUserClientAsync();
        var segment = booking.Segments[0].Id;

        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/api/v1/bookings/{booking.Reference}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await intruder.GetAsync($"/api/v1/bookings/{booking.Reference}/segments/{segment}/seat-map", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PutSeatsAsync(booking, 0, (booking.Passengers[0].Id, "3C"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/api/v1/bookings/{booking.Reference}/cancellation", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await intruder.PostAsync($"/api/v1/bookings/{booking.Reference}/cancel", null, Ct)).StatusCode);

        var intruderList = (await intruder.GetFromJsonAsync<PagedResult<BookingSummaryDto>>("/api/v1/bookings", SkyJourneyApiFactory.Json, Ct))!;
        Assert.Equal(0, intruderList.TotalCount);

        var ownerList = (await owner.GetFromJsonAsync<PagedResult<BookingSummaryDto>>("/api/v1/bookings", SkyJourneyApiFactory.Json, Ct))!;
        var summary = Assert.Single(ownerList.Items);
        Assert.Equal(booking.Reference, summary.Reference);
        Assert.Equal("VIE", summary.Destination.IataCode);
        Assert.True(summary.IsUpcoming);

        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/bookings", Ct)).StatusCode);
    }

    [Fact]
    public async Task Cancelling_an_unpaid_booking_releases_seats_and_capacity()
    {
        var flight = await factory.FindFlightAsync("BUD", "ZRH", new DateOnly(2026, 11, 12));
        var client = await factory.CreateUserClientAsync();
        var booking = await client.CreateBookingAsync(BookingTestHelpers.Request(flight.Id, adults: 2));
        await client.PutSeatsAsync(booking, 0, (booking.Passengers[0].Id, "4A"));

        var quote = (await client.GetFromJsonAsync<CancellationQuoteDto>($"/api/v1/bookings/{booking.Reference}/cancellation",
            SkyJourneyApiFactory.Json, Ct))!;
        Assert.True(quote.CanCancel);
        Assert.Equal(0m, quote.Refund!.Amount);

        var response = await client.PostAsync($"/api/v1/bookings/{booking.Reference}/cancel", null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cancelled = await response.ReadAsAsync<BookingDto>();
        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
        Assert.Null(cancelled.ExpiresAt);
        Assert.Empty(cancelled.Segments[0].Seats);
        Assert.Equal(flight.SeatsSold, await factory.SeatsSoldAsync(flight.Id));

        var again = await client.PostAsync($"/api/v1/bookings/{booking.Reference}/cancel", null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("booking_not_cancellable", (await again.ReadAsAsync<ProblemResponse>()).Code);
    }

    [Fact]
    public async Task Cancelling_a_confirmed_plus_booking_refunds_half_the_fare_but_not_the_seats()
    {
        var flight = await factory.FindFlightAsync("BUD", "ARN", new DateOnly(2026, 11, 9));
        var client = await factory.CreateUserClientAsync();
        var booking = await client.CreateBookingAsync(BookingTestHelpers.Request(flight.Id, FareFamily.Plus));
        var withSeat = await (await client.PutSeatsAsync(booking, 0, (booking.Passengers[0].Id, "20A"))).ReadAsAsync<BookingDto>();
        await client.ConfirmAsync(booking);

        var response = await client.PostAsync($"/api/v1/bookings/{booking.Reference}/cancel", null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cancelled = await response.ReadAsAsync<BookingDto>();
        var fare = booking.Segments[0].FarePricePerPassenger.Amount;
        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
        Assert.Equal(Math.Round(fare / 2, 2, MidpointRounding.AwayFromZero), cancelled.RefundedAmount.Amount);
        Assert.Equal(withSeat.TotalPrice, cancelled.TotalPrice);
        Assert.Equal(flight.SeatsSold, await factory.SeatsSoldAsync(flight.Id));

        var payment = await factory.WithDbAsync(db => db.PaymentAttempts.SingleAsync(p => p.BookingId == booking.Id, Ct));
        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);
    }

    [Fact]
    public async Task Confirmed_bookings_cannot_be_cancelled_within_24_hours_of_departure()
    {
        // Departs on the frozen "today", a few hours from now: still bookable, no longer cancellable once paid.
        var flight = await factory.FindFlightAsync("BUD", "HEL", new DateOnly(2026, 11, 2));
        var client = await factory.CreateUserClientAsync();
        var booking = await client.CreateBookingAsync(BookingTestHelpers.Request(flight.Id, FareFamily.Flex));
        await client.ConfirmAsync(booking);

        var quote = (await client.GetFromJsonAsync<CancellationQuoteDto>($"/api/v1/bookings/{booking.Reference}/cancellation",
            SkyJourneyApiFactory.Json, Ct))!;
        var response = await client.PostAsync($"/api/v1/bookings/{booking.Reference}/cancel", null, Ct);

        Assert.False(quote.CanCancel);
        Assert.Equal("cancellation_window_closed", quote.ReasonCode);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("cancellation_window_closed", (await response.ReadAsAsync<ProblemResponse>()).Code);
    }

    [Fact]
    public async Task A_lapsed_hold_reads_as_expired_and_is_released_by_the_next_booking_on_the_flight()
    {
        var clock = new FakeTimeProvider(SkyJourneyApiFactory.StartTime);
        await using var host = factory.WithOwnClock(clock);
        var flight = await factory.FindFlightAsync("BUD", "DUB", new DateOnly(2026, 11, 10));
        var email = AuthHelpers.UniqueEmail();
        var client = await host.CreateUserClientAsync(email);
        var booking = await client.CreateBookingAsync(BookingTestHelpers.Request(flight.Id, adults: 2));
        await client.PutSeatsAsync(booking, 0, (booking.Passengers[0].Id, "6D"));

        clock.Advance(Booking.HoldDuration + TimeSpan.FromSeconds(1));
        await client.SignInAgainAsync(email); // the hold outlives the 15-minute access token

        var read = (await client.GetFromJsonAsync<BookingDto>($"/api/v1/bookings/{booking.Reference}", SkyJourneyApiFactory.Json, Ct))!;
        Assert.Equal(BookingStatus.Expired, read.Status);
        Assert.Null(read.ExpiresAt);
        // Reading does not write; the hold is still counted until something releases it.
        Assert.Equal(flight.SeatsSold + 2, await factory.SeatsSoldAsync(flight.Id));

        var seatChange = await client.PutSeatsAsync(booking, 0, (booking.Passengers[0].Id, "7D"));
        Assert.Equal(HttpStatusCode.Conflict, seatChange.StatusCode);
        Assert.Equal("booking_expired", (await seatChange.ReadAsAsync<ProblemResponse>()).Code);

        var other = await host.CreateUserClientAsync();
        var next = await other.CreateBookingAsync(BookingTestHelpers.Request(flight.Id));
        var taken = await other.PutSeatsAsync(next, 0, (next.Passengers[0].Id, "6D"));

        Assert.Equal(HttpStatusCode.OK, taken.StatusCode);
        Assert.Equal(flight.SeatsSold + 1, await factory.SeatsSoldAsync(flight.Id));
        var stored = await factory.WithDbAsync(db => db.Bookings.SingleAsync(b => b.Id == booking.Id, Ct));
        Assert.Equal(BookingStatus.Expired, stored.Status);
    }

    [Fact]
    public async Task Invalid_itineraries_and_requests_are_rejected()
    {
        var client = await factory.CreateUserClientAsync();
        var outbound = await factory.FindFlightAsync("BUD", "OTP", new DateOnly(2026, 11, 9));
        var sameDayReturn = await factory.FindFlightAsync("OTP", "BUD", new DateOnly(2026, 11, 9));

        var tooEarly = await client.PostBookingAsync(BookingTestHelpers.Request(outbound.Id, returnFlightId: sameDayReturn.Id));
        Assert.Equal(HttpStatusCode.Conflict, tooEarly.StatusCode);
        Assert.Equal("invalid_itinerary", (await tooEarly.ReadAsAsync<ProblemResponse>()).Code);

        var wrongRoute = await client.PostBookingAsync(BookingTestHelpers.Request(outbound.Id, returnFlightId: outbound.Id));
        Assert.Equal(HttpStatusCode.BadRequest, wrongRoute.StatusCode);

        var unknown = await client.PostBookingAsync(BookingTestHelpers.Request(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        var childrenOnly = await client.PostBookingAsync(BookingTestHelpers.Request(outbound.Id, adults: 0, children: 2));
        var problem = await childrenOnly.ReadAsAsync<ProblemResponse>();
        Assert.Equal(HttpStatusCode.BadRequest, childrenOnly.StatusCode);
        Assert.Contains("passengers", problem.Errors!.Keys);

        using var noKey = new HttpRequestMessage(HttpMethod.Post, "/api/v1/bookings")
        {
            Content = JsonContent.Create(BookingTestHelpers.Request(outbound.Id), options: SkyJourneyApiFactory.Json),
        };
        var missingKey = await client.SendAsync(noKey, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, missingKey.StatusCode);
        Assert.Contains(IdempotencyKey.HeaderName, (await missingKey.ReadAsAsync<ProblemResponse>()).Errors!.Keys,
            StringComparer.OrdinalIgnoreCase);

        Assert.Equal(outbound.SeatsSold, await factory.SeatsSoldAsync(outbound.Id));
    }

    private static SeatMapSeatDto Seat(SeatMapDto map, string number) => map.Seats.Single(s => s.SeatNumber == number);
}
