using Serilog;
using SkyJourney.Api.Common;
using SkyJourney.Application;
using SkyJourney.Infrastructure;

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
        .AddApiServices();

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging(options =>
    {
        // Only method, path, status and timing are logged: never bodies, query strings or headers.
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0} ms";
    });

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
