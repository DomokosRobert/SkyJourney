namespace SkyJourney.Domain.Common;

/// <summary>
/// A business rule was violated. <see cref="Code"/> is a stable, machine-readable identifier
/// (e.g. <c>cancellation_window_closed</c>) that clients may switch on.
/// </summary>
public class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
