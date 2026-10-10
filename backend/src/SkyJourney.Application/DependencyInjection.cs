using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SkyJourney.Application.Admin;
using SkyJourney.Application.Airports;
using SkyJourney.Application.Bookings;
using SkyJourney.Application.FlightData;
using SkyJourney.Application.Flights;
using SkyJourney.Application.Payments;

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
        services.AddScoped<PaymentService>();
        services.AddScoped<AdminAirportService>();
        services.AddScoped<AdminScheduleService>();
        services.AddScoped<AdminFlightService>();
        services.AddScoped<AdminBookingService>();
        return services;
    }
}
