namespace SkyJourney.Infrastructure.Identity;

/// <summary>
/// A refresh token, stored only as a SHA-256 hash. All tokens descending from one login share a
/// <see cref="FamilyId"/>, so presenting a rotated token can revoke the whole chain (reuse detection).
/// </summary>
public sealed class RefreshToken
{
    public const int HashLength = 64;

    private RefreshToken()
    {
    }

    public RefreshToken(Guid userId, Guid familyId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt, Guid? id = null)
    {
        Id = id ?? Guid.CreateVersion7();
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public RefreshTokenRevocation? RevocationReason { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }
}

public enum RefreshTokenRevocation
{
    Rotated,
    Logout,
    ReuseDetected,
}
