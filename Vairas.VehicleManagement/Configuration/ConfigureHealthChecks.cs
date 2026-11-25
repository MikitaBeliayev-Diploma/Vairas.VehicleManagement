using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Vairas.VehicleManagement.HealthChecks;

namespace Vairas.VehicleManagement.Configuration;

public static class ConfigureHealthChecks
{
    public static IServiceCollection AddServiceHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<DatabaseReadiness>("database", tags: ["ready"]);
        return services;
    }

    public static WebApplication MapServiceHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = registration => registration.Tags.Contains("ready") });
        return app;
    }
}
