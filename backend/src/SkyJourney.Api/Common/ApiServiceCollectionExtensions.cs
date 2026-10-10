using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using SkyJourney.Infrastructure.Persistence;

namespace SkyJourney.Api.Common;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
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

            // Responses produced by the framework (authentication, routing) get a machine-readable code too.
            if (!context.ProblemDetails.Extensions.ContainsKey("code") && DefaultCode(context.ProblemDetails.Status) is { } code)
            {
                context.ProblemDetails.Extensions["code"] = code;
            }
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddHealthChecks()
            .AddDbContextCheck<SkyJourneyDbContext>("database", tags: ["ready"]);

        services.AddApiAuthentication(configuration);

        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddOperationTransformer<BearerSecuritySchemeTransformer>();
        });
        return services;
    }

    private static string? DefaultCode(int? status) => status switch
    {
        StatusCodes.Status401Unauthorized => "unauthenticated",
        StatusCodes.Status403Forbidden => "forbidden",
        StatusCodes.Status404NotFound => "not_found",
        StatusCodes.Status405MethodNotAllowed => "method_not_allowed",
        _ => null,
    };

    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        // Liveness: the process is up. Readiness: dependencies (database) are reachable.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
        return app;
    }
}
