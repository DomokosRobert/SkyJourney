using SkyJourney.Api.Common;
using SkyJourney.Application.Admin;
using SkyJourney.Application.Common;
using SkyJourney.Domain.Bookings;
using SkyJourney.Domain.Flights;

namespace SkyJourney.Api.Endpoints;

/// <summary>
/// Back-office API. Every route requires the Admin role (granted with the <c>promote-admin</c> command; see the
/// README). Non-admin callers get 403, anonymous callers 401.
/// </summary>
public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin")
            .RequireAuthorization(AuthConfiguration.AdminPolicy)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        MapAirports(admin.MapGroup("/airports").WithTags("Admin: airports"));
        MapSchedules(admin.MapGroup("/schedules").WithTags("Admin: schedules"));
        MapFlights(admin.MapGroup("/flights").WithTags("Admin: flights"));
        MapBookings(admin.MapGroup("/bookings").WithTags("Admin: bookings"));
        return api;
    }

    private static void MapAirports(RouteGroupBuilder airports)
    {
        airports.MapGet("/", (AdminAirportService service, CancellationToken ct) => service.ListAsync(ct))
            .WithName("AdminListAirports")
            .WithSummary("All airports, including inactive ones.")
            .Produces<IReadOnlyList<AdminAirportDto>>();

        airports.MapPost("/", async (CreateAirportRequest request, AdminAirportService service, CancellationToken ct) =>
            {
                var airport = await service.CreateAsync(request, ct);
                return Results.Created($"/api/v1/airports/{airport.IataCode}", airport);
            })
            .Validate<CreateAirportRequest>()
            .WithName("AdminCreateAirport")
            .Produces<AdminAirportDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        airports.MapPut("/{iataCode}", (string iataCode, UpdateAirportRequest request, AdminAirportService service, CancellationToken ct) =>
                service.UpdateAsync(iataCode, request, ct))
            .Validate<UpdateAirportRequest>()
            .WithName("AdminUpdateAirport")
            .Produces<AdminAirportDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        airports.MapPost("/{iataCode}/activate", (string iataCode, AdminAirportService service, CancellationToken ct) =>
                service.SetActiveAsync(iataCode, true, ct))
            .WithName("AdminActivateAirport")
            .Produces<AdminAirportDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        airports.MapPost("/{iataCode}/deactivate", (string iataCode, AdminAirportService service, CancellationToken ct) =>
                service.SetActiveAsync(iataCode, false, ct))
            .WithName("AdminDeactivateAirport")
            .WithSummary("Hide the airport from search. Existing flights and bookings are unaffected.")
            .Produces<AdminAirportDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapSchedules(RouteGroupBuilder schedules)
    {
        schedules.MapGet("/", (int? page, int? pageSize, AdminScheduleService service, CancellationToken ct) =>
                service.ListAsync(new PageRequest(page ?? 1, pageSize ?? 20), ct))
            .WithName("AdminListSchedules")
            .Produces<PagedResult<AdminScheduleDto>>();

        schedules.MapPost("/", async (CreateScheduleRequest request, AdminScheduleService service, CancellationToken ct) =>
            {
                var result = await service.CreateAsync(request, ct);
                return Results.Created($"/api/v1/admin/schedules/{result.Schedule.Id}", result);
            })
            .Validate<CreateScheduleRequest>()
            .WithName("AdminCreateSchedule")
            .WithSummary("Create a recurring schedule and generate its flights for the booking horizon.")
            .Produces<CreateScheduleResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static void MapFlights(RouteGroupBuilder flights)
    {
        flights.MapGet("/", (DateOnly? date, string? origin, string? destination, string? flightNumber, FlightStatus? status,
                    int? page, int? pageSize, AdminFlightService service, CancellationToken ct) =>
                service.ListAsync(new AdminFlightQuery(date, origin, destination, flightNumber, status, page, pageSize), ct))
            .WithName("AdminListFlights")
            .WithSummary("Dated flights with load and fares, in departure order. Filter by local date, route, number or status.")
            .Produces<PagedResult<AdminFlightDto>>();

        flights.MapGet("/{flightId:guid}", (Guid flightId, AdminFlightService service, CancellationToken ct) =>
                service.GetAsync(flightId, ct))
            .WithName("AdminGetFlight")
            .Produces<AdminFlightDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        flights.MapPut("/{flightId:guid}/fares/{family}", (Guid flightId, string family, UpdateFareRequest request,
                    AdminFlightService service, CancellationToken ct) =>
                service.UpdateFareAsync(flightId, family, request, ct))
            .Validate<UpdateFareRequest>()
            .WithName("AdminUpdateFare")
            .WithSummary("Set a fare's price and availability. Existing bookings keep the price they were sold at.")
            .Produces<AdminFlightDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        flights.MapPost("/{flightId:guid}/cancel", (Guid flightId, CancelRequest request, AdminFlightService service,
                    CancellationToken ct) =>
                service.CancelAsync(flightId, request, ct))
            .Validate<CancelRequest>()
            .WithName("AdminCancelFlight")
            .WithSummary("Cancel the flight and all its active bookings with a full (simulated) refund. Safe to repeat.")
            .Produces<FlightCancellationResultDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapBookings(RouteGroupBuilder bookings)
    {
        bookings.MapGet("/", (string? reference, Guid? flightId, Guid? userId, BookingStatus? status, int? page, int? pageSize,
                    AdminBookingService service, CancellationToken ct) =>
                service.ListAsync(new AdminBookingQuery(reference, flightId, userId, status, page, pageSize), ct))
            .WithName("AdminListBookings")
            .WithSummary("All bookings, newest first. Filter by reference, flight, user or status.")
            .Produces<PagedResult<AdminBookingSummaryDto>>();

        bookings.MapGet("/{reference}", (string reference, AdminBookingService service, CancellationToken ct) =>
                service.GetAsync(reference, ct))
            .WithName("AdminGetBooking")
            .WithSummary("A booking with its owner id, status history and payment attempts.")
            .Produces<AdminBookingDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        bookings.MapPost("/{reference}/cancel", (string reference, CancelRequest request, AdminBookingService service,
                    CancellationToken ct) =>
                service.CancelAsync(reference, request, ct))
            .Validate<CancelRequest>()
            .WithName("AdminCancelBooking")
            .WithSummary("Cancel on behalf of the airline: confirmed bookings are refunded in full (simulated).")
            .Produces<AdminBookingDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
