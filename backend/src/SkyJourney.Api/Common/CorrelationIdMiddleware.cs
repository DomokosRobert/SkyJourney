using System.Text.RegularExpressions;
using Serilog.Context;

namespace SkyJourney.Api.Common;

/// <summary>
/// Accepts a caller-supplied <c>X-Correlation-Id</c> (if it is safe) or creates one, echoes it on the
/// response and adds it to every log event written while the request is processed.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        var correlationId = SafeId().IsMatch(incoming) ? incoming : Guid.NewGuid().ToString("N");

        context.Items[HeaderName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    public static string? Get(HttpContext context) => context.Items[HeaderName] as string;

    // Prevents log forging / header injection through the correlation id.
    [GeneratedRegex("^[A-Za-z0-9-]{8,64}$")]
    private static partial Regex SafeId();
}
