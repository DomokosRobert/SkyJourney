using Serilog;
using SkyJourney.Api.Common;
using SkyJourney.Api.Endpoints;
using SkyJourney.Application;
using SkyJourney.Infrastructure;
using SkyJourney.Infrastructure.Identity;
using SkyJourney.Infrastructure.Persistence;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration)
        .AddIdentityInfrastructure(builder.Environment)
        .AddApiServices(builder.Configuration);

    var app = builder.Build();

    if (args is [AdminProvisioning.CommandName, var adminEmail])
    {
        var (succeeded, message) = await AdminProvisioning.PromoteAsync(app.Services, adminEmail);
        Console.WriteLine(message);
        Environment.ExitCode = succeeded ? 0 : 1;
        return;
    }

    await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration);

    if (app.Services.GetRequiredService<JwtSigningKey>().IsEphemeral)
    {
        app.Logger.LogWarning("No Jwt:SigningKey configured; using a random key for this process. " +
            "Access tokens become invalid on restart (clients recover through refresh).");
    }

    // Correlation and request logging wrap the exception handler, so every log line (including the
    // handler's) carries the correlation id and the request log records the final status code.
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging(options =>
    {
        // Only method, path, status and timing are logged: never bodies, query strings or headers.
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0} ms";
        options.GetLevel = RequestLogLevel.For;
    });
    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    if (app.Configuration.GetValue<bool>("OpenApi:Enabled"))
    {
        app.MapOpenApi();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "SkyJourney API v1");
            options.RoutePrefix = "swagger";
        });
    }

    app.MapHealthEndpoints();

    var api = app.MapGroup("/api/v1");
    api.MapAirportEndpoints();
    api.MapFlightEndpoints();
    api.MapAuthEndpoints();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "SkyJourney API terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
