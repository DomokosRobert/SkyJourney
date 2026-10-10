namespace SkyJourney.Application.Auth;

/// <summary>Account and token operations (implemented in Infrastructure on top of ASP.NET Core Identity).</summary>
public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>Rotates a refresh token. Presenting an already-rotated token revokes its whole family.</summary>
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken);

    /// <summary>Revokes the refresh token's family. Unknown or already revoked tokens are ignored.</summary>
    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);

    Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken);
}
