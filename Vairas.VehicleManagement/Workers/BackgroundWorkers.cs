using System.Diagnostics;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Domain;
using Vairas.VehicleManagement.Integrations;
using Microsoft.Extensions.Options;
using Vairas.VehicleManagement.Configuration.Options;

namespace Vairas.VehicleManagement.Workers;
public sealed class BackgroundWorkers(IServiceScopeFactory scopes, IOptions<WorkerOptions> optionsSettings, IOptions<SchedulingOptions> schedulingSettings, ILogger<BackgroundWorkers> logger) : BackgroundService
{
    private readonly SchedulingOptions scheduling = schedulingSettings.Value;
    private readonly WorkerOptions options = optionsSettings.Value;
    private static readonly ActivitySource Activity = new(VehicleTelemetry.Name);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
            return;
        var nextHealthLog = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IVehicleStore>();
                var health = await store.HealthAsync(stoppingToken);
                VehicleTelemetry.Update(health);
                if (DateTimeOffset.UtcNow >= nextHealthLog)
                {
                    logger.LogInformation("Vehicle backlog pending {PendingSagas} recovery {RecoverySagas} outbox {PendingOutbox} oldest {OldestOutboxSeconds} seconds", health.PendingSagas, health.RecoverySagas, health.PendingOutbox, health.OldestOutboxSeconds);
                    nextHealthLog = DateTimeOffset.UtcNow.AddMinutes(1);
                }

                if (scheduling.Enabled)
                    foreach (var item in await store.DueAsync(false, 25, stoppingToken))
                    {
                        using var trace = Activity.StartActivity("vehicle.saga.step");
                        trace?.SetTag("operation.id", item.ItemId);
                        await scope.ServiceProvider.GetRequiredService<IStateChangeProcessor>().ProcessAsync(item, stoppingToken);
                    }

                if (options.OutboxEnabled)
                    foreach (var item in await store.DueAsync(true, 25, stoppingToken))
                        await scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>().DispatchAsync(item, stoppingToken);
            }
            catch (Exception e)when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError("Vehicle worker iteration failed with type {ExceptionType}", e.GetType().Name);
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
