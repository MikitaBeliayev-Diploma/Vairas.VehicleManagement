namespace Vairas.VehicleManagement.Domain;
public sealed class OutboxMessage
{
    public Guid MessageId { get; set; }
    public Guid SchoolId { get; set; }
    public Guid VehicleId { get; set; }
    public long AggregateVersion { get; set; }
    public string EventType { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}
