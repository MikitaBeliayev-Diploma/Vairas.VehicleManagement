using System.Linq.Expressions;
using System.Text.Json;
using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Tests;

public sealed class TestPublisher : IEventPublisher
{
    public bool Fail { get; set; }
    public List<Guid> Sent { get; } = [];
    public Task PublishAsync(OutboxMessage message, CancellationToken ct)
    { if (Fail) throw new IOException("synthetic broker outage"); Sent.Add(message.MessageId); return Task.CompletedTask; }
}
