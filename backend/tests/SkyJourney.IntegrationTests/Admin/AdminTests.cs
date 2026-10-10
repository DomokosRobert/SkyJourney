using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SkyJourney.Application.Admin;
using SkyJourney.Application.Auth;
using SkyJourney.Application.Bookings;
using SkyJourney.Application.Common;
using SkyJourney.Application.Flights;
using SkyJourney.Domain.Bookings;
using SkyJourney.Domain.Flights;
using SkyJourney.Infrastructure.Identity;
using SkyJourney.IntegrationTests.Bookings;
using SkyJourney.IntegrationTests.Infrastructure;

namespace SkyJourney.IntegrationTests.Admin;

/// <summary>
/// Back-office API. Like the booking tests, every test that changes flights uses its own route and date
/// (BUD-DXB 13 Nov, BUD-KEF 14 and 16 Nov, BUD-OSL 2 Nov) or airports it creates itself (codes QX*).
/// </summary>
public sealed class AdminTests(SkyJourneyApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Admin_routes_require_the_admin_role()
    {
        var anonymous = factory.CreateClient();
        var customer = await factory.CreateUserClientAsync();
        var admin = await CreateAdminClientAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/flights", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/admin/flights", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await customer.PostAsJsonAsync("/api/v1/admin/airports/BUD/deactivate", new { }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/flights", Ct)).StatusCode);
    }

    [Fact]
    public async Task Airports_can_be_created_updated_and_hidden_from_search()
    {
        var admin = await CreateAdminClientAsync();
        var publicClient = factory.CreateClient();

        var created = await admin.PostJsonAsync("/api/v1/admin/airports", Airport("qxa", "Admintown"));
        var duplicate = await admin.PostJsonAsync("/api/v1/admin/airports", Airport("QXA", "Admintown"));
        var invalid = await admin.PostJsonAsync("/api/v1/admin/airports", Airport("QX1", "") with { TimeZoneId = "" });
        var badZone = await admin.PostJsonAsync("/api/v1/admin/airports", Airport("QXZ", "Nowhere") with { TimeZoneId = "Mars/Olympus" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("QXA", (await created.ReadAsAsync<AdminAirportDto>()).IataCode);
        Assert.Equal("airport_exists", (await duplicate.ReadAsAsync<ProblemResponse>()).Code);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("invalid_time_zone", (await badZone.ReadAsAsync<ProblemResponse>()).Code);

        var updated = await (await admin.PutAsJsonAsync("/api/v1/admin/airports/QXA",
            new UpdateAirportRequest("Admintown Central", "Admintown", "HU", "Europe/Budapest", 47.1, 19.1), Ct))
            .ReadAsAsync<AdminAirportDto>();
        Assert.Equal("Admintown Central", updated.Name);
        Assert.Equal(HttpStatusCode.OK, (await publicClient.GetAsync("/api/v1/airports/QXA", Ct)).StatusCode);

        var deactivated = await (await admin.PostJsonAsync("/api/v1/admin/airports/QXA/deactivate", new { })).ReadAsAsync<AdminAirportDto>();
        Assert.False(deactivated.IsActive);
        Assert.Equal(HttpStatusCode.NotFound, (await publicClient.GetAsync("/api/v1/airports/QXA", Ct)).StatusCode);
        var all = await (await admin.GetAsync("/api/v1/admin/airports", Ct)).ReadAsAsync<List<AdminAirportDto>>();
        Assert.Contains(all, a => a is { IataCode: "QXA", IsActive: false });

        await admin.PostJsonAsync("/api/v1/admin/airports/QXA/activate", new { });
        Assert.Equal(HttpStatusCode.OK, (await publicClient.GetAsync("/api/v1/airports/QXA", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostJsonAsync("/api/v1/admin/airports/QXQ/activate", new { })).StatusCode);
    }

    [Fact]
    public async Task Creating_a_schedule_generates_bookable_flights_within_the_horizon()
    {
        var admin = await CreateAdminClientAsync();
        await admin.PostJsonAsync("/api/v1/admin/airports", Airport("QXB", "Schedulia"));
        await admin.PostJsonAsync("/api/v1/admin/airports", Airport("QXC", "Timetableton"));
        var request = new CreateScheduleRequest("sj9001", "SJ", "QXB", "QXC", "A320", new TimeOnly(8, 0), 120,
            [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday], new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 15), 80m, "EUR");

        var response = await admin.PostJsonAsync("/api/v1/admin/schedules", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.ReadAsAsync<CreateScheduleResult>();
        Assert.Equal("SJ9001", result.Schedule.FlightNumber);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday], result.Schedule.OperatingDays);
        Assert.Equal(6, result.GeneratedFlights); // 2, 4, 6, 9, 11 and 13 November

        var search = await (await factory.CreateClient()
                .GetAsync("/api/v1/flights/search?origin=QXB&destination=QXC&departureDate=2026-11-04", Ct))
            .ReadAsAsync<FlightSearchResponse>();
        var offer = Assert.Single(search.Outbound);
        Assert.Equal("SJ9001", offer.FlightNumber);

        var flights = await (await admin.GetAsync("/api/v1/admin/flights?flightNumber=SJ9001&pageSize=50", Ct))
            .ReadAsAsync<PagedResult<AdminFlightDto>>();
        Assert.Equal(6, flights.TotalCount);
        Assert.All(flights.Items, f => Assert.Equal(result.Schedule.Id, f.ScheduleId));

        var overlap = await admin.PostJsonAsync("/api/v1/admin/schedules", request with { ValidFrom = new DateOnly(2026, 11, 10) });
        var unknownAircraft = await admin.PostJsonAsync("/api/v1/admin/schedules", request with { FlightNumber = "SJ9002", AircraftCode = "B747" });
        var wrongPrefix = await admin.PostJsonAsync("/api/v1/admin/schedules", request with { FlightNumber = "XX9003" });
        var invalid = await admin.PostJsonAsync("/api/v1/admin/schedules", request with { OperatingDays = [], BaseFare = -1m });

        Assert.Equal("schedule_overlap", (await overlap.ReadAsAsync<ProblemResponse>()).Code);
        Assert.Equal("aircraft_not_found", (await unknownAircraft.ReadAsAsync<ProblemResponse>()).Code);
        Assert.Equal("invalid_flight_number", (await wrongPrefix.ReadAsAsync<ProblemResponse>()).Code);
        var problem = await invalid.ReadAsAsync<ProblemResponse>();
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("operatingDays", problem.Errors!.Keys);
        Assert.Contains("baseFare", problem.Errors!.Keys);
    }

    [Fact]
    public async Task Fare_changes_apply_to_new_bookings_only()
    {
        var flight = await factory.FindFlightAsync("BUD", "DXB", new DateOnly(2026, 11, 13));
        var admin = await CreateAdminClientAsync();
        var customer = await factory.CreateUserClientAsync();
        var before = await customer.CreateBookingAsync(BookingTestHelpers.Request(flight.Id));

        var repriced = await admin.PutAsJsonAsync($"/api/v1/admin/flights/{flight.Id}/fares/basic",
            new UpdateFareRequest(199.99m, true), Ct);
        var after = await customer.CreateBookingAsync(BookingTestHelpers.Request(flight.Id));
        var closed = await admin.PutAsJsonAsync($"/api/v1/admin/flights/{flight.Id}/fares/Basic",
            new UpdateFareRequest(199.99m, false), Ct);
        var refused = await customer.PostBookingAsync(BookingTestHelpers.Request(flight.Id));

        Assert.Equal(HttpStatusCode.OK, repriced.StatusCode);
        Assert.NotEqual(199.99m, before.Segments[0].FarePricePerPassenger.Amount);
        Assert.Equal(before.TotalPrice, (await customer.GetFromJsonAsync<BookingDto>(
            $"/api/v1/bookings/{before.Reference}", SkyJourneyApiFactory.Json, Ct))!.TotalPrice);
        Assert.Equal(199.99m, after.Segments[0].FarePricePerPassenger.Amount);
        var basic = (await closed.ReadAsAsync<AdminFlightDto>()).Fares.Single(f => f.Family == FareFamily.Basic);
        Assert.False(basic.IsAvailable);
        Assert.Equal("fare_unavailable", (await refused.ReadAsAsync<ProblemResponse>()).Code);

        var unknownFamily = await admin.PutAsJsonAsync($"/api/v1/admin/flights/{flight.Id}/fares/first", new UpdateFareRequest(10m, true), Ct);
        var tooPrecise = await admin.PutAsJsonAsync($"/api/v1/admin/flights/{flight.Id}/fares/Plus", new UpdateFareRequest(10.001m, true), Ct);
        Assert.Equal(HttpStatusCode.NotFound, unknownFamily.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooPrecise.StatusCode);
    }

    [Fact]
    public async Task Cancelling_a_flight_cancels_its_bookings_with_a_full_refund()
    {
        var flight = await factory.FindFlightAsync("BUD", "KEF", new DateOnly(2026, 11, 14));
        var admin = await CreateAdminClientAsync();
        var paidCustomer = await factory.CreateUserClientAsync();
        var unpaidCustomer = await factory.CreateUserClientAsync();
        var paid = await paidCustomer.CreateBookingAsync(BookingTestHelpers.Request(flight.Id, FareFamily.Basic, adults: 2));
        await paidCustomer.ConfirmAsync(paid);
        var unpaid = await unpaidCustomer.CreateBookingAsync(BookingTestHelpers.Request(flight.Id));

        var response = await admin.PostJsonAsync($"/api/v1/admin/flights/{flight.Id}/cancel", new CancelRequest("Volcanic ash"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.ReadAsAsync<FlightCancellationResultDto>();
        Assert.Equal(FlightStatus.Cancelled, result.Flight.Status);
        Assert.Equal(0, result.Flight.SeatsSold);
        Assert.Equal(2, result.CancelledBookings);
        Assert.Equal([paid.TotalPrice], result.Refunded); // Basic fares are normally non-refundable
        Assert.Empty(result.Skipped);

        var paidAfter = (await admin.GetFromJsonAsync<AdminBookingDto>($"/api/v1/admin/bookings/{paid.Reference}", SkyJourneyApiFactory.Json, Ct))!;
        Assert.Equal(BookingStatus.Cancelled, paidAfter.Booking.Status);
        Assert.Equal(paid.TotalPrice, paidAfter.Booking.RefundedAmount);
        Assert.Equal(PaymentStatus.Refunded, Assert.Single(paidAfter.Payments).Status);
        var last = paidAfter.History[^1];
        Assert.Equal((BookingActor.Administrator, BookingStatus.Cancelled), (last.Actor, last.To));
        Assert.Contains("Volcanic ash", last.Reason);

        var unpaidAfter = await unpaidCustomer.GetFromJsonAsync<BookingDto>($"/api/v1/bookings/{unpaid.Reference}", SkyJourneyApiFactory.Json, Ct);
        Assert.Equal(BookingStatus.Cancelled, unpaidAfter!.Status);
        Assert.Equal("flight_cancelled", (await (await unpaidCustomer.PayAsync(unpaid)).ReadAsAsync<ProblemResponse>()).Code);
        Assert.Equal("flight_not_bookable",
            (await (await unpaidCustomer.PostBookingAsync(BookingTestHelpers.Request(flight.Id))).ReadAsAsync<ProblemResponse>()).Code);

        // Repeating is safe: nothing is left to cancel.
        var again = await (await admin.PostJsonAsync($"/api/v1/admin/flights/{flight.Id}/cancel", new CancelRequest("Volcanic ash")))
            .ReadAsAsync<FlightCancellationResultDto>();
        Assert.Equal(0, again.CancelledBookings);
        Assert.Empty(again.Refunded);
    }

    [Fact]
    public async Task Bookings_on_a_cancelled_flight_cannot_be_paid()
    {
        // The window a payment guard protects: the flight is cancelled while a booking on it is still unpaid
        // (e.g. it was skipped because a payment was in flight). Simulated by cancelling only the flight.
        var flight = await factory.FindFlightAsync("BUD", "KEF", new DateOnly(2026, 11, 16));
        var customer = await factory.CreateUserClientAsync();
        var booking = await customer.CreateBookingAsync(BookingTestHelpers.Request(flight.Id));
        await factory.WithDbAsync(db => db.Flights.Where(f => f.Id == flight.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Status, FlightStatus.Cancelled), Ct));

        var response = await customer.PayAsync(booking);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("flight_cancelled", (await response.ReadAsAsync<ProblemResponse>()).Code);
        Assert.Equal(0, await factory.WithDbAsync(db => db.PaymentAttempts.CountAsync(p => p.BookingId == booking.Id, Ct)));
    }

    [Fact]
    public async Task Admins_can_find_and_cancel_bookings_inside_the_customer_cutoff_with_a_full_refund()
    {
        var flight = await factory.FindFlightAsync("BUD", "OSL", new DateOnly(2026, 11, 2));
        var admin = await CreateAdminClientAsync();
        var customer = await factory.CreateUserClientAsync();
        var booking = await customer.CreateBookingAsync(BookingTestHelpers.Request(flight.Id, FareFamily.Plus));
        await customer.ConfirmAsync(booking);

        var customerAttempt = await customer.PostJsonAsync($"/api/v1/bookings/{booking.Reference}/cancel", new { });
        Assert.Equal("cancellation_window_closed", (await customerAttempt.ReadAsAsync<ProblemResponse>()).Code);

        var byReference = await (await admin.GetAsync($"/api/v1/admin/bookings?reference={booking.Reference.ToLowerInvariant()}", Ct))
            .ReadAsAsync<PagedResult<AdminBookingSummaryDto>>();
        var byFlight = await (await admin.GetAsync($"/api/v1/admin/bookings?flightId={flight.Id}&status=Confirmed", Ct))
            .ReadAsAsync<PagedResult<AdminBookingSummaryDto>>();
        var summary = Assert.Single(byReference.Items);
        Assert.Equal([flight.FlightNumber], summary.FlightNumbers);
        Assert.Contains(byFlight.Items, b => b.Reference == booking.Reference);

        var noReason = await admin.PostJsonAsync($"/api/v1/admin/bookings/{booking.Reference}/cancel", new CancelRequest(" "));
        var cancelled = await admin.PostJsonAsync($"/api/v1/admin/bookings/{booking.Reference}/cancel", new CancelRequest("Customer bereavement"));
        var twice = await admin.PostJsonAsync($"/api/v1/admin/bookings/{booking.Reference}/cancel", new CancelRequest("Again"));

        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var result = await cancelled.ReadAsAsync<AdminBookingDto>();
        Assert.Equal(BookingStatus.Cancelled, result.Booking.Status);
        Assert.Equal(booking.TotalPrice, result.Booking.RefundedAmount);
        Assert.Equal(summary.UserId, result.UserId);
        Assert.Equal("booking_not_cancellable", (await twice.ReadAsAsync<ProblemResponse>()).Code);
        Assert.Equal(0, await factory.SeatsSoldAsync(flight.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/admin/bookings/SJ0000", Ct)).StatusCode);
    }

    private static CreateAirportRequest Airport(string code, string city) =>
        new(code, $"{city} International", city, "HU", "Europe/Budapest", 47.0, 19.0);

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var email = AuthHelpers.UniqueEmail("admin");
        var client = factory.CreateClient();
        await client.RegisterAsync(email);
        Assert.True((await AdminProvisioning.PromoteAsync(factory.Services, email)).Succeeded);

        // Roles are read from the access token, so sign in again to get one that carries the Admin role.
        var auth = await (await client.PostJsonAsync("/api/v1/auth/login", new LoginRequest(email, AuthHelpers.Password)))
            .ReadAsAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
