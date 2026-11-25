using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Integrations;

namespace Vairas.VehicleManagement.Configuration;

public static class ConfigureHttpClients
{
    public static IServiceCollection AddDependencyHttpClients(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddTransient<DependencyCorrelationHandler>();
        services.AddHttpClient("dependencies", client => client.Timeout = TimeSpan.FromSeconds(5))
            .AddHttpMessageHandler<DependencyCorrelationHandler>();
        services.AddSingleton<IWorkloadTokens, WorkloadTokens>();
        services.AddScoped<IDependencyTransport, DependencyTransport>();
        services.AddScoped<ISchoolAuthority, SchoolAuthorityClient>();
        services.AddScoped<IUserProfiles, UserProfilesClient>();
        services.AddScoped<IVehicleActorFactory, VehicleActorFactory>();
        services.AddScoped<ISchedulingGate, SchedulingGate>();
        return services;
    }
}
