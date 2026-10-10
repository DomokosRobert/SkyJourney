using SkyJourney.Application.Common;
using SkyJourney.Infrastructure.Identity;

namespace SkyJourney.Api.Common;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid UserId =>
        IsAuthenticated && Guid.TryParse(accessor.HttpContext!.User.FindFirst("sub")?.Value, out var id)
            ? id
            : throw new UnauthorizedException("unauthenticated", "Sign in to continue.");

    public bool IsAdmin => accessor.HttpContext?.User.IsInRole(Roles.Admin) == true;
}
