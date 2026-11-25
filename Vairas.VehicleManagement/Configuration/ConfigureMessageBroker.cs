using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Workers;

namespace Vairas.VehicleManagement.Configuration;

public static class ConfigureMessageBroker
{
    public static IServiceCollection AddMessageBroker(this IServiceCollection services)
    {
        services.AddSingleton<IEventPublisher, ServiceBusPublisher>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
        services.AddHostedService<BackgroundWorkers>();
        return services;
    }
}
