namespace SkyJourney.IntegrationTests.Infrastructure;

/// <summary>The subset of RFC 9457 Problem Details the API returns.</summary>
public sealed record ProblemResponse(int Status, string Title, string? Code, Dictionary<string, string[]>? Errors);
