using Microsoft.AspNetCore.HostFiltering;
using Microsoft.Extensions.Options;
using Vairas.VehicleManagement.Configuration.Options;

namespace Vairas.VehicleManagement.Configuration;

/// <summary>Binds and validates every strongly typed options section; invalid required settings fail at startup.</summary>
public static class ConfigureOptions
{
    public static IServiceCollection AddVehicleOptions(this IServiceCollection services, IConfiguration configuration)
    {
        AddValidated<DatabaseOptions, DatabaseOptionsValidator>(services, configuration.GetSection("ConnectionStrings"));
        AddValidated<JwtOptions, JwtOptionsValidator>(services, configuration.GetSection("Authentication"));
        AddValidated<DependencyOptions, DependencyOptionsValidator>(services, configuration.GetSection("Dependencies"));
        AddValidated<CatalogueOptions, CatalogueOptionsValidator>(services, configuration.GetSection("Catalogue"));
        services.AddOptions<SchedulingOptions>().Bind(configuration.GetSection("Scheduling"));
        services.AddOptions<WorkerOptions>().Bind(configuration.GetSection("Workers"));
        services.AddOptions<HostFilteringOptions>().ValidateOnStart();
        services.AddSingleton<IValidateOptions<HostFilteringOptions>, AllowedHostsValidator>();
        return services;
    }

    private static void AddValidated<TOptions, TValidator>(IServiceCollection services, IConfigurationSection section)
        where TOptions : class
        where TValidator : class, IValidateOptions<TOptions>
    {
        services.AddOptions<TOptions>().Bind(section).ValidateOnStart();
        services.AddSingleton<IValidateOptions<TOptions>, TValidator>();
    }
}
