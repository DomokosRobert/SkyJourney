using SkyJourney.Api.Common;
using SkyJourney.Application.Auth;
using SkyJourney.Application.Common;

namespace SkyJourney.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Auth")
            .AllowAnonymous()
            .RequireRateLimiting(AuthConfiguration.AuthRateLimitPolicy)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/register", async (RegisterRequest request, IAuthService service, CancellationToken ct) =>
                TypedResults.Created("/api/v1/me", await service.RegisterAsync(request, ct)))
            .Validate<RegisterRequest>()
            .WithName("Register")
            .WithSummary("Create an account and sign in.")
            .ProducesProblem(StatusCodes.Status409Conflict);

        auth.MapPost("/login", (LoginRequest request, IAuthService service, CancellationToken ct) =>
                service.LoginAsync(request, ct))
            .Validate<LoginRequest>()
            .WithName("Login")
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("/refresh", (RefreshRequest request, IAuthService service, CancellationToken ct) =>
                service.RefreshAsync(request, ct))
            .Validate<RefreshRequest>()
            .WithName("RefreshTokens")
            .WithSummary("Exchange a refresh token for new tokens. Each refresh token can be used once.")
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("/logout", async (LogoutRequest request, IAuthService service, CancellationToken ct) =>
            {
                await service.LogoutAsync(request, ct);
                return TypedResults.NoContent();
            })
            .Validate<LogoutRequest>()
            .WithName("Logout")
            .WithSummary("Revoke the session that the refresh token belongs to.");

        var me = api.MapGroup("/me").WithTags("Profile")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        me.MapGet("/", (ICurrentUser user, IAuthService service, CancellationToken ct) =>
                service.GetProfileAsync(user.UserId, ct))
            .WithName("GetProfile")
            .Produces<UserProfileDto>();

        me.MapPut("/", (UpdateProfileRequest request, ICurrentUser user, IAuthService service, CancellationToken ct) =>
                service.UpdateProfileAsync(user.UserId, request, ct))
            .Validate<UpdateProfileRequest>()
            .WithName("UpdateProfile")
            .Produces<UserProfileDto>();

        return api;
    }
}
