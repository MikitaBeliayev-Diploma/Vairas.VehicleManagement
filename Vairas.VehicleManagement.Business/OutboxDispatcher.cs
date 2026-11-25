using System.Text.Json;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

public sealed class OutboxDispatcher(IVehicleStore store, IEventPublisher publisher) : IOutboxDispatcher
{
    public async Task<bool> DispatchAsync(WorkItem item, CancellationToken ct)
    {
        var lease = Guid.NewGuid();
        OutboxMessage message;
        await using (var s = await store.OpenAsync(item.SchoolId, true, ct))
        {
            var row = await s.FindAsync<OutboxMessage>(x => x.SchoolId == item.SchoolId && x.MessageId == item.ItemId, ct);
            var now = await s.NowAsync(ct);
            if (row is null || row.PublishedAt.HasValue || row.NextAttemptAt > now || row.LeaseUntil > now) return false;
            row.LeaseToken = lease; row.LeaseUntil = now.AddMinutes(2); row.Attempts++;
            await s.CommitAsync(ct);
            message = JsonSerializer.Deserialize<OutboxMessage>(JsonSerializer.Serialize(row))!;
        }
        var sent = false;
        try { await publisher.PublishAsync(message, ct); sent = true; }
        catch (Exception) when (!ct.IsCancellationRequested) { /* retained message is retried with the same broker MessageId */ }
        await using var finish = await store.OpenAsync(item.SchoolId, true, ct);
        var current = await finish.FindAsync<OutboxMessage>(x => x.SchoolId == item.SchoolId && x.MessageId == item.ItemId, ct) ?? throw VehicleErrors.Missing();
        var time = await finish.NowAsync(ct);
        if (current.LeaseToken != lease || current.LeaseUntil <= time) return true;
        current.LeaseToken = null; current.LeaseUntil = null;
        if (sent) { current.PublishedAt = time; VehicleTelemetry.EventsPublished.Add(1); }
        else current.NextAttemptAt = time.AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(current.Attempts, 8))) + Random.Shared.Next(5));
        await finish.CommitAsync(ct);
        return true;
    }
}
