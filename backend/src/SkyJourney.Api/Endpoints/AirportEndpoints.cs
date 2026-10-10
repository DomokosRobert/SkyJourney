using SkyJourney.Application.Airports;
using SkyJourney.Application.Common;

namespace SkyJourney.Api.Endpoints;

public static class AirportEndpoints
{
    public static RouteGroupBuilder MapAirportEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/airports").WithTags("Airports");

        group.MapGet("/", (string? query, int? limit, AirportService airports, CancellationToken ct) =>
                airports.SearchAsync(query, limit, ct))
            .WithName("SearchAirports")
            .WithSummary("Search active airports by IATA code, city or name.")
            .Produces<IReadOnlyList<AirportDto>>();

        group.MapGet("/{iataCode}", (string iataCode, AirportService airports, CancellationToken ct) =>
                airports.GetAsync(iataCode, ct))
            .WithName("GetAirport")
            .Produces<AirportDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{iataCode}/destinations", (string iataCode, AirportService airports, CancellationToken ct) =>
                airports.GetDestinationsAsync(iataCode, ct))
            .WithName("GetAirportDestinations")
            .WithSummary("Airports with upcoming flights from the given airport.")
            .Produces<IReadOnlyList<AirportDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }
}
