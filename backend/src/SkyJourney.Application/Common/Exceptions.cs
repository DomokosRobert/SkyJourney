using Microsoft.EntityFrameworkCore;

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

/// <summary>The caller could not be authenticated (HTTP 401).</summary>
public sealed class UnauthorizedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>
/// A unique index rejected a write (PostgreSQL 23505). Raised by the DbContext so application code can react to
/// specific constraints (e.g. a seat taken by a concurrent request) without depending on the database driver.
/// </summary>
public sealed class UniqueConstraintViolationException(string? constraintName, Exception innerException)
    : DbUpdateException($"Unique constraint '{constraintName}' was violated.", innerException)
{
    public string? ConstraintName { get; } = constraintName;
}
