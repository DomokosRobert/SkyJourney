using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkyJourney.Application.Auth;
using SkyJourney.Application.Common;
using SkyJourney.Infrastructure.Persistence;

namespace SkyJourney.Infrastructure.Identity;

internal sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    SkyJourneyDbContext db,
    AccessTokenIssuer accessTokens,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    // Verified against when the e-mail is unknown, so both failure paths cost one password hash.
    private static readonly Lazy<string> DummyPasswordHash =
        new(() => new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Guid.NewGuid().ToString()));

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            CreatedAt = timeProvider.GetUtcNow(),
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
            {
                throw new ConflictException("email_taken", "An account with this e-mail address already exists.");
            }

            throw new ValidationException(result.Errors.Select(e => new ValidationFailure(
                e.Code.StartsWith("Password", StringComparison.Ordinal) ? "password" : "email", e.Description)));
        }

        logger.LogInformation("User {UserId} registered", user.Id);
        return await IssueTokensAsync(user, familyId: Guid.CreateVersion7(), cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            userManager.PasswordHasher.VerifyHashedPassword(new ApplicationUser(), DummyPasswordHash.Value, request.Password);
            throw InvalidCredentials();
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            throw AccountLocked();
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            logger.LogInformation("Failed login for user {UserId}", user.Id);
            throw await userManager.IsLockedOutAsync(user) ? AccountLocked() : InvalidCredentials();
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return await IssueTokensAsync(user, familyId: Guid.CreateVersion7(), cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var hash = RefreshTokenSecret.Hash(request.RefreshToken);
        var token = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (token is null || token.ExpiresAt <= now)
        {
            throw InvalidRefreshToken();
        }

        if (token.RevokedAt is not null)
        {
            await HandleRevokedTokenAsync(token, now, cancellationToken);
            throw InvalidRefreshToken();
        }

        var user = await userManager.FindByIdAsync(token.UserId.ToString());
        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            throw InvalidRefreshToken();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Claim the token atomically: of two concurrent refreshes with the same token, only one wins.
        var replacementId = Guid.CreateVersion7();
        var claimed = await db.RefreshTokens
            .Where(t => t.Id == token.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.RevocationReason, RefreshTokenRevocation.Rotated)
                .SetProperty(t => t.ReplacedByTokenId, replacementId), cancellationToken);

        if (claimed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            await RevokeFamilyAsync(token.FamilyId, now, RefreshTokenRevocation.ReuseDetected, cancellationToken);
            logger.LogWarning("Concurrent reuse of refresh token family {FamilyId}; family revoked", token.FamilyId);
            throw InvalidRefreshToken();
        }

        var response = await IssueTokensAsync(user, token.FamilyId, cancellationToken, replacementId);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        var hash = RefreshTokenSecret.Hash(request.RefreshToken);
        var familyId = await db.RefreshTokens
            .Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.FamilyId)
            .SingleOrDefaultAsync(cancellationToken);

        if (familyId is { } family)
        {
            await RevokeFamilyAsync(family, timeProvider.GetUtcNow(), RefreshTokenRevocation.Logout, cancellationToken);
        }
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(userId);
        return await ToProfileAsync(user);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(userId);
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new ConflictException("profile_update_failed", string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        return await ToProfileAsync(user);
    }

    private async Task HandleRevokedTokenAsync(RefreshToken token, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // A rotated token presented again means it was copied: the legitimate client already holds its
        // replacement. Revoke the whole family so neither copy can be used any longer.
        if (token.RevocationReason == RefreshTokenRevocation.Rotated)
        {
            await RevokeFamilyAsync(token.FamilyId, now, RefreshTokenRevocation.ReuseDetected, cancellationToken);
            logger.LogWarning("Reuse of rotated refresh token detected for user {UserId}; family {FamilyId} revoked",
                token.UserId, token.FamilyId);
        }
    }

    private Task<int> RevokeFamilyAsync(Guid familyId, DateTimeOffset now, RefreshTokenRevocation reason, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.RevocationReason, reason), cancellationToken);

    private async Task<AuthResponse> IssueTokensAsync(
        ApplicationUser user, Guid familyId, CancellationToken cancellationToken, Guid? refreshTokenId = null)
    {
        var now = timeProvider.GetUtcNow();
        var roles = (await userManager.GetRolesAsync(user)).Order(StringComparer.Ordinal).ToList();
        var (accessToken, accessExpiresAt) = accessTokens.Issue(user, roles);

        var refreshToken = RefreshTokenSecret.Generate();
        var refreshExpiresAt = now.AddDays(jwtOptions.Value.RefreshTokenDays);
        db.RefreshTokens.Add(new RefreshToken(
            user.Id, familyId, RefreshTokenSecret.Hash(refreshToken), now, refreshExpiresAt, refreshTokenId));
        await db.SaveChangesAsync(cancellationToken);

        return new AuthResponse(accessToken, accessExpiresAt, refreshToken, refreshExpiresAt, ToProfile(user, roles));
    }

    private async Task<ApplicationUser> FindUserAsync(Guid userId) =>
        await userManager.FindByIdAsync(userId.ToString())
        ?? throw new UnauthorizedException("user_not_found", "The account no longer exists.");

    private async Task<UserProfileDto> ToProfileAsync(ApplicationUser user) =>
        ToProfile(user, (await userManager.GetRolesAsync(user)).Order(StringComparer.Ordinal).ToList());

    private static UserProfileDto ToProfile(ApplicationUser user, IReadOnlyList<string> roles) =>
        new(user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName, roles, user.CreatedAt);

    private static UnauthorizedException InvalidCredentials() =>
        new("invalid_credentials", "The e-mail address or password is incorrect.");

    private static UnauthorizedException AccountLocked() =>
        new("account_locked", "Too many failed sign-in attempts. Try again in a few minutes.");

    private static UnauthorizedException InvalidRefreshToken() =>
        new("invalid_refresh_token", "The session has expired. Sign in again.");
}
