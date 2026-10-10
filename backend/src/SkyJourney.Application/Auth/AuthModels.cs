namespace SkyJourney.Application.Auth;

public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record UpdateProfileRequest(string FirstName, string LastName);

public sealed record UserProfileDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt);

/// <summary>
/// Tokens issued on registration, login and refresh. The refresh token is returned exactly once;
/// the server only keeps its hash.
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserProfileDto User);
