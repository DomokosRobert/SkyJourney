using SkyJourney.Api.Common;
using SkyJourney.Application.Flights;

namespace SkyJourney.Api.Endpoints;

public static class FlightEndpoints
{
    public static RouteGroupBuilder MapFlightEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/flights").WithTags("Flights");

        group.MapGet("/search", ([AsParameters] FlightSearchQuery query, FlightSearchService flights, CancellationToken ct) =>
                flights.SearchAsync(query, ct))
            .Validate<FlightSearchQuery>()
            .WithName("SearchFlights")
            .WithSummary("Search direct flights, one-way or return. All results are demo data.")
            .Produces<FlightSearchResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/lowest-fares", (string origin, string destination, DateOnly from, DateOnly to,
                    FlightSearchService flights, CancellationToken ct) =>
                flights.GetLowestFaresAsync(origin, destination, from, to, ct))
            .WithName("GetLowestFares")
            .WithSummary("Cheapest available fare per day on a route, for calendar price hints.")
            .Produces<LowestFaresResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{flightId:guid}", (Guid flightId, int? passengers, FlightSearchService flights, CancellationToken ct) =>
                flights.GetFlightAsync(flightId, passengers ?? 1, ct))
            .WithName("GetFlight")
            .Produces<FlightOfferDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }
}
