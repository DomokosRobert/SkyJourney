using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SkyJourney.Application.Common;
using SkyJourney.Infrastructure.Identity;

namespace SkyJourney.Api.Common;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Requests per client IP and window on the anonymous auth endpoints (login, register, refresh, logout).</summary>
    public int AuthPermitLimit { get; init; } = 10;

    public int AuthWindowSeconds { get; init; } = 60;
}

public static class AuthConfiguration
{
    public const string AdminPolicy = "Admin";
    public const string AuthRateLimitPolicy = "auth";

    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, JwtSigningKey, TimeProvider>((options, jwt, signingKey, timeProvider) =>
            {
                options.MapInboundClaims = false; // keep "sub" and "role" as issued
                options.TimeProvider = timeProvider;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = signingKey.Key,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = "sub",
                    RoleClaimType = ClaimNames.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    // Checked against the injected clock (not DateTime.UtcNow) so expiry is testable.
                    LifetimeValidator = (notBefore, expires, _, parameters) =>
                    {
                        var now = timeProvider.GetUtcNow().UtcDateTime;
                        return (notBefore is null || notBefore <= now + parameters.ClockSkew)
                            && expires is not null && expires > now - parameters.ClockSkew;
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicy, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Admin));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRateLimitedProblemAsync;
            options.AddPolicy(AuthRateLimitPolicy, context =>
            {
                var settings = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.AuthPermitLimit,
                        Window = TimeSpan.FromSeconds(settings.AuthWindowSeconds),
                        QueueLimit = 0,
                    });
            });
        });

        // Native apps do not need CORS. Origins are only configured for browser clients (e.g. Expo web).
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            if (origins.Length > 0)
            {
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()
                    .WithExposedHeaders(CorrelationIdMiddleware.HeaderName);
            }
        }));

        return services;
    }

    private static async ValueTask WriteRateLimitedProblemAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var problems = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests",
                Detail = "Too many attempts. Wait a moment and try again.",
                Extensions = { ["code"] = "rate_limited" },
            },
        });
    }
}
