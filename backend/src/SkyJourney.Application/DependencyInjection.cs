using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SkyJourney.Application.Airports;
using SkyJourney.Application.Bookings;
using SkyJourney.Application.FlightData;
using SkyJourney.Application.Flights;

namespace SkyJourney.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<AirportService>();
        services.AddScoped<FlightSearchService>();
        services.AddScoped<FlightDataImporter>();
        services.AddScoped<BookingService>();
        services.AddScoped<BookingExpiryService>();
        return services;
    }
}
