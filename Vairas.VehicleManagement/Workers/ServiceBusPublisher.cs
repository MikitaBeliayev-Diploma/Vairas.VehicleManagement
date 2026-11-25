using System.Diagnostics;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Domain;
using Vairas.VehicleManagement.Integrations;
using Microsoft.Extensions.Options;
using Vairas.VehicleManagement.Configuration.Options;

namespace Vairas.VehicleManagement.Workers;
public sealed class ServiceBusPublisher(IOptions<WorkerOptions> optionsSettings) : IEventPublisher, IAsyncDisposable
{
    private readonly WorkerOptions options = optionsSettings.Value;
    private ServiceBusClient? client;
    private ServiceBusSender? sender;
    public async Task PublishAsync(OutboxMessage message, CancellationToken ct)
    {
        if (!options.OutboxEnabled || string.IsNullOrWhiteSpace(options.ServiceBusNamespace) && string.IsNullOrWhiteSpace(options.ConnectionString))
            throw VehicleErrors.Dependency("BROKER_NOT_CONFIGURED");
        client ??= string.IsNullOrWhiteSpace(options.ConnectionString)
            ? new ServiceBusClient(options.ServiceBusNamespace, new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = options.ManagedIdentityClientId }))
            : new ServiceBusClient(options.ConnectionString);
        sender ??= client.CreateSender(options.Topic);
        var outgoing = new ServiceBusMessage(BinaryData.FromString(message.PayloadJson))
        {
            MessageId = message.MessageId.ToString("D"),
            Subject = message.EventType,
            ContentType = "application/json",
            CorrelationId = message.CorrelationId
        };
        outgoing.ApplicationProperties["eventId"] = message.MessageId.ToString("D");
        outgoing.ApplicationProperties["eventType"] = message.EventType;
        outgoing.ApplicationProperties["eventVersion"] = 1;
        outgoing.ApplicationProperties["occurredAt"] = message.OccurredAt.ToString("O");
        outgoing.ApplicationProperties["producer"] = "vehicle";
        outgoing.ApplicationProperties["correlationId"] = message.CorrelationId;
        outgoing.ApplicationProperties["aggregateId"] = message.VehicleId.ToString("D");
        outgoing.ApplicationProperties["aggregateVersion"] = message.AggregateVersion;
        await sender.SendMessageAsync(outgoing, ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (sender is not null)
            await sender.DisposeAsync();
        if (client is not null)
            await client.DisposeAsync();
    }
}
