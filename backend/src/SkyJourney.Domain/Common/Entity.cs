namespace SkyJourney.Domain.Common;

public abstract class Entity
{
    /// <summary>Time-ordered UUIDv7: globally unique, safe to expose, index-friendly.</summary>
    public Guid Id { get; protected init; } = Guid.CreateVersion7();
}
