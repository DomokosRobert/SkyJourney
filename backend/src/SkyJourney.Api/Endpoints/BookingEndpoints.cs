using Microsoft.AspNetCore.Mvc;
using SkyJourney.Api.Common;
using SkyJourney.Application.Bookings;
using SkyJourney.Application.Common;
using SkyJourney.Application.Payments;

namespace SkyJourney.Api.Endpoints;

public static class BookingEndpoints
{
    /// <summary>Set to <c>true</c> on a create response that replays an earlier request with the same key.</summary>
    public const string ReplayedHeader = "Idempotent-Replayed";

    public static RouteGroupBuilder MapBookingEndpoints(this RouteGroupBuilder api)
    {
        var bookings = api.MapGroup("/bookings").WithTags("Bookings")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        bookings.MapPost("/", async (CreateBookingRequest request,
                [FromHeader(Name = IdempotencyKey.HeaderName)] string? idempotencyKey,
                BookingService service, HttpContext http, CancellationToken ct) =>
            {
                var result = await service.CreateAsync(request, IdempotencyKey.Require(idempotencyKey), ct);
                http.Response.Headers[ReplayedHeader] = result.Created ? "false" : "true";
                return result.Created
                    ? Results.Created($"/api/v1/bookings/{result.Booking.Reference}", result.Booking)
                    : Results.Ok(result.Booking);
            })
            .Validate<CreateBookingRequest>()
            .WithName("CreateBooking")
            .WithSummary("Hold capacity on the chosen flights for 20 minutes and create an unpaid booking.")
            .WithDescription("Requires an Idempotency-Key header. Repeating a request with the same key returns the original " +
                             "booking (200); reusing a key for a different request returns 422.")
            .Produces<BookingDto>(StatusCodes.Status201Created)
            .Produces<BookingDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        bookings.MapGet("/", (int? page, int? pageSize, BookingService service, CancellationToken ct) =>
                service.ListAsync(new PageRequest(page ?? 1, pageSize ?? 20), ct))
            .WithName("ListMyBookings")
            .WithSummary("The caller's bookings, newest first.")
            .Produces<PagedResult<BookingSummaryDto>>();

        bookings.MapGet("/{reference}", (string reference, BookingService service, CancellationToken ct) =>
                service.GetAsync(reference, ct))
            .WithName("GetBooking")
            .Produces<BookingDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        bookings.MapGet("/{reference}/segments/{segmentId:guid}/seat-map",
                (string reference, Guid segmentId, BookingService service, CancellationToken ct) =>
                    service.GetSeatMapAsync(reference, segmentId, ct))
            .WithName("GetSeatMap")
            .WithSummary("The aircraft layout with availability and the price of each seat for this booking's fare.")
            .Produces<SeatMapDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        bookings.MapPut("/{reference}/segments/{segmentId:guid}/seats",
                (string reference, Guid segmentId, SelectSeatsRequest request, BookingService service, CancellationToken ct) =>
                    service.SelectSeatsAsync(reference, segmentId, request, ct))
            .Validate<SelectSeatsRequest>()
            .WithName("SelectSeats")
            .WithSummary("Hold seats for passengers on one flight of an unpaid booking.")
            .Produces<BookingDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        bookings.MapPost("/{reference}/payments", async (string reference, PayBookingRequest request,
                [FromHeader(Name = IdempotencyKey.HeaderName)] string? idempotencyKey,
                PaymentService service, HttpContext http, CancellationToken ct) =>
            {
                var result = await service.PayAsync(reference, request, IdempotencyKey.Require(idempotencyKey), ct);
                http.Response.Headers[ReplayedHeader] = result.Created ? "false" : "true";
                return result.Created
                    ? Results.Created($"/api/v1/bookings/{result.Payment.Booking.Reference}", result.Payment)
                    : Results.Ok(result.Payment);
            })
            .Validate<PayBookingRequest>()
            .RequireRateLimiting(AuthConfiguration.PaymentRateLimitPolicy)
            .WithName("PayBooking")
            .WithSummary("Pay for an unpaid booking with a test payment token (simulated; nothing is charged).")
            .WithDescription("Tokens: sim_success, sim_declined, sim_insufficient_funds. Requires an Idempotency-Key header. " +
                             "A declined payment is a normal result (status Failed) and the booking stays payable until its " +
                             "hold expires; retry with a new key. Repeating a key returns the recorded outcome (200).")
            .Produces<PaymentResultDto>(StatusCodes.Status201Created)
            .Produces<PaymentResultDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        bookings.MapGet("/{reference}/cancellation", (string reference, BookingService service, CancellationToken ct) =>
                service.GetCancellationQuoteAsync(reference, ct))
            .WithName("GetCancellationQuote")
            .WithSummary("Whether the booking can be cancelled now, and the refund that would be issued.")
            .Produces<CancellationQuoteDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        bookings.MapPost("/{reference}/cancel", (string reference, BookingService service, CancellationToken ct) =>
                service.CancelAsync(reference, ct))
            .WithName("CancelBooking")
            .WithSummary("Cancel the booking, release its seats and refund according to the fare rules (simulated).")
            .Produces<BookingDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return api;
    }
}
