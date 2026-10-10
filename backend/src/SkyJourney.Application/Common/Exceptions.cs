namespace SkyJourney.Application.Common;

public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.")
{
    public string Resource { get; } = resource;
}

/// <summary>The request conflicts with the current state of a resource (HTTP 409).</summary>
public sealed class ConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>The request is well-formed but semantically unacceptable (HTTP 422).</summary>
public sealed class UnprocessableException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>The caller is authenticated but not allowed to perform the operation (HTTP 403).</summary>
public sealed class ForbiddenException(string message) : Exception(message);
