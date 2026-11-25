using Microsoft.AspNetCore.Authorization;
using Vairas.VehicleManagement.Security;

namespace Vairas.VehicleManagement.Configuration;

public static class ConfigureAuthorization
{
    private static readonly string[] WorkloadScopes = ["vehicles.eligibility.read"];

    public static IServiceCollection AddEntryAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("VehicleEntry", policy => policy.AddRequirements(new EntryRequirement()));
            foreach (var scope in WorkloadScopes)
                options.AddPolicy(scope, policy => policy.AddRequirements(new EntryRequirement(scope)));
        });
        services.AddScoped<IAuthorizationHandler, EntryHandler>();
        return services;
    }
}
