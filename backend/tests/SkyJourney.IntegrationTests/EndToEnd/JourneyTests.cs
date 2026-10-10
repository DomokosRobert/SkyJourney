using System.Net;
using System.Net.Http.Json;
using SkyJourney.Application.Admin;
using SkyJourney.Application.Bookings;
using SkyJourney.Application.Common;
using SkyJourney.Application.Flights;
using SkyJourney.Application.Payments;
using SkyJourney.Domain.Bookings;
using SkyJourney.Domain.Flights;
using SkyJourney.Infrastructure.Identity;
using SkyJourney.Infrastructure.Payments;
using SkyJourney.IntegrationTests.Bookings;
using SkyJourney.IntegrationTests.Infrastructure;

namespace SkyJourney.IntegrationTests.EndToEnd;

/// <summary>
/// Whole user journeys through the public API only, in the order the mobile app calls it. The feature tests check
/// each rule in depth; these check that the pieces fit together. Routes: BUD-BCN 15 Nov / BCN-BUD 19 Nov, BUD-AMS 17 Nov.
/// </summary>
public sealed class JourneyTests(SkyJourneyApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_customer_searches_books_a_round_trip_chooses_seats_pays_and_cancels()
    {
        // 1. Search anonymously, as the app does before sign-in.
        var search = await (await factory.CreateClient().GetAsync(
                "/api/v1/flights/search?origin=BUD&destination=BCN&departureDate=2026-11-15&returnDate=2026-11-19&adults=2", Ct))
            .ReadAsAsync<FlightSearchResponse>();
        var outbound = search.Outbound.First(o => o.IsBookable);
        var inbound = search.Return!.First(o => o.IsBookable);
        var outboundFare = outbound.Fares.Single(f => f.Family == FareFamily.Flex);
        var inboundFare = inbound.Fares.Single(f => f.Family == FareFamily.Flex);
        var seatsSoldBefore = await factory.SeatsSoldAsync(outbound.FlightId);

        // 2. Sign up, then book: the server prices the booking exactly as the search quoted it.
        var customer = await factory.CreateUserClientAsync();
        var booking = await customer.CreateBookingAsync(
            BookingTestHelpers.Request(outbound.FlightId, FareFamily.Flex, adults: 2, returnFlightId: inbound.FlightId));
        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
        Assert.NotNull(booking.ExpiresAt);
        Assert.Equal(outboundFare.TotalPrice.Amount + inboundFare.TotalPrice.Amount, booking.TotalPrice.Amount);

        // 3. Choose two free seats on the outbound flight; seat fees are added to the total.
        var seatMap = (await customer.GetFromJsonAsync<SeatMapDto>(
            $"/api/v1/bookings/{booking.Reference}/segments/{booking.Segments[0].Id}/seat-map", SkyJourneyApiFactory.Json, Ct))!;
        var freeSeats = seatMap.Seats.Where(s => s.Availability == SeatAvailability.Available).Take(2).ToList();
        var seated = await customer.PutSeatsAsync(booking, 0,
            (booking.Passengers[0].Id, freeSeats[0].SeatNumber), (booking.Passengers[1].Id, freeSeats[1].SeatNumber));
        Assert.Equal(HttpStatusCode.OK, seated.StatusCode);
        booking = await GetBookingAsync(customer, booking.Reference);
        Assert.Equal(outboundFare.TotalPrice.Amount + inboundFare.TotalPrice.Amount + freeSeats.Sum(s => s.Price.Amount),
            booking.TotalPrice.Amount);

        // 4. A declined test token leaves the booking payable; the success token confirms it.
        var declined = await (await customer.PayAsync(booking, SimulatedPaymentGateway.DeclinedToken)).ReadAsAsync<PaymentResultDto>();
        Assert.Equal((PaymentStatus.Failed, BookingStatus.PendingPayment), (declined.Status, declined.Booking.Status));
        var paid = await (await customer.PayAsync(booking)).ReadAsAsync<PaymentResultDto>();
        Assert.Equal((PaymentStatus.Succeeded, BookingStatus.Confirmed), (paid.Status, paid.Booking.Status));
        Assert.Equal(booking.TotalPrice, paid.Amount);

        // 5. The trip shows up in the customer's list.
        var trips = (await customer.GetFromJsonAsync<PagedResult<BookingSummaryDto>>("/api/v1/bookings", SkyJourneyApiFactory.Json, Ct))!;
        Assert.Contains(trips.Items, t => t.Reference == booking.Reference && t.Status == BookingStatus.Confirmed);

        // 6. Flex: cancelling more than 24 h out refunds everything, seat fees included, and frees the places.
        var quote = (await customer.GetFromJsonAsync<CancellationQuoteDto>(
            $"/api/v1/bookings/{booking.Reference}/cancellation", SkyJourneyApiFactory.Json, Ct))!;
        Assert.True(quote.CanCancel);
        Assert.Equal(booking.TotalPrice, quote.Refund);
        var cancelled = await (await customer.PostJsonAsync($"/api/v1/bookings/{booking.Reference}/cancel", new { }))
            .ReadAsAsync<BookingDto>();
        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
        Assert.Equal(booking.TotalPrice, cancelled.RefundedAmount);
        Assert.Equal(seatsSoldBefore, await factory.SeatsSoldAsync(outbound.FlightId));
    }

    [Fact]
    public async Task An_airline_cancellation_refunds_the_customer_and_removes_the_flight_from_sale()
    {
        var customer = await factory.CreateUserClientAsync();
        var search = await (await customer.GetAsync("/api/v1/flights/search?origin=BUD&destination=AMS&departureDate=2026-11-17", Ct))
            .ReadAsAsync<FlightSearchResponse>();
        var flight = search.Outbound.First(o => o.IsBookable);
        var booking = await customer.CreateBookingAsync(BookingTestHelpers.Request(flight.FlightId));
        await customer.PayAsync(booking);

        var adminEmail = AuthHelpers.UniqueEmail("ops");
        var admin = factory.CreateClient();
        await admin.RegisterAsync(adminEmail);
        await AdminProvisioning.PromoteAsync(factory.Services, adminEmail);
        await admin.SignInAgainAsync(adminEmail);
        var result = await (await admin.PostJsonAsync($"/api/v1/admin/flights/{flight.FlightId}/cancel", new CancelRequest("Strike")))
            .ReadAsAsync<FlightCancellationResultDto>();
        Assert.Equal(1, result.CancelledBookings);

        // The customer sees the cancellation and a full refund (even on a Basic fare)...
        var after = await GetBookingAsync(customer, booking.Reference);
        Assert.Equal(BookingStatus.Cancelled, after.Status);
        Assert.Equal(booking.TotalPrice, after.RefundedAmount);

        // ...and the flight can no longer be bought.
        var again = await (await customer.GetAsync("/api/v1/flights/search?origin=BUD&destination=AMS&departureDate=2026-11-17", Ct))
            .ReadAsAsync<FlightSearchResponse>();
        Assert.DoesNotContain(again.Outbound, o => o.FlightId == flight.FlightId && o.IsBookable);
    }

    private static async Task<BookingDto> GetBookingAsync(HttpClient client, string reference) =>
        (await client.GetFromJsonAsync<BookingDto>($"/api/v1/bookings/{reference}", SkyJourneyApiFactory.Json, Ct))!;
}
