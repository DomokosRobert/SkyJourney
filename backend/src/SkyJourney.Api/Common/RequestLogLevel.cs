using Serilog.Events;

namespace SkyJourney.Api.Common;

/// <summary>Chooses the level of the per-request log line written by Serilog's request logging.</summary>
public static class RequestLogLevel
{
    /// <summary>Status set when the client disconnected before the response was written (nginx convention).</summary>
    public const int ClientClosedRequest = 499;

    public static LogEventLevel For(HttpContext context, double elapsedMs, Exception? exception) =>
        exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError ? LogEventLevel.Error
        : context.Response.StatusCode == ClientClosedRequest ? LogEventLevel.Debug
        : LogEventLevel.Information;
}
