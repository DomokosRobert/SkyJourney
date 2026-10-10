using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using SkyJourney.Api.Endpoints;
using SkyJourney.Application.Bookings;
using SkyJourney.Application.Payments;
using SkyJourney.Domain.Bookings;
using SkyJourney.Infrastructure.Payments;
using SkyJourney.IntegrationTests.Bookings;
using SkyJourney.IntegrationTests.Infrastructure;

namespace SkyJourney.IntegrationTests.Payments;

/// <summary>
/// Simulated checkout over HTTP against PostgreSQL: outcomes, idempotency, concurrent attempts and hold expiry.
/// Each test uses its own route and date (see docs/testing.md).
/// </summary>
public sealed class PaymentTests(SkyJourneyApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<List<PaymentAttempt>> AttemptsAsync(Guid bookingId) =>
        factory.WithDbAsync(db => db.PaymentAttempts.AsNoTracking().Where(p => p.BookingId == bookingId).ToListAsync(Ct));

    [Fact]
    public async Task A_successful_payment_confirms_the_booking_and_its_seats_and_replays_by_key()
    {
        var flight = await factory.FindFlightAsync("BUD", "ATH", new DateOnly(2026, 11, 8));
        var client = await factory.CreateUserClientAsync();
        var booking = await client.CreateBookingAsync(BookingTestHelpers.Request(flight.Id));
        Assert.Equal(HttpStatusCode.OK, (await client.PutSeatsAsync(booking, 0, (booking.Passengers[0].Id, "14C"))).StatusCode);
        var priced = (await client.GetFromJsonAsync<BookingDto>($"/api/v1/bookings/{booking.Reference}", SkyJourneyApiFactory.Json, Ct))!;
        var key = Guid.NewGuid().ToString();

        var response = await client.PayAsync(booking, idempotencyKey: key);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("false", Assert.Single(response.Headers.GetValues(BookingEndpoints.ReplayedHeader)));
        Assert.Equal($"/api/v1/bookings/{booking.Reference}", response.Headers.Location?.ToString());
        var payment = await response.ReadAsAsync<PaymentResultDto>();
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(priced.TotalPrice, payment.Amount);
        Assert.Null(payment.FailureCode);
        Assert.Equal(BookingStatus.Confirmed, payment.Booking.Status);
        Assert.Null(payment.Booking.ExpiresAt);

        var attempt = Assert.Single(await AttemptsAsync(booking.Id));
        Assert.StartsWith("sim_", attempt.ProviderReference);
        Assert.Equal("Simulated", attempt.Provider);
        var seat = await factory.WithDbAsync(db => db.SeatReservations.AsNoTracking()
            .SingleAsync(r => r.BookingPassengerId == booking.Passengers[0].Id, Ct));
        Assert.Equal(SeatReservationStatus.Confirmed, seat.Status);
        Assert.Equal(flight.SeatsSold + 1, await factory.SeatsSoldAsync(flight.Id));

        var replay = await client.PayAsync(booking, idempotencyKey: key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues(BookingEndpoints.ReplayedHeader)));
        Assert.Equal(payment.PaymentId, (await replay.ReadAsAsync<PaymentResultDto>()).PaymentId);

        var reused = await client.PayAsync(booking, SimulatedPaymentGateway.DeclinedToken, key);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
        Assert.Equal("idempotency_key_reused", (await reused.ReadAsAsync<ProblemResponse>()).Code);

        var again = await client.PayAsync(booking);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("booking_not_payable", (await again.ReadAsAsync<ProblemResponse>()).Code);

        var seatChange = await client.PutSeatsAsync(booking, 0, (booking.Passengers[0].Id, "15C"));
        Assert.Equal(HttpStatusCode.Conflict, seatChange.StatusCode);
        Assert.Equal("seats_locked", (await seatChange.ReadAsAsync<ProblemResponse>()).Code);
        Assert.Single(await AttemptsAsync(booking.Id));
    }

    [Fact]
    public async Task Declined_payments_are_recorded_and_leave_the_booking_payable()
    {
        var flight = await factory.FindFlightAsync("BUD", "BCN", new DateOnly(2026, 11, 8));
        var client = await factory.CreateUserClientAsync();
        var booking = await client.CreateBookingAsync(BookingTestHelpers.Request(flight.Id));

        (string Token, string Code)[] declines =
        [
            (SimulatedPaymentGateway.DeclinedToken, PaymentFailures.CardDeclined),
            (SimulatedPaymentGateway.InsufficientFundsToken, PaymentFailures.InsufficientFunds),
            ("sim_unknown", PaymentFailures.InvalidPaymentToken),
        ];
        foreach (var (token, code) in declines)
        {
            var response = await client.PayAsync(booking, token);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var payment = await response.ReadAsAsync<PaymentResultDto>();
            Assert.Equal(PaymentStatus.Failed, payment.Status);
            Assert.Equal(code, payment.FailureCode);
            Assert.False(string.IsNullOrWhiteSpace(payment.FailureMessage));
            Assert.Equal(BookingStatus.PendingPayment, payment.Booking.Status);
            Assert.NotNull(payment.Booking.ExpiresAt);
        }

        await client.ConfirmAsync(booking);

        var attempts = await AttemptsAsync(booking.Id);
        Assert.Equal(4, attempts.Count);
        Assert.Single(attempts, a => a.Status == PaymentStatus.Succeeded);
        Assert.Equal(3, attempts.Count(a => a.Status == PaymentStatus.Failed));
        Assert.Equal(flight.SeatsSold + 1, await factory.SeatsSoldAsync(flight.Id));
    }

    [Fact]
    public async Task Concurrent_payments_with_different_keys_charge_the_booking_once()
    {
        var flight = await factory.FindFlightAsync("BUD", "BER", new DateOnly(2026, 11, 9));
        var client = await factory.CreateUserClientAsync();
        var booking = await client.CreateBookingAsync(BookingTestHelpers.Request(flight.Id, adults: 2));

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PayAsync(booking)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        foreach (var response in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains((await response.ReadAsAsync<ProblemResponse>()).Code, new[] { "payment_in_progress", "booking_not_payable" });
        }

        var attempts = await AttemptsAsync(booking.Id);
        Assert.Single(attempts, a => a.Status == PaymentStatus.Succeeded);
        Assert.DoesNotContain(attempts, a => a.Status == PaymentStatus.Pending);
        Assert.Equal(flight.SeatsSold + 2, await factory.SeatsSoldAsync(flight.Id));
    }

    [Fact]
    public async Task Concurrent_retries_with_one_key_create_a_single_payment()
    {
        var flight = await factory.FindFlightAsync("BUD", "IST", new DateOnly(2026, 11, 9));
        var client = await factory.CreateUserClientAsync();
        var booking = await client.CreateBookingAsync(BookingTestHelpers.Request(flight.Id));
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.PayAsync(booking, idempotencyKey: key)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        foreach (var response in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Assert.Equal(PaymentStatus.Succeeded, (await response.ReadAsAsync<PaymentResultDto>()).Status);
            }
            else
            {
                // A retry that arrives while the first request is still charging is told to try again.
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                Assert.Equal("payment_in_progress", (await response.ReadAsAsync<ProblemResponse>()).Code);
            }
        }

        var attempt = Assert.Single(await AttemptsAsync(booking.Id));
        Assert.Equal(PaymentStatus.Succeeded, attempt.Status);
    }

    [Fact]
    public async Task Paying_after_the_hold_lapsed_is_rejected_and_releases_the_booking()
    {
        var clock = new FakeTimeProvider(SkyJourneyApiFactory.StartTime);
        await using var host = factory.WithOwnClock(clock);
        var flight = await factory.FindFlightAsync("BUD", "MUC", new DateOnly(2026, 11, 10));
        var email = AuthHelpers.UniqueEmail();
        var client = await host.CreateUserClientAsync(email);
        var booking = await client.CreateBookingAsync(BookingTestHelpers.Request(flight.Id));

        clock.Advance(Booking.HoldDuration + TimeSpan.FromMinutes(1));
        await client.SignInAgainAsync(email);
        var response = await client.PayAsync(booking);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("booking_expired", (await response.ReadAsAsync<ProblemResponse>()).Code);
        var stored = await factory.WithDbAsync(db => db.Bookings.AsNoTracking().SingleAsync(b => b.Id == booking.Id, Ct));
        Assert.Equal(BookingStatus.Expired, stored.Status);
        Assert.Empty(await AttemptsAsync(booking.Id));
        Assert.Equal(flight.SeatsSold, await factory.SeatsSoldAsync(flight.Id));
    }

    [Fact]
    public async Task Payment_requests_are_validated_and_scoped_to_the_owner()
    {
        var flight = await factory.FindFlightAsync("BUD", "SPU", new DateOnly(2026, 11, 10));
        var client = await factory.CreateUserClientAsync();
        var booking = await client.CreateBookingAsync(BookingTestHelpers.Request(flight.Id));

        using (var withoutKey = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/bookings/{booking.Reference}/payments")
        {
            Content = JsonContent.Create(new PayBookingRequest(SimulatedPaymentGateway.SuccessToken), options: SkyJourneyApiFactory.Json),
        })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(withoutKey, Ct)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PayAsync(booking, "")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PayAsync(booking, "4111 1111 1111 1111")).StatusCode);

        var other = await factory.CreateUserClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.PayAsync(booking)).StatusCode);

        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PayAsync(booking)).StatusCode);

        Assert.Empty(await AttemptsAsync(booking.Id));
        Assert.Equal(BookingStatus.PendingPayment,
            (await client.GetFromJsonAsync<BookingDto>($"/api/v1/bookings/{booking.Reference}", SkyJourneyApiFactory.Json, Ct))!.Status);
    }

    [Fact]
    public async Task Payment_attempts_are_rate_limited_per_user()
    {
        await using var host = factory.WithOwnClock(new FakeTimeProvider(SkyJourneyApiFactory.StartTime),
            ("RateLimiting:PaymentPermitLimit", "2"), ("RateLimiting:PaymentWindowSeconds", "60"));
        var flight = await factory.FindFlightAsync("BUD", "TLV", new DateOnly(2026, 11, 11));
        var client = await host.CreateUserClientAsync();
        var booking = await client.CreateBookingAsync(BookingTestHelpers.Request(flight.Id));

        var first = await client.PayAsync(booking, SimulatedPaymentGateway.DeclinedToken);
        var second = await client.PayAsync(booking, SimulatedPaymentGateway.DeclinedToken);
        var limited = await client.PayAsync(booking);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("rate_limited", (await limited.ReadAsAsync<ProblemResponse>()).Code);
        Assert.Equal(2, (await AttemptsAsync(booking.Id)).Count);
    }
}
