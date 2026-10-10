using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using SkyJourney.Application.Auth;
using SkyJourney.Application.Bookings;
using SkyJourney.Domain.Bookings;
using SkyJourney.Domain.Flights;
using SkyJourney.IntegrationTests.Infrastructure;

namespace SkyJourney.IntegrationTests.Bookings;

internal static class BookingTestHelpers
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A client signed in as a freshly registered user.</summary>
    public static async Task<HttpClient> CreateUserClientAsync(this WebApplicationFactory<Program> factory, string? email = null)
    {
        var client = factory.CreateClient();
        var auth = await client.RegisterAsync(email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    /// <summary>Signs in again, e.g. after moving a test clock past the access token's lifetime.</summary>
    public static async Task SignInAgainAsync(this HttpClient client, string email)
    {
        var response = await client.PostJsonAsync("/api/v1/auth/login", new LoginRequest(email, AuthHelpers.Password));
        response.EnsureSuccessStatusCode();
        var auth = await response.ReadAsAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
    }

    /// <summary>The single scheduled flight on a route and (origin-local) date; every test uses its own route/date.</summary>
    public static Task<Flight> FindFlightAsync(this SkyJourneyApiFactory factory, string origin, string destination, DateOnly date) =>
        factory.WithDbAsync(db => db.Flights.AsNoTracking().Include(f => f.Fares)
            .Where(f => f.Origin.IataCode == origin && f.Destination.IataCode == destination && f.DepartureLocalDate == date)
            .OrderBy(f => f.DepartureUtc)
            .FirstAsync(Ct));

    public static Task<int> SeatsSoldAsync(this SkyJourneyApiFactory factory, Guid flightId) =>
        factory.WithDbAsync(db => db.Flights.Where(f => f.Id == flightId).Select(f => f.SeatsSold).SingleAsync(Ct));

    public static CreateBookingRequest Request(Guid outboundFlightId, FareFamily family = FareFamily.Basic, int adults = 1,
        int children = 0, Guid? returnFlightId = null) =>
        new(new FlightSelectionRequest(outboundFlightId, family),
            returnFlightId is { } id ? new FlightSelectionRequest(id, family) : null,
            Enumerable.Range(1, adults).Select(i => new PassengerRequest($"Adult{i}", "Traveller", PassengerType.Adult))
                .Concat(Enumerable.Range(1, children).Select(i => new PassengerRequest($"Child{i}", "Traveller", PassengerType.Child)))
                .ToList());

    public static async Task<HttpResponseMessage> PostBookingAsync(this HttpClient client, CreateBookingRequest request,
        string? idempotencyKey = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/bookings")
        {
            Content = JsonContent.Create(request, options: SkyJourneyApiFactory.Json),
        };
        message.Headers.Add(IdempotencyKey.HeaderName, idempotencyKey ?? Guid.NewGuid().ToString());
        return await client.SendAsync(message, Ct);
    }

    public static async Task<BookingDto> CreateBookingAsync(this HttpClient client, CreateBookingRequest request)
    {
        var response = await client.PostBookingAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsAsync<BookingDto>();
    }

    public static Task<HttpResponseMessage> PutSeatsAsync(this HttpClient client, BookingDto booking, int segmentIndex,
        params (Guid PassengerId, string SeatNumber)[] seats) =>
        client.PutAsJsonAsync($"/api/v1/bookings/{booking.Reference}/segments/{booking.Segments[segmentIndex].Id}/seats",
            new SelectSeatsRequest(seats.Select(s => new SeatAssignmentRequest(s.PassengerId, s.SeatNumber)).ToList()),
            SkyJourneyApiFactory.Json, Ct);

    /// <summary>Pays for a booking directly through the domain model (the payment API arrives in a later milestone).</summary>
    public static Task ConfirmAsync(this SkyJourneyApiFactory factory, string reference) =>
        factory.WithDbAsync(async db =>
        {
            var booking = await db.Bookings
                .Include(b => b.Passengers)
                .Include(b => b.Payments)
                .Include(b => b.Segments).ThenInclude(s => s.SeatReservations)
                .SingleAsync(b => b.Reference == reference, Ct);
            var now = factory.Clock.GetUtcNow();
            var attempt = booking.StartPayment(Guid.NewGuid().ToString(), "test", now);
            booking.CompletePayment(attempt, succeeded: true, "test-ref", null, now);
            await db.SaveChangesAsync(Ct);
        });
}
