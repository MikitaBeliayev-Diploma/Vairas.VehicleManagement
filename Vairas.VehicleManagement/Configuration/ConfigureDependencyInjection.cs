using Vairas.VehicleManagement.Business;

namespace Vairas.VehicleManagement.Configuration;

/// <summary>Application services; infrastructure registrations live in the dedicated Configure* classes.</summary>
public static class ConfigureDependencyInjection
{
    public static IServiceCollection AddVehicleServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IStateChangeProcessor, StateChangeProcessor>();
        services.AddSingleton<ICategoryCatalogue, CategoryCatalogue>();
        return services;
    }
}
