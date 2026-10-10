namespace SkyJourney.Application.Common;

/// <summary>The authenticated caller of the current request.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>The caller's user id. Throws <see cref="UnauthorizedException"/> when the request is anonymous.</summary>
    Guid UserId { get; }

    bool IsAdmin { get; }
}
