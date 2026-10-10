using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using SkyJourney.Infrastructure.Persistence;

namespace SkyJourney.Api.Common;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            if (CorrelationIdMiddleware.Get(context.HttpContext) is { } correlationId)
            {
                context.ProblemDetails.Extensions["correlationId"] = correlationId;
            }
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddHealthChecks()
            .AddDbContextCheck<SkyJourneyDbContext>("database", tags: ["ready"]);

        services.AddOpenApi("v1");
        return services;
    }

    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        // Liveness: the process is up. Readiness: dependencies (database) are reachable.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
        return app;
    }
}
