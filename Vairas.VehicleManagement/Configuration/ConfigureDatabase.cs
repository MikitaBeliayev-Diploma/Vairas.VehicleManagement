using Microsoft.Extensions.Options;
using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Configuration.Options;
using Vairas.VehicleManagement.Persistance;

namespace Vairas.VehicleManagement.Configuration;

public static class ConfigureDatabase
{
    public static IServiceCollection AddVehicleDatabase(this IServiceCollection services)
    {
        services.AddDbContextFactory<VehicleDbContext>((provider, builder) =>
            VehicleDatabase.Configure(builder, provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.VehicleManagement));
        services.AddScoped<IVehicleStore, VehicleStore>();
        return services;
    }
}
